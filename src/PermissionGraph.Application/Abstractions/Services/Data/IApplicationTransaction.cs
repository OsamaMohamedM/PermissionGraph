namespace PermissionGraph.Application.Abstractions.Data;

public interface IApplicationTransaction
{
    Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IApplicationTransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
