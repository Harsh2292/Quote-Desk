namespace QuoteDesk.Api.Auth;

/// <summary>
/// Bound from the <c>Auth</c> configuration section. <see cref="GoogleOptions.ClientId"/> and
/// <see cref="JwtOptions.SigningKey"/> have no safe default and are validated at startup — see the
/// <c>.Validate(...).ValidateOnStart()</c> calls in Program.cs.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public GoogleOptions Google { get; init; } = new();
    public JwtOptions Jwt { get; init; } = new();

    /// <summary>Emails granted the "admin" role on sign-in, matched case-insensitively.</summary>
    public IReadOnlyList<string> AdminEmails { get; init; } = [];

    /// <summary>CORS origins allowed to call the API. Empty in dev — the Vite proxy is same-origin.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}

public sealed class GoogleOptions
{
    public string ClientId { get; init; } = string.Empty;
}

public sealed class JwtOptions
{
    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "quotedesk";
    public string Audience { get; init; } = "quotedesk";
    public int LifetimeHours { get; init; } = 8;
}
