using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.DTOs
{
    public class InitiateMultipartRequest
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;

        // Optional: hint for how many parts/desired chunk size (frontend/backend may ignore)
        public int? SuggestedPartSizeBytes { get; set; }

        // Optional metadata attached to the file
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
