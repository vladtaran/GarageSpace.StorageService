using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.DTOs
{
    public class InitiateUploadResponse
    {
        public Guid FileId { get; set; }
        public string PresignedUrl { get; set; } = string.Empty;
        public int PresignedUrlExpirationSeconds { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
