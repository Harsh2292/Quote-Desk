namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// Every test class hitting <see cref="QuoteDeskApiFactory"/> shares this collection instead of
/// each declaring its own <c>IClassFixture</c> — xUnit runs test classes in different collections
/// in parallel, and two factories racing to migrate the same "QuoteDeskTests_Api" database at once
/// fails with "Database already exists". One shared instance, migrated once.
/// </summary>
[CollectionDefinition("QuoteDeskApi")]
public class QuoteDeskApiCollection : ICollectionFixture<QuoteDeskApiFactory>;
