using Microsoft.Extensions.AI;

namespace QuoteDesk.Agents.Tools;

/// <summary>The gated write tools — <c>create_quote_draft</c> and <c>send_quote</c>. Only the
/// approval workflow (task 06) is ever constructed with this registry; see
/// <see cref="ReadToolRegistry"/> for why the Resolve agent never sees it.</summary>
public sealed class WriteToolRegistry
{
    public WriteToolRegistry(QuoteWriteTools quoteWriteTools)
    {
        Tools =
        [
            AIFunctionFactory.Create(quoteWriteTools.CreateQuoteDraftAsync, new AIFunctionFactoryOptions { Name = "create_quote_draft" }),
            AIFunctionFactory.Create(quoteWriteTools.SendQuoteAsync, new AIFunctionFactoryOptions { Name = "send_quote" }),
        ];
    }

    public IReadOnlyList<AIFunction> Tools { get; }
}
