using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuoteDesk.Agents;
using QuoteDesk.Agents.Llm;
using QuoteDesk.Api;
using QuoteDesk.Api.Approvals;
using QuoteDesk.Api.Auth;
using QuoteDesk.Api.Enquiries;
using QuoteDesk.Api.Logging;
using QuoteDesk.Api.Quotes;
using QuoteDesk.Api.RateLimiting;
using QuoteDesk.Data;
using QuoteDesk.Data.Seed;
using QuoteDesk.Intake;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var connectionString = builder.Configuration.GetConnectionString("QuoteDesk")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:QuoteDesk. Set it with dotnet user-secrets.");

builder.Services.AddQuoteDeskData(connectionString);
builder.Services.AddQuoteDeskIntake();

// Bound synchronously, before the container is built, the same way the connection string and
// Auth:* settings above fail fast — a missing key stops the app at boot, not at the first SSE frame.
var llmOptions = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()
    ?? throw new InvalidOperationException($"Missing '{LlmOptions.SectionName}' configuration section.");

if (string.IsNullOrWhiteSpace(llmOptions.ApiKey))
{
    throw new InvalidOperationException("Missing Llm:ApiKey. Set it with dotnet user-secrets.");
}

builder.Services.AddQuoteDeskAgentPipeline(llmOptions);

// Together, these turn every unhandled exception into a generic RFC 9457 ProblemDetails 500 — no
// stack trace, no exception message, no connection string, per CLAUDE.md's Security rules. This
// also overrides the Development-only exception page ASP.NET Core would otherwise inject, so the
// same behaviour holds locally, under test, and in production.
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database");

// Bound synchronously, before the container is built, the same way the connection string above
// fails fast — a missing client id or signing key stops the app at boot, not at first request.
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

if (string.IsNullOrWhiteSpace(authOptions.Google.ClientId))
{
    throw new InvalidOperationException("Missing Auth:Google:ClientId. Set it with dotnet user-secrets.");
}

if (Encoding.UTF8.GetByteCount(authOptions.Jwt.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "Missing or too-short Auth:Jwt:SigningKey — needs at least 32 bytes for HS256. Set it with dotnet user-secrets.");
}

builder.Services.AddSingleton(Options.Create(authOptions));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
builder.Services.AddSingleton<IJwtIssuer, JwtIssuer>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types exactly as JwtIssuer wrote them ("sub", "role", ...) instead of the
        // legacy WS-Fed remapping JwtBearer applies by default.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.SigningKey)),
        };
    });

// The load-bearing line: every endpoint added from here on is protected by default. Forgetting
// [Authorize] on a new route in a later task cannot silently leave it open.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Empty in dev — the Vite proxy already makes the Api same-origin, so no CORS policy is registered.
if (authOptions.AllowedOrigins.Count > 0)
{
    var allowedOrigins = authOptions.AllowedOrigins.ToArray();
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

var rateLimiting = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
    ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        // Distinguishes our own limit from a model-provider rate limit, which arrives as an SSE
        // `error` event with code `provider_rate_limited` instead — never as this transport-level
        // 429. The frontend's useAgentStream maps a transport 429 to a plain error, not the
        // provider-rate-limited replay picker, precisely because this is the only source of one.
        var detail = RateLimitRejectionMessages.For(context.HttpContext.Request.Path);

        await TypedResults
            .Problem(detail, statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests.")
            .ExecuteAsync(context.HttpContext);
    };

    // Applies to every request automatically — the same "protected by default" shape the fallback
    // authorization policy above already gives every route, so an endpoint added later needs no
    // rate-limiting code of its own to get a baseline. Health checks are exempt: Container Apps (09b)
    // polls /health/live continuously, and throttling a liveness probe would be worse than not having
    // one. Partitioned by the authenticated `sub` claim when present, else client IP — CLAUDE.md's
    // "per IP and per token" — which is why UseRateLimiter (below) has to run after UseAuthentication.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (httpContext.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var key = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.GlobalPermitPerMinute,
            Window = TimeSpan.FromMinutes(1),
        });
    });

    // Stacked on top of the global limiter above (a request must pass both), only on the one
    // anonymous route — POST /api/auth/google — since each call costs a real Google token
    // verification and it is the entire surface an unauthenticated caller can reach.
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.AuthPermitPerMinute,
            Window = TimeSpan.FromMinutes(1),
        }));

    // Stacked on top of the global limiter above, only on POST /api/enquiries/{id}/process — the one
    // route that spends the shared Gemini key. AddFixedWindowLimiter always partitions on a single
    // constant key, so this is one shared bucket across every visitor: a hard cap on the whole demo,
    // not a per-user allowance, because the key's own daily quota is shared the same way.
    // POST /api/approvals/{id} deliberately does not carry this policy — ApproveExecutor makes no
    // model call at all, so the global baseline above is all it needs.
    options.AddFixedWindowLimiter("pipeline", limiterOptions =>
    {
        limiterOptions.PermitLimit = rateLimiting.PipelinePermitPerDay;
        limiterOptions.Window = TimeSpan.FromDays(1);
    });
});

var databaseOptions = builder.Configuration.GetSection(DatabaseStartupOptions.SectionName).Get<DatabaseStartupOptions>()
    ?? new DatabaseStartupOptions();

var app = builder.Build();

// `dotnet run -- --seed` fills an empty database with deterministic demo data, then exits — it
// never starts Kestrel, so it is safe to run against a fresh container before the app comes up.
if (args.Contains("--seed", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<QuoteDeskDbContext>();
    await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
    Log.Information("Seed complete.");
    return;
}

// Off by default (see DatabaseStartupOptions's remarks) — only 09b's Container Apps environment
// turns these on, since nothing else creates or seeds the schema in production.
if (databaseOptions.MigrateOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<QuoteDeskDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrated on startup.");

    if (databaseOptions.SeedOnStartup)
    {
        await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
        Log.Information("Database seeded on startup.");
    }
}

// First in the pipeline so it wraps every other middleware, including logging and auth.
app.UseExceptionHandler();

// Before UseSerilogRequestLogging, not after: that middleware logs its "Request finished" summary
// only once every downstream middleware has returned, so CorrelationMiddleware's LogContext scope
// has to still be open at that point — placing it any later would let the scope close before the
// one log line that matters most (the request summary itself) gets a chance to carry the id.
app.UseMiddleware<CorrelationMiddleware>();

app.UseSerilogRequestLogging();

if (authOptions.AllowedOrigins.Count > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

// After authentication/authorization so the GlobalLimiter's partitioner above can read the `sub`
// claim on an authenticated request, not just fall back to IP.
app.UseRateLimiter();

// Liveness never depends on anything downstream — it answers even when the database is unreachable.
// Both health checks stay outside the fallback authorization policy: a probe carries no token.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

// Readiness runs every registered check, including the database check added above.
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapAuthEndpoints();
app.MapEnquiryEndpoints();
app.MapApprovalEndpoints();
app.MapQuoteEndpoints();

app.Run();

// Exposed for WebApplicationFactory in QuoteDesk.IntegrationTests.
public partial class Program;
