namespace QuoteDesk.Data.Entities;

/// <summary>
/// A salesperson who has signed in with Google. Named <c>AppUser</c> rather than <c>User</c> so it
/// never reads ambiguously next to ASP.NET's own <c>User</c>; the table is still <c>Users</c>.
/// There is no password column and never will be — Google is the identity provider.
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>Google's <c>sub</c> claim — stable for the lifetime of the account, unlike the email.</summary>
    public required string GoogleSubject { get; set; }

    public required string Email { get; set; }
    public required string Name { get; set; }
    public string? PictureUrl { get; set; }

    /// <summary>"admin" / "sales".</summary>
    public required string Role { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset LastLoginAt { get; set; }
}
