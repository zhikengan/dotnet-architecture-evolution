namespace BuildingBlocks.Application;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
