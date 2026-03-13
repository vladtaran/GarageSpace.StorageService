using System.Security.Cryptography;
using GarageSpace.StorageService.Application.Services;

namespace GarageSpace.StorageService.Infrastructure.Services;

public class FileValidationService : IFileValidationService
{
    private const long MaxFileSizeBytes = 5L * 1024 * 1024 * 1024; // 5 GB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        "video/mp4",
        "video/mpeg",
        "video/webm",
        "audio/mpeg",
        "audio/wav",
        "audio/ogg",
        "application/pdf",
        "application/zip",
        "application/x-zip-compressed",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv",
        "application/octet-stream",
    };

    public async Task<IFileValidationService.ValidationResult> ValidateFileAsync(
        string fileName,
        long fileSize,
        string mimeType,
        Stream fileStream)
    {
        var result = new IFileValidationService.ValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(fileName))
            result.Errors.Add("File name is required.");

        if (fileSize <= 0)
            result.Errors.Add("File size must be greater than zero.");

        if (fileSize > MaxFileSizeBytes)
            result.Errors.Add(
                $"File size {fileSize} bytes exceeds the maximum allowed size of {MaxFileSizeBytes} bytes.");

        if (string.IsNullOrWhiteSpace(mimeType) || !AllowedMimeTypes.Contains(mimeType))
            result.Errors.Add($"MIME type '{mimeType}' is not allowed.");

        if (fileStream is { CanRead: true, Length: > 0 })
        {
            var originalPosition = fileStream.Position;
            result.ContentHash = await ComputeSha256Async(fileStream);
            fileStream.Position = originalPosition;
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static async Task<string> ComputeSha256Async(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
