using FluentAssertions;
using GarageSpace.StorageService.Application.DTOs;
using GarageSpace.StorageService.Application.Services;
using GarageSpace.StorageService.Domain.Entities;
using GarageSpace.StorageService.Domain.Enums;
using GarageSpace.StorageService.Domain.Repositories;
using GarageSpace.StorageService.Infrastructure.Services;
using Moq;
using AppPartETag = GarageSpace.StorageService.Application.DTOs.PartETag;

namespace GarageSpace.StorageService.UnitTests.Services;

public class UploadServiceTests
{
    private readonly Mock<IS3Service> _s3ServiceMock = new();
    private readonly Mock<IFileMetadataRepository> _fileMetadataRepoMock = new();
    private readonly Mock<IUploadSessionRepository> _uploadSessionRepoMock = new();

    private readonly UploadService _sut;

    public UploadServiceTests()
    {
        _sut = new UploadService(
            _s3ServiceMock.Object,
            _fileMetadataRepoMock.Object,
            _uploadSessionRepoMock.Object);
    }

    // -------------------------------------------------------------------------
    // InitiatePresignedUploadAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InitiatePresignedUploadAsync_HappyPath_ReturnsPresignedUrlAndFileId()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateUploadRequest
        {
            FileName = "photo.jpg",
            FileSize = 2048,
            MimeType = "image/jpeg",
        };

        _s3ServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://s3.example.com/presigned");

        var response = await _sut.InitiatePresignedUploadAsync(request, userId);

        response.FileId.Should().NotBe(Guid.Empty);
        response.PresignedUrl.Should().Be("https://s3.example.com/presigned");
        response.PresignedUrlExpirationSeconds.Should().Be(3600);
        response.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InitiatePresignedUploadAsync_SavesFileMetadataWithUploadingStatus()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateUploadRequest
        {
            FileName = "report.pdf",
            FileSize = 512,
            MimeType = "application/pdf",
        };

        FileMetadata? savedMetadata = null;
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://presigned");
        _fileMetadataRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(m => savedMetadata = m)
            .Returns(Task.CompletedTask);

        await _sut.InitiatePresignedUploadAsync(request, userId);

