using DeliveryPilot.SharedKernel.Abstractions.Domain;

namespace DeliveryPilot.SharedKernel.Abstractions.Persistence;

public interface IDeletableRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    Task DeleteByIdAsync(TId id, CancellationToken cancellationToken);
}
