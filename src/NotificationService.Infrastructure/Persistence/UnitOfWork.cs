using Microsoft.EntityFrameworkCore.Storage;
using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Persistence
{
    public sealed class UnitOfWork(NotificationDbContext dbContext) : IUnitOfWork
    {
        private readonly NotificationDbContext _dbContext = dbContext;
        private IDbContextTransaction? _transaction;

        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            _transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            await _dbContext.SaveChangesAsync(ct);
            await _transaction!.CommitAsync(ct);
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(ct);
        }
    }
}
