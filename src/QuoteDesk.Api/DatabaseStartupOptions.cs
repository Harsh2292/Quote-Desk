namespace QuoteDesk.Api;

/// <summary>
/// Bound from the "Database" config section in <c>Program.cs</c> — task 09's deliberately small
/// answer to "nothing applies migrations or seeds the database in production" (docs/SPEC.md §6 lists
/// the schema; nothing before this ran <c>dotnet ef database update</c> outside a test).
///
/// The textbook answer is a separate migration job — a CI step or an idempotent SQL script applied
/// before the container starts — and that is still the right answer for a system with more than one
/// replica or a team that needs migrations reviewed before they run. It costs real setup here though:
/// <c>dotnet ef</c> builds the startup project, so it hits every one of <c>Program.cs</c>'s fail-fast
/// throws and needs dummy config, and applying a script from a GitHub runner needs an Azure SQL
/// firewall rule for the runner's IP. For a single-replica demo where
/// <see cref="QuoteDesk.Data.Seed.DeterministicSeeder"/> already documents itself as "safe to call on
/// every startup" (it no-ops once any customer row exists), migrating and seeding on boot is the
/// smallest thing that ships — both flags default to <see langword="false"/> so nothing changes
/// locally or under test, and only 09b's Container Apps environment turns them on.
/// </summary>
public sealed class DatabaseStartupOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; init; }

    public bool SeedOnStartup { get; init; }
}
