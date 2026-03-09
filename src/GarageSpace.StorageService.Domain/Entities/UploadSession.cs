using GarageSpace.StorageService.Domain.Enums;
using System;
using System.Collections.Generic;

namespace GarageSpace.StorageService.Domain.Entities
{
    public class UploadSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string MultipartUploadId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UploadSessionStatus Status { get; set; }
        public List<UploadPart> Parts { get; set; } = new List<UploadPart>();
    }
}
