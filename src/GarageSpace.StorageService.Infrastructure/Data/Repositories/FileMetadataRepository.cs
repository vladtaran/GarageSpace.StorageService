using GarageSpace.StorageService.Domain.Entities;
using GarageSpace.StorageService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GarageSpace.StorageService.Infrastructure.Data.Repositories;

public class FileMetadataRepository : IFileMetadataRepository
{
    private readonly StorageProcessorDbContext _context;

    public FileMetadataRepository(StorageProcessorDbContext context)
    {
        _context = context;
    }

    public async Task<FileMetadata?> GetByIdAsync(Guid id)
        => await _context.FileMetadata.FirstOrDefaultAsync(f => f.Id == id);

    public async Task<FileMetadata?> GetByS3KeyAsync(string s3Key)
        => await _context.FileMetadata.FirstOrDefaultAsync(f => f.S3Key == s3Key);

    public async Task<List<FileMetadata>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 20)
        => await _context.FileMetadata
            .Where(f => f.UploadedByUserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

    public async Task AddAsync(FileMetadata fileMetadata)
        => await _context.FileMetadata.AddAsync(fileMetadata);

    public Task UpdateAsync(FileMetadata fileMetadata)
    {
        _context.FileMetadata.Update(fileMetadata);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.FileMetadata.FindAsync(id);
        if (entity is not null)
            _context.FileMetadata.Remove(entity);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
