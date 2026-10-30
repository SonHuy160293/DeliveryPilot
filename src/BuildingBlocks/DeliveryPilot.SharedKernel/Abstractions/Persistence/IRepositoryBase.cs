using DeliveryPilot.SharedKernel.Abstractions.Domain;

namespace DeliveryPilot.SharedKernel.Abstractions.Persistence;

public interface IRepositoryBase<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    Task<TEntity> GetByIdAsync(TId id, CancellationToken cancellationToken);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken);
}
