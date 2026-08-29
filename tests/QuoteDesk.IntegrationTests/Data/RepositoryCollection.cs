namespace QuoteDesk.IntegrationTests.Data;

/// <summary>
/// Every test class hitting <see cref="RepositoryFixture"/> shares this collection instead of each
/// declaring its own <c>IClassFixture</c> — the same reasoning as
/// QuoteDesk.IntegrationTests.Api.QuoteDeskApiCollection: two fixtures racing to
/// <c>EnsureDeleted</c>/<c>Migrate</c>/seed the same "QuoteDeskTests_Repository" database at once
/// fails intermittently. One shared instance, seeded once.
/// </summary>
[CollectionDefinition("Repository")]
public class RepositoryCollection : ICollectionFixture<RepositoryFixture>;
