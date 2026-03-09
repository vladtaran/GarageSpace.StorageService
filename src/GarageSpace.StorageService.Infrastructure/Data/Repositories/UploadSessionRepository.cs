using GarageSpace.StorageService.Domain.Entities;
using GarageSpace.StorageService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GarageSpace.StorageService.Infrastructure.Data.Repositories;

public class UploadSessionRepository : IUploadSessionRepository
{
    private readonly StorageProcessorDbContext _context;

    public UploadSessionRepository(StorageProcessorDbContext context)
    {
        _context = context;
    }

    public async Task<UploadSession?> GetByIdAsync(Guid id)
        => await _context.UploadSessions.FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<UploadSession>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 20)
        => await _context.UploadSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

    public async Task AddAsync(UploadSession session)
        => await _context.UploadSessions.AddAsync(session);

    public Task UpdateAsync(UploadSession session)
    {
        _context.UploadSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.UploadSessions.FindAsync(id);
        if (entity is not null)
            _context.UploadSessions.Remove(entity);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
