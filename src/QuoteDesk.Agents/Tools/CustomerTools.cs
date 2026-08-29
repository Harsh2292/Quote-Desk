using System.ComponentModel;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Tools;

/// <summary><c>resolve_customer</c> and <c>get_customer_history</c> — read-only, per docs/SPEC.md §7.</summary>
public sealed class CustomerTools(ICustomerRepository customers, IOrderHistoryRepository orderHistory)
{
    [Description(
        "Finds the customer record for an enquiry's sender, tried in this order: the sender's email domain, " +
        "then their WhatsApp number, then an exact match on the company name. Call this first on every " +
        "enquiry — the tier and credit terms it returns drive pricing. When Found is false, no customer " +
        "record matches: treat this as a new-customer enquiry (list price only, no credit terms, flag it " +
        "for a human to verify) rather than guessing which existing customer it might be.")]
    public async Task<CustomerMatch> ResolveCustomerAsync(
        [Description("The company name as written in the enquiry, e.g. 'Shreeji Textiles'. Used only if the sender's email domain and WhatsApp number both miss.")]
        string companyName,
        [Description("The sender's email address or WhatsApp number, exactly as the enquiry arrived with.")]
        string senderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(senderId);

        if (TryGetEmailDomain(senderId, out var domain))
        {
            var byDomain = await customers.FindByEmailDomainAsync(domain, cancellationToken);
            if (byDomain is not null)
            {
                return Found(byDomain, $"Matched by sender email domain '{domain}'.");
            }
        }

        var byWhatsApp = await customers.FindByWhatsAppNumberAsync(senderId, cancellationToken);
        if (byWhatsApp is not null)
        {
            return Found(byWhatsApp, $"Matched by WhatsApp number '{senderId}'.");
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var byName = await customers.FindByNameAsync(companyName, cancellationToken);
            if (byName is not null)
            {
                return Found(byName, $"Matched by exact company name '{companyName}'.");
            }
        }

        return new CustomerMatch
        {
            Found = false,
            Reason = "No customer record matches this sender's email domain, WhatsApp number or company name — treat as a new-customer enquiry.",
        };
    }

    [Description(
        "Lists a customer's prior purchases, most recent first. This is what resolves phrases like " +
        "'same as last time' or 'our usual rate' — call it whenever an enquiry references past business, " +
        "and read the SKU and price it names rather than assuming the customer means the most recent order " +
        "of anything. An empty list means no matching prior purchases were found.")]
    public async Task<IReadOnlyList<PriorPurchase>> GetCustomerHistoryAsync(
        [Description("The customer's Id, from a prior resolve_customer call.")]
        int customerId,
        [Description("Narrows the history to one SKU. Omit (null) to see every prior purchase.")]
        string? sku,
        CancellationToken cancellationToken)
    {
        var orders = await orderHistory.GetByCustomerAsync(customerId, sku, cancellationToken);

        return [.. orders.Select(o => new PriorPurchase
        {
            Sku = o.Sku,
            Qty = o.Qty,
            UnitPrice = o.UnitPrice,
            OrderedAt = o.OrderedAt,
        })];
    }

    private static CustomerMatch Found(CustomerRecord customer, string reason) => new()
    {
        Found = true,
        CustomerId = customer.Id,
        Name = customer.Name,
        Tier = customer.Tier.ToString(),
        CreditDays = customer.CreditDays,
        DefaultShipTo = customer.DefaultShipTo,
        Reason = reason,
    };

    private static bool TryGetEmailDomain(string senderId, out string domain)
    {
        var at = senderId.IndexOf('@', StringComparison.Ordinal);
        if (at < 0 || at == senderId.Length - 1)
        {
            domain = string.Empty;
            return false;
        }

        domain = senderId[(at + 1)..];
        return true;
    }
}
