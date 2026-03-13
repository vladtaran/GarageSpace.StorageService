using GarageSpace.StorageService.Application.DTOs;
using GarageSpace.StorageService.Application.Services;
using GarageSpace.StorageService.Domain.Entities;
using GarageSpace.StorageService.Domain.Enums;
using GarageSpace.StorageService.Domain.Repositories;
using System.Text.Json;

namespace GarageSpace.StorageService.Infrastructure.Services;

public class UploadService : IUploadService
{
    private const int PresignedUrlExpirationSeconds = 3600; // 1 hour
    private const int MultipartExpirationSeconds = 7200;    // 2 hours
    private const long DefaultPartSizeBytes = 10L * 1024 * 1024; // 10 MB
    private const long MinPartSizeBytes = 5L * 1024 * 1024;      // 5 MB (AWS minimum)

    private readonly IS3Service _s3Service;
    private readonly IFileMetadataRepository _fileMetadataRepository;
    private readonly IUploadSessionRepository _uploadSessionRepository;

    public UploadService(
        IS3Service s3Service,
        IFileMetadataRepository fileMetadataRepository,
        IUploadSessionRepository uploadSessionRepository)
    {
        _s3Service = s3Service;
        _fileMetadataRepository = fileMetadataRepository;
        _uploadSessionRepository = uploadSessionRepository;
    }

    // -------------------------------------------------------------------------
    // Presigned upload
    // -------------------------------------------------------------------------

    public async Task<InitiateUploadResponse> InitiatePresignedUploadAsync(
        InitiateUploadRequest request,
        Guid userId)
    {
        var s3Key = BuildS3Key(request.FileName);
        var expiration = TimeSpan.FromSeconds(PresignedUrlExpirationSeconds);
        var expiresAt = DateTime.UtcNow.Add(expiration);

        var presignedUrl = await _s3Service.GeneratePresignedUploadUrlAsync(s3Key, expiration);

        var now = DateTime.UtcNow;
        var fileMetadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            FileName = request.FileName,
            S3Key = s3Key,
            S3Bucket = string.Empty,
            FileSizeBytes = request.FileSize,
            MimeType = request.MimeType,
            ContentHash = string.Empty,
            UploadedByUserId = userId,
            UploadedAt = now,
            ExpirationDate = null,
            AccessLevel = ParseAccessLevel(request.AccessLevel),
            Status = FileStatus.Uploading,
            Tags = null,
            Metadata = request.Metadata is not null
                ? JsonSerializer.Serialize(request.Metadata)
                : null,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await _fileMetadataRepository.AddAsync(fileMetadata);
        await _fileMetadataRepository.SaveChangesAsync();

        return new InitiateUploadResponse
        {
            FileId = fileMetadata.Id,
            PresignedUrl = presignedUrl,
            PresignedUrlExpirationSeconds = PresignedUrlExpirationSeconds,
            ExpiresAt = expiresAt,
        };
    }

    // -------------------------------------------------------------------------
    // Multipart upload
    // -------------------------------------------------------------------------

    public async Task<InitiateMultipartResponse> InitiateMultipartUploadAsync(
        InitiateMultipartRequest request,
        Guid userId)
    {
        var partSizeBytes = request.SuggestedPartSizeBytes.HasValue
            ? Math.Max(request.SuggestedPartSizeBytes.Value, MinPartSizeBytes)
            : DefaultPartSizeBytes;

        var partCount = (int)Math.Ceiling((double)request.FileSize / partSizeBytes);
        partCount = Math.Max(1, partCount);

        var expiration = TimeSpan.FromSeconds(MultipartExpirationSeconds);
        var expiresAt = DateTime.UtcNow.Add(expiration);

        // Initiate multipart upload in S3 (returns compound "{s3Key}|{awsUploadId}")
        var compoundUploadId = await _s3Service.InitiateMultipartUploadAsync(
            request.FileName, request.MimeType);

        // The S3 key is embedded in the compound upload ID so we can track it immediately.
        var s3Key = ExtractS3Key(compoundUploadId);

        var presignedPartUrls = await _s3Service.GeneratePresignedPartUrlsAsync(
            compoundUploadId, partCount, expiration);

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();

        var fileMetadata = new FileMetadata
        {
            Id = fileId,
            FileName = request.FileName,
            S3Key = s3Key,
            S3Bucket = string.Empty,
            FileSizeBytes = request.FileSize,
            MimeType = request.MimeType,
            ContentHash = string.Empty,
            UploadedByUserId = userId,
            UploadedAt = now,
            AccessLevel = FileAccessLevel.Private,
            Status = FileStatus.Uploading,
            Metadata = request.Metadata is not null
                ? JsonSerializer.Serialize(request.Metadata)
                : null,
            CreatedAt = now,
        };

        var uploadSession = new UploadSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = request.FileName,
            MultipartUploadId = compoundUploadId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Status = UploadSessionStatus.InProgress,
        };

        await _fileMetadataRepository.AddAsync(fileMetadata);
        await _uploadSessionRepository.AddAsync(uploadSession);
        await _fileMetadataRepository.SaveChangesAsync();
        await _uploadSessionRepository.SaveChangesAsync();

