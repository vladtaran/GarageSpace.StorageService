using GarageSpace.StorageService.Domain.Enums;
using System;

namespace GarageSpace.StorageService.Domain.Entities
{
    public class FileMetadata
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string S3Key { get; set; } = string.Empty;
        public string S3Bucket { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public Guid UploadedByUserId { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public FileAccessLevel AccessLevel { get; set; }
        public FileStatus Status { get; set; }
        public string? Tags { get; set; }
        public string? Metadata { get; set; }
                
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
