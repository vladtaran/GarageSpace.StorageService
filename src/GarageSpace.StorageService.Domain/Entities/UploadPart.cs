using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Domain.Entities
{
    public class UploadPart
    {
        public Guid Id { get; set; }
        public Guid UploadSessionId { get; set; }
        public int PartNumber { get; set; }
        public string ETag { get; set; } = string.Empty;
        public DateTime? UploadedAt { get; set; }
    }
}