        return new InitiateMultipartResponse
        {
            UploadId = uploadSession.Id.ToString(),
            FileId = fileId,
            PresignedUrls = presignedPartUrls,
            ExpirationSeconds = MultipartExpirationSeconds,
            ExpiresAt = expiresAt,
        };
    }

    public async Task<FileMetadataDto> CompleteMultipartUploadAsync(
        string uploadId,
        CompleteMultipartRequest request,
        Guid userId)
    {
        if (!Guid.TryParse(uploadId, out var sessionId))
            throw new ArgumentException("Invalid uploadId format.", nameof(uploadId));

        var session = await _uploadSessionRepository.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException($"Upload session '{uploadId}' not found.");

        if (session.UserId != userId)
            throw new UnauthorizedAccessException("You are not authorized to complete this upload.");

        var s3Parts = request.Parts
            .Select(p => new Application.Services.PartETag
            {
                PartNumber = p.PartNumber,
                ETag = p.ETag,
            })
            .ToList();

        var s3Result = await _s3Service.CompleteMultipartUploadAsync(
            session.MultipartUploadId, s3Parts);

        // The S3 key was stored in the FileMetadata at initiation time (extracted from the compound upload ID).
        var pendingS3Key = ExtractS3Key(session.MultipartUploadId);
        var fileMetadata = await _fileMetadataRepository.GetByS3KeyAsync(pendingS3Key)
            ?? throw new InvalidOperationException(
                $"File metadata for upload session '{uploadId}' not found.");

        fileMetadata.S3Key = s3Result.S3Key;
        fileMetadata.S3Bucket = s3Result.S3Bucket;
        fileMetadata.FileSizeBytes = s3Result.FileSizeBytes;
        fileMetadata.ContentHash = s3Result.ContentHash;
        fileMetadata.Status = FileStatus.Completed;
        fileMetadata.UploadedAt = s3Result.UploadedAt;
        fileMetadata.UpdatedAt = DateTime.UtcNow;

        session.Status = UploadSessionStatus.Completed;

        await _fileMetadataRepository.UpdateAsync(fileMetadata);
        await _uploadSessionRepository.UpdateAsync(session);
        await _fileMetadataRepository.SaveChangesAsync();
        await _uploadSessionRepository.SaveChangesAsync();

        return MapToDto(fileMetadata);
    }

    // -------------------------------------------------------------------------
    // Direct upload
    // -------------------------------------------------------------------------

    public async Task<FileMetadataDto> UploadFileDirectAsync(
        string fileName,
        Stream fileStream,
        Guid userId)
    {
        var contentType = "application/octet-stream";
        var s3Result = await _s3Service.UploadFileAsync(fileName, fileStream, contentType);

        var now = DateTime.UtcNow;
        var fileMetadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            S3Key = s3Result.S3Key,
            S3Bucket = s3Result.S3Bucket,
            FileSizeBytes = s3Result.FileSizeBytes,
            MimeType = contentType,
            ContentHash = s3Result.ContentHash,
            UploadedByUserId = userId,
            UploadedAt = s3Result.UploadedAt,
            AccessLevel = FileAccessLevel.Private,
            Status = FileStatus.Completed,
            CreatedAt = now,
        };

        await _fileMetadataRepository.AddAsync(fileMetadata);
        await _fileMetadataRepository.SaveChangesAsync();

        return MapToDto(fileMetadata);
    }

    // -------------------------------------------------------------------------
    // Metadata & deletion
    // -------------------------------------------------------------------------

    public async Task<FileMetadataDto> GetFileMetadataAsync(Guid fileId)
    {
        var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileId)
            ?? throw new InvalidOperationException($"File '{fileId}' not found.");

        return MapToDto(fileMetadata);
    }

    public async Task DeleteFileAsync(Guid fileId, Guid userId)
    {
        var fileMetadata = await _fileMetadataRepository.GetByIdAsync(fileId)
            ?? throw new InvalidOperationException($"File '{fileId}' not found.");

        if (fileMetadata.UploadedByUserId != userId)
            throw new UnauthorizedAccessException("You are not authorized to delete this file.");

        if (!string.IsNullOrEmpty(fileMetadata.S3Key))
            await _s3Service.DeleteFileAsync(fileMetadata.S3Key);

        fileMetadata.Status = FileStatus.Deleted;
        fileMetadata.UpdatedAt = DateTime.UtcNow;

        await _fileMetadataRepository.UpdateAsync(fileMetadata);
        await _fileMetadataRepository.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string BuildS3Key(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return $"{Guid.NewGuid()}{ext}";
    }

    /// <summary>
    /// Extracts the S3 object key from the compound upload ID returned by
    /// <see cref="IS3Service.InitiateMultipartUploadAsync"/>, which has the form
    /// <c>"{s3Key}|{awsUploadId}"</c>.
    /// </summary>
    private static string ExtractS3Key(string compoundUploadId)
    {
        var separatorIndex = compoundUploadId.IndexOf('|');
        return separatorIndex >= 0 ? compoundUploadId[..separatorIndex] : compoundUploadId;
    }

    private static FileAccessLevel ParseAccessLevel(string? accessLevel) =>
        accessLevel?.ToLowerInvariant() switch
        {
            "public" => FileAccessLevel.Public,
            "internal" => FileAccessLevel.Internal,
            _ => FileAccessLevel.Private,
        };

    private static FileMetadataDto MapToDto(FileMetadata f) => new()
    {
        Id = f.Id,
        FileName = f.FileName,
        S3Key = f.S3Key,
        S3Bucket = f.S3Bucket,
        FileSizeBytes = f.FileSizeBytes,
        MimeType = f.MimeType,
        UploadedByUserId = f.UploadedByUserId,
        UploadedAt = f.UploadedAt,
        ExpirationDate = f.ExpirationDate,
        AccessLevel = f.AccessLevel,
        Status = f.Status,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
    };
}
