using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuoteDesk.Agents;
using QuoteDesk.Api.Auth;
using QuoteDesk.Api.Enquiries;
using QuoteDesk.Data;
using QuoteDesk.Data.Seed;
using QuoteDesk.Intake;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var connectionString = builder.Configuration.GetConnectionString("QuoteDesk")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:QuoteDesk. Set it with dotnet user-secrets.");

builder.Services.AddQuoteDeskData(connectionString);
builder.Services.AddQuoteDeskIntake();
builder.Services.AddQuoteDeskAgents();

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

// First in the pipeline so it wraps every other middleware, including logging and auth.
app.UseExceptionHandler();

app.UseSerilogRequestLogging();

if (authOptions.AllowedOrigins.Count > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

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

app.Run();

// Exposed for WebApplicationFactory in QuoteDesk.IntegrationTests.
public partial class Program;
