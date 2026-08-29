using Microsoft.Extensions.AI;

namespace QuoteDesk.Agents.Tools;

/// <summary>
/// The tools the Resolve agent is constructed with — read-only, per docs/SPEC.md §7. Deliberately a
/// separate object from <see cref="WriteToolRegistry"/> rather than one registry with a filter: the
/// Resolve agent is never given a reference to <see cref="WriteToolRegistry"/> at all, so there is no
/// runtime check to bypass. That separation is the entire enforcement of CLAUDE.md rule 3, "nothing
/// leaves without a human."
/// </summary>
public sealed class ReadToolRegistry
{
    public ReadToolRegistry(CustomerTools customerTools, CatalogTools catalogTools, StockTools stockTools, PricingTools pricingTools)
    {
        Tools =
        [
            AIFunctionFactory.Create(customerTools.ResolveCustomerAsync, Named("resolve_customer")),
            AIFunctionFactory.Create(customerTools.GetCustomerHistoryAsync, Named("get_customer_history")),
            AIFunctionFactory.Create(catalogTools.SearchCatalogAsync, Named("search_catalog")),
            AIFunctionFactory.Create(stockTools.CheckStockAsync, Named("check_stock")),
            AIFunctionFactory.Create(pricingTools.PriceQuoteAsync, Named("price_quote")),
        ];
    }

    public IReadOnlyList<AIFunction> Tools { get; }

    // AIFunctionNameAttribute would name a function too, but it is marked [Experimental("MEAI001")]
    // in this package version — passing the name explicitly here achieves the same docs/SPEC.md §7
    // snake_case contract without depending on an unstable API. Descriptions still come from each
    // method's [Description] attribute, which is not experimental.
    private static AIFunctionFactoryOptions Named(string name) => new() { Name = name };
}
