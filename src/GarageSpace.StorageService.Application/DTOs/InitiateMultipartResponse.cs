using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.DTOs
{
    public class InitiateMultipartResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public Guid FileId { get; set; }
        public List<string> PresignedUrls { get; set; } = new List<string>();
        public int ExpirationSeconds { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
