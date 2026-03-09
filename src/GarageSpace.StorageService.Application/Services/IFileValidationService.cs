using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.Services
{
    public interface IFileValidationService
    {
        Task<ValidationResult> ValidateFileAsync(
            string fileName,
            long fileSize,
            string mimeType,
            Stream fileStream);

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public string? ContentHash { get; set; }
        }
    }
}
