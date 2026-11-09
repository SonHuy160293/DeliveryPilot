using System.Data;
using System.Data.Common;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

/// <summary>
/// Pipeline-owned command transaction. Handlers and stores never call <c>SaveChangesAsync</c>.
/// </summary>
public sealed class ProjectsUnitOfWork(ProjectsDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            TResponse response = await action(cancellationToken);
            if (response is Result { IsFailure: true })
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return response;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (DbException)
            {
                // Preserve the original command/commit exception.
            }
            catch (InvalidOperationException)
            {
                // The transaction may already be completed; preserve the original exception.
            }

            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
