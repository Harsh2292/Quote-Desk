namespace QuoteDesk.Data.Repositories;

public interface IUserRepository
{
    Task<UserRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the user on first sign-in, or refreshes their profile and login time on every one
    /// after. Matched on <see cref="GoogleUserUpsert.GoogleSubject"/>, never on the email — a Google
    /// account can change its email address but never its subject.
    /// </summary>
    Task<UserRecord> UpsertFromGoogleAsync(GoogleUserUpsert upsert, CancellationToken cancellationToken);
}