        savedMetadata.Should().NotBeNull();
        savedMetadata!.Status.Should().Be(FileStatus.Uploading);
        savedMetadata.FileName.Should().Be("report.pdf");
        savedMetadata.MimeType.Should().Be("application/pdf");
        savedMetadata.FileSizeBytes.Should().Be(512);
        savedMetadata.UploadedByUserId.Should().Be(userId);
    }

    [Theory]
    [InlineData("public", FileAccessLevel.Public)]
    [InlineData("PUBLIC", FileAccessLevel.Public)]
    [InlineData("internal", FileAccessLevel.Internal)]
    [InlineData("private", FileAccessLevel.Private)]
    [InlineData(null, FileAccessLevel.Private)]
    [InlineData("unknown", FileAccessLevel.Private)]
    public async Task InitiatePresignedUploadAsync_ParsesAccessLevelCorrectly(
        string? accessLevelInput,
        FileAccessLevel expectedAccessLevel)
    {
        var request = new InitiateUploadRequest
        {
            FileName = "file.txt",
            FileSize = 100,
            MimeType = "text/plain",
            AccessLevel = accessLevelInput,
        };

        FileMetadata? savedMetadata = null;
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://presigned");
        _fileMetadataRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(m => savedMetadata = m)
            .Returns(Task.CompletedTask);

        await _sut.InitiatePresignedUploadAsync(request, Guid.NewGuid());

        savedMetadata!.AccessLevel.Should().Be(expectedAccessLevel);
    }

    [Fact]
    public async Task InitiatePresignedUploadAsync_SerializesMetadataDictionary()
    {
        var request = new InitiateUploadRequest
        {
            FileName = "file.txt",
            FileSize = 100,
            MimeType = "text/plain",
            Metadata = new Dictionary<string, string> { ["project"] = "GarageSpace" },
        };

        FileMetadata? savedMetadata = null;
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://presigned");
        _fileMetadataRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(m => savedMetadata = m)
            .Returns(Task.CompletedTask);

        await _sut.InitiatePresignedUploadAsync(request, Guid.NewGuid());

        savedMetadata!.Metadata.Should().Contain("GarageSpace");
    }

    // -------------------------------------------------------------------------
    // InitiateMultipartUploadAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InitiateMultipartUploadAsync_HappyPath_ReturnsPartUrlsAndFileId()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateMultipartRequest
        {
            FileName = "video.mp4",
            FileSize = 100 * 1024 * 1024, // 100 MB → 10 parts at 10 MB default
            MimeType = "video/mp4",
        };

        const string compoundId = "s3key.mp4|aws-upload-id";
        var expectedUrls = Enumerable.Range(1, 10).Select(i => $"https://part{i}").ToList();

        _s3ServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync("video.mp4", "video/mp4"))
            .ReturnsAsync(compoundId);
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedPartUrlsAsync(compoundId, 10, It.IsAny<TimeSpan>()))
            .ReturnsAsync(expectedUrls);

        var response = await _sut.InitiateMultipartUploadAsync(request, userId);

        response.FileId.Should().NotBe(Guid.Empty);
        response.UploadId.Should().NotBeNullOrEmpty();
        response.PresignedUrls.Should().HaveCount(10);
        response.ExpirationSeconds.Should().Be(7200);
    }

    [Theory]
    [InlineData(10 * 1024 * 1024, null, 1)]             // 10 MB, default part size → 1 part
    [InlineData(50 * 1024 * 1024, null, 5)]             // 50 MB, default part size → 5 parts
    [InlineData(50 * 1024 * 1024, 10 * 1024 * 1024, 5)] // 50 MB, explicit 10 MB part → 5 parts
    [InlineData(1 * 1024 * 1024, null, 1)]              // 1 MB, rounds up to 1 part
    public async Task InitiateMultipartUploadAsync_CalculatesPartCountCorrectly(
        long fileSize, int? suggestedPartSize, int expectedPartCount)
    {
        var request = new InitiateMultipartRequest
        {
            FileName = "file.bin",
            FileSize = fileSize,
            MimeType = "application/octet-stream",
            SuggestedPartSizeBytes = suggestedPartSize,
        };

        const string compoundId = "key|aws-id";
        _s3ServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(compoundId);
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedPartUrlsAsync(compoundId, expectedPartCount, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new List<string>(new string[expectedPartCount]));

        var response = await _sut.InitiateMultipartUploadAsync(request, Guid.NewGuid());

        response.PresignedUrls.Should().HaveCount(expectedPartCount);
    }

    [Fact]
    public async Task InitiateMultipartUploadAsync_EnforcesMinimumPartSize()
    {
        // Suggested part size below 5 MB minimum should be clamped to 5 MB
        // 20 MB file with suggested 2 MB parts → clamped to 5 MB → 4 parts
        var request = new InitiateMultipartRequest
        {
            FileName = "file.bin",
            FileSize = 20 * 1024 * 1024,
            MimeType = "application/octet-stream",
            SuggestedPartSizeBytes = 2 * 1024 * 1024, // 2 MB (below minimum)
        };

        const string compoundId = "key|aws-id";
        _s3ServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(compoundId);

        int capturedPartCount = 0;
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedPartUrlsAsync(compoundId, It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Callback<string, int, TimeSpan>((_, count, _) => capturedPartCount = count)
            .ReturnsAsync(new List<string>());

        await _sut.InitiateMultipartUploadAsync(request, Guid.NewGuid());

        capturedPartCount.Should().Be(4); // 20 MB / 5 MB (minimum) = 4 parts
    }

    [Fact]
    public async Task InitiateMultipartUploadAsync_ExtractsS3KeyFromCompoundId()
    {
        var request = new InitiateMultipartRequest
        {
            FileName = "video.mp4",
            FileSize = 10 * 1024 * 1024,
            MimeType = "video/mp4",
        };

        const string expectedS3Key = "abc123.mp4";
        const string compoundId = $"{expectedS3Key}|aws-upload-id-xyz";

        _s3ServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(compoundId);
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedPartUrlsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(new List<string> { "https://part1" });

        FileMetadata? savedMetadata = null;
        _fileMetadataRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(m => savedMetadata = m)
            .Returns(Task.CompletedTask);

        await _sut.InitiateMultipartUploadAsync(request, Guid.NewGuid());

        savedMetadata!.S3Key.Should().Be(expectedS3Key);
    }

    [Fact]
    public async Task InitiateMultipartUploadAsync_CreatesUploadSession()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateMultipartRequest
        {
            FileName = "archive.zip",
            FileSize = 15 * 1024 * 1024,
            MimeType = "application/zip",
        };

        const string compoundId = "key.zip|aws-id";
        _s3ServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(compoundId);
        _s3ServiceMock
            .Setup(s => s.GeneratePresignedPartUrlsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(new List<string> { "url1", "url2" });

        UploadSession? savedSession = null;
        _uploadSessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UploadSession>()))
            .Callback<UploadSession>(s => savedSession = s)
            .Returns(Task.CompletedTask);

        await _sut.InitiateMultipartUploadAsync(request, userId);

        savedSession.Should().NotBeNull();
        savedSession!.UserId.Should().Be(userId);
        savedSession.FileName.Should().Be("archive.zip");
        savedSession.MultipartUploadId.Should().Be(compoundId);
        savedSession.Status.Should().Be(UploadSessionStatus.InProgress);
    }

    // -------------------------------------------------------------------------
    // CompleteMultipartUploadAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteMultipartUploadAsync_HappyPath_UpdatesFileMetadataToCompleted()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string compoundId = "final-key.mp4|aws-upload-id";

        var session = new UploadSession
        {
            Id = sessionId,
            UserId = userId,
            FileName = "video.mp4",
            MultipartUploadId = compoundId,
            Status = UploadSessionStatus.InProgress,
        };

        var existingMetadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            FileName = "video.mp4",
            S3Key = "final-key.mp4",
            Status = FileStatus.Uploading,
            UploadedByUserId = userId,
        };

        var s3Result = new S3UploadResult
        {
            S3Key = "final-key.mp4",
            S3Bucket = "my-bucket",
            FileSizeBytes = 50_000_000,
            ContentHash = "abc123hash",
            UploadedAt = DateTime.UtcNow,
        };

        _uploadSessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _fileMetadataRepoMock
            .Setup(r => r.GetByS3KeyAsync("final-key.mp4"))
            .ReturnsAsync(existingMetadata);
        _s3ServiceMock
            .Setup(s => s.CompleteMultipartUploadAsync(compoundId, It.IsAny<List<Application.Services.PartETag>>()))
            .ReturnsAsync(s3Result);

        var request = new CompleteMultipartRequest
        {
            Parts = new List<AppPartETag>
            {
                new() { PartNumber = 1, ETag = "etag1" },
                new() { PartNumber = 2, ETag = "etag2" },
            },
        };

        var result = await _sut.CompleteMultipartUploadAsync(sessionId.ToString(), request, userId);

        result.Status.Should().Be(FileStatus.Completed);
        result.S3Bucket.Should().Be("my-bucket");
        result.FileSizeBytes.Should().Be(50_000_000);
        existingMetadata.Status.Should().Be(FileStatus.Completed);
        session.Status.Should().Be(UploadSessionStatus.Completed);
    }

    [Fact]
    public async Task CompleteMultipartUploadAsync_WithInvalidGuidUploadId_ThrowsArgumentException()
    {
        var act = () => _sut.CompleteMultipartUploadAsync(
            "not-a-guid", new CompleteMultipartRequest(), Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid uploadId format*");
    }

    [Fact]
    public async Task CompleteMultipartUploadAsync_WithNonExistentSession_ThrowsInvalidOperationException()
    {
        var sessionId = Guid.NewGuid();
        _uploadSessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync((UploadSession?)null);

        var act = () => _sut.CompleteMultipartUploadAsync(
            sessionId.ToString(), new CompleteMultipartRequest(), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{sessionId}'*not found*");
    }

    [Fact]
    public async Task CompleteMultipartUploadAsync_WithDifferentUser_ThrowsUnauthorizedAccessException()
    {
        var sessionId = Guid.NewGuid();
        var sessionOwner = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        _uploadSessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(new UploadSession { Id = sessionId, UserId = sessionOwner });

        var act = () => _sut.CompleteMultipartUploadAsync(
            sessionId.ToString(), new CompleteMultipartRequest(), otherUser);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CompleteMultipartUploadAsync_WhenMetadataNotFound_ThrowsInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        const string compoundId = "missing-key.mp4|aws-id";

        _uploadSessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(new UploadSession
            {
                Id = sessionId,
                UserId = userId,
                MultipartUploadId = compoundId,
            });

        _s3ServiceMock
            .Setup(s => s.CompleteMultipartUploadAsync(It.IsAny<string>(), It.IsAny<List<Application.Services.PartETag>>()))
            .ReturnsAsync(new S3UploadResult());

        _fileMetadataRepoMock
            .Setup(r => r.GetByS3KeyAsync("missing-key.mp4"))
            .ReturnsAsync((FileMetadata?)null);

        var act = () => _sut.CompleteMultipartUploadAsync(
            sessionId.ToString(), new CompleteMultipartRequest(), userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*File metadata for upload session*not found*");
    }

    // -------------------------------------------------------------------------
    // UploadFileDirectAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadFileDirectAsync_HappyPath_ReturnsCompletedDto()
    {
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream("data"u8.ToArray());

        var s3Result = new S3UploadResult
        {
            S3Key = "uuid.txt",
            S3Bucket = "bucket",
            FileSizeBytes = 4,
            ContentHash = "hash",
            UploadedAt = DateTime.UtcNow,
        };

        _s3ServiceMock
            .Setup(s => s.UploadFileAsync("notes.txt", stream, "application/octet-stream"))
            .ReturnsAsync(s3Result);

        FileMetadata? saved = null;
        _fileMetadataRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FileMetadata>()))
            .Callback<FileMetadata>(m => saved = m)
            .Returns(Task.CompletedTask);

        var result = await _sut.UploadFileDirectAsync("notes.txt", stream, userId);

        result.Status.Should().Be(FileStatus.Completed);
        result.S3Key.Should().Be("uuid.txt");
        result.UploadedByUserId.Should().Be(userId);
        saved!.Status.Should().Be(FileStatus.Completed);
        saved.AccessLevel.Should().Be(FileAccessLevel.Private);
    }

    // -------------------------------------------------------------------------
    // GetFileMetadataAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFileMetadataAsync_ExistingFile_ReturnsMappedDto()
    {
        var fileId = Guid.NewGuid();
        var metadata = new FileMetadata
        {
            Id = fileId,
            FileName = "doc.pdf",
            S3Key = "key.pdf",
            S3Bucket = "bucket",
            MimeType = "application/pdf",
            FileSizeBytes = 1000,
            Status = FileStatus.Completed,
            AccessLevel = FileAccessLevel.Private,
        };

        _fileMetadataRepoMock
            .Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(metadata);

        var result = await _sut.GetFileMetadataAsync(fileId);

        result.Id.Should().Be(fileId);
        result.FileName.Should().Be("doc.pdf");
        result.Status.Should().Be(FileStatus.Completed);
    }

    [Fact]
    public async Task GetFileMetadataAsync_FileNotFound_ThrowsInvalidOperationException()
    {
        var fileId = Guid.NewGuid();
        _fileMetadataRepoMock
            .Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync((FileMetadata?)null);

        var act = () => _sut.GetFileMetadataAsync(fileId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{fileId}'*not found*");
    }

    // -------------------------------------------------------------------------
    // DeleteFileAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteFileAsync_HappyPath_DeletesS3ObjectAndSoftDeletesMetadata()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var metadata = new FileMetadata
        {
            Id = fileId,
            S3Key = "stored-key.jpg",
            UploadedByUserId = userId,
            Status = FileStatus.Completed,
        };

        _fileMetadataRepoMock.Setup(r => r.GetByIdAsync(fileId)).ReturnsAsync(metadata);

        await _sut.DeleteFileAsync(fileId, userId);

        _s3ServiceMock.Verify(s => s.DeleteFileAsync("stored-key.jpg"), Times.Once);
        metadata.Status.Should().Be(FileStatus.Deleted);
        metadata.UpdatedAt.Should().NotBeNull();
        _fileMetadataRepoMock.Verify(r => r.UpdateAsync(metadata), Times.Once);
        _fileMetadataRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenFileNotFound_ThrowsInvalidOperationException()
    {
        var fileId = Guid.NewGuid();
        _fileMetadataRepoMock
            .Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync((FileMetadata?)null);

        var act = () => _sut.DeleteFileAsync(fileId, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{fileId}'*not found*");
    }

    [Fact]
    public async Task DeleteFileAsync_WhenUserNotOwner_ThrowsUnauthorizedAccessException()
    {
        var fileId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _fileMetadataRepoMock
            .Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(new FileMetadata { Id = fileId, UploadedByUserId = ownerUserId, S3Key = "k" });

        var act = () => _sut.DeleteFileAsync(fileId, otherUserId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _s3ServiceMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenS3KeyIsEmpty_SkipsS3Deletion()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        _fileMetadataRepoMock
            .Setup(r => r.GetByIdAsync(fileId))
            .ReturnsAsync(new FileMetadata { Id = fileId, S3Key = string.Empty, UploadedByUserId = userId });

        await _sut.DeleteFileAsync(fileId, userId);

        _s3ServiceMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }
}
