namespace PermissionGraph.Infrastructure.Services.Data;

internal sealed class EfApplicationTransaction(PermissionGraphDbContext dbContext) : IApplicationTransaction
{
    public async Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new Scope(dbContext, transaction);
    }

    private sealed class Scope(PermissionGraphDbContext dbContext, IDbContextTransaction transaction) : IApplicationTransactionScope
    {
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictApplicationException("concurrency_conflict", "The resource was modified by another request.");
            }
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }
    }
}