using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.Services
{
    public interface IS3Service
    {
        Task<S3UploadResult> UploadFileAsync(
            string fileName,
            Stream fileStream,
            string contentType);

        Task<string> InitiateMultipartUploadAsync(
            string fileName,
            string contentType);

        Task<List<string>> GeneratePresignedPartUrlsAsync(
            string uploadId,
            int partCount,
            TimeSpan expiration);

        Task<string> GeneratePresignedUploadUrlAsync(
            string key,
            TimeSpan expiration);

        Task<string> GeneratePresignedDownloadUrlAsync(
            string key,
            TimeSpan expiration);

        Task<S3UploadResult> CompleteMultipartUploadAsync(
            string uploadId,
            List<PartETag> parts);

        Task DeleteFileAsync(string key);

        Task<S3ObjectMetadata> GetObjectMetadataAsync(string key);
    }

    public class S3UploadResult
    {
        public string S3Key { get; set; } = string.Empty;
        public string S3Bucket { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class S3ObjectMetadata
    {
        public string Key { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string ETag { get; set; } = string.Empty;
    }

    public class PartETag
    {
        public int PartNumber { get; set; }
        public string ETag { get; set; } = string.Empty;
    }
}
