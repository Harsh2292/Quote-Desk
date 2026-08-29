namespace QuoteDesk.Data.Repositories;

public interface ICustomerRepository
{
    Task<CustomerRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<CustomerRecord?> FindByEmailDomainAsync(string domain, CancellationToken cancellationToken);

    Task<CustomerRecord?> FindByWhatsAppNumberAsync(string number, CancellationToken cancellationToken);

    Task<CustomerRecord?> FindByNameAsync(string name, CancellationToken cancellationToken);
}
