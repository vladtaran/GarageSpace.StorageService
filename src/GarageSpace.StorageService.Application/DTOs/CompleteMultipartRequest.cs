using System.Collections.Generic;

namespace GarageSpace.StorageService.Application.DTOs
{
    public class CompleteMultipartRequest
    {
        public List<PartETag> Parts { get; set; } = new List<PartETag>();
    }

    public class PartETag
    {
        public int PartNumber { get; set; }
        public string ETag { get; set; } = string.Empty;
    }
}
