using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Domain.Enums
{
    public enum FileStatus
    {
        Uploading = 0,
        Completed = 1,
        Failed = 2,
        Deleted = 3,
        Scanning = 4
    }
}
