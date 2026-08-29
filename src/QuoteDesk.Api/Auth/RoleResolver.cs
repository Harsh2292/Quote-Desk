namespace QuoteDesk.Api.Auth;

/// <summary>
/// "admin" for any email in <c>Auth:AdminEmails</c> (matched case-insensitively, since email
/// addresses are), "sales" otherwise. Pure, so it is unit tested without a database or a token.
/// </summary>
public static class RoleResolver
{
    public const string Admin = "admin";
    public const string Sales = "sales";

    public static string Resolve(string email, IReadOnlyList<string> adminEmails)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(adminEmails);

        var isAdmin = adminEmails.Any(admin => string.Equals(admin, email, StringComparison.OrdinalIgnoreCase));
        return isAdmin ? Admin : Sales;
    }
}
