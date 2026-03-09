using GarageSpace.StorageService.Domain.Entities;

namespace GarageSpace.StorageService.Domain.Repositories;

public interface IUploadSessionRepository
{
    Task<UploadSession?> GetByIdAsync(Guid id);
    Task<List<UploadSession>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 20);
    Task AddAsync(UploadSession session);
    Task UpdateAsync(UploadSession session);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
