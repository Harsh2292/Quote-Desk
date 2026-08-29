using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class UserRepository(QuoteDeskDbContext db) : IUserRepository
{
    public async Task<UserRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null ? null : ToRecord(user);
    }

    public async Task<UserRecord> UpsertFromGoogleAsync(GoogleUserUpsert upsert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upsert);

        // Tracked deliberately — unlike every read in this layer, this one is going to be written back.
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.GoogleSubject == upsert.GoogleSubject, cancellationToken);

        if (user is null)
        {
            user = new Entities.AppUser
            {
                GoogleSubject = upsert.GoogleSubject,
                Email = upsert.Email,
                Name = upsert.Name,
                PictureUrl = upsert.PictureUrl,
                Role = upsert.Role,
                CreatedAt = upsert.SignedInAt,
                LastLoginAt = upsert.SignedInAt,
            };

            db.Users.Add(user);
        }
        else
        {
            // The profile is Google's to own, so it is refreshed on every sign-in rather than
            // frozen at whatever it was the first time. CreatedAt is the one field never touched.
            user.Email = upsert.Email;
            user.Name = upsert.Name;
            user.PictureUrl = upsert.PictureUrl;
            user.Role = upsert.Role;
            user.LastLoginAt = upsert.SignedInAt;
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(user);
    }

    private static UserRecord ToRecord(Entities.AppUser u) =>
        new(u.Id, u.GoogleSubject, u.Email, u.Name, u.PictureUrl, u.Role, u.CreatedAt, u.LastLoginAt);
}
