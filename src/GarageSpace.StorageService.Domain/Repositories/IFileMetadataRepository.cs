using GarageSpace.StorageService.Domain.Entities;

namespace GarageSpace.StorageService.Domain.Repositories;

public interface IFileMetadataRepository
{
    Task<FileMetadata?> GetByIdAsync(Guid id);
    Task<FileMetadata?> GetByS3KeyAsync(string s3Key);
    Task<List<FileMetadata>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 20);
    Task AddAsync(FileMetadata fileMetadata);
    Task UpdateAsync(FileMetadata fileMetadata);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
