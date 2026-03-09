using GarageSpace.StorageService.Application.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarageSpace.StorageService.Application.Services
{
    public interface IUploadService
    {
        Task<InitiateUploadResponse> InitiatePresignedUploadAsync(
            InitiateUploadRequest request,
            Guid userId);

        Task<InitiateMultipartResponse> InitiateMultipartUploadAsync(
            InitiateMultipartRequest request,
            Guid userId);

        Task<FileMetadataDto> CompleteMultipartUploadAsync(
            string uploadId,
            CompleteMultipartRequest request,
            Guid userId);

        Task<FileMetadataDto> UploadFileDirectAsync(
            string fileName,
            Stream fileStream,
            Guid userId);

        Task<FileMetadataDto> GetFileMetadataAsync(Guid fileId);

        Task DeleteFileAsync(Guid fileId, Guid userId);
    }
}
