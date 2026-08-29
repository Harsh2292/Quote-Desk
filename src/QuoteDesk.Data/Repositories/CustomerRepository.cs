using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class CustomerRepository(QuoteDeskDbContext db) : ICustomerRepository
{
    public async Task<CustomerRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer is null ? null : ToRecord(customer);
    }

    public async Task<CustomerRecord?> FindByEmailDomainAsync(string domain, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EmailDomain == domain, cancellationToken);

        return customer is null ? null : ToRecord(customer);
    }

    public async Task<CustomerRecord?> FindByWhatsAppNumberAsync(string number, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.WhatsAppNumber == number, cancellationToken);

        return customer is null ? null : ToRecord(customer);
    }

    public async Task<CustomerRecord?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == name, cancellationToken);

        return customer is null ? null : ToRecord(customer);
    }

    private static CustomerRecord ToRecord(Entities.Customer c) =>
        new(c.Id, c.Name, c.EmailDomain, c.WhatsAppNumber, c.Tier, c.CreditDays, c.GstIn, c.DefaultShipTo);
}
