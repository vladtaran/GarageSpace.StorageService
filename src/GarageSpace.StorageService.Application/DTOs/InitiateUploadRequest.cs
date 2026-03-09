using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.DTOs
{
    public class InitiateUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;

        // Optional access level hint; if not provided backend chooses default (e.g., Private)
        public string? AccessLevel { get; set; }

        // Optional metadata attached to the file
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
