using FluentAssertions;
using GarageSpace.StorageService.API.Controllers;
using GarageSpace.StorageService.Application.DTOs;
using GarageSpace.StorageService.Application.Services;
using GarageSpace.StorageService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AppPartETag = GarageSpace.StorageService.Application.DTOs.PartETag;

namespace GarageSpace.StorageService.UnitTests.Controllers;

public class UploadsControllerTests
{
    private readonly Mock<IUploadService> _uploadServiceMock = new();
    private readonly UploadsController _sut;

    public UploadsControllerTests()
    {
        _sut = new UploadsController(_uploadServiceMock.Object);
    }

    private static FileMetadataDto SampleFileMetadataDto(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FileName = "sample.pdf",
        S3Key = "key.pdf",
        S3Bucket = "bucket",
        MimeType = "application/pdf",
        FileSizeBytes = 1024,
        Status = FileStatus.Completed,
        AccessLevel = FileAccessLevel.Private,
        UploadedByUserId = Guid.NewGuid(),
        UploadedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    // -------------------------------------------------------------------------
    // InitiatePresignedUpload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InitiatePresignedUpload_WhenServiceSucceeds_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateUploadRequest { FileName = "photo.jpg", FileSize = 512, MimeType = "image/jpeg" };
        var expected = new InitiateUploadResponse
        {
            FileId = Guid.NewGuid(),
            PresignedUrl = "https://s3/presigned",
            PresignedUrlExpirationSeconds = 3600,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        _uploadServiceMock
            .Setup(s => s.InitiatePresignedUploadAsync(request, userId))
            .ReturnsAsync(expected);

        var result = await _sut.InitiatePresignedUpload(request, userId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task InitiatePresignedUpload_WhenModelStateInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FileName", "Required");

        var result = await _sut.InitiatePresignedUpload(new InitiateUploadRequest(), Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
        _uploadServiceMock.Verify(
            s => s.InitiatePresignedUploadAsync(It.IsAny<InitiateUploadRequest>(), It.IsAny<Guid>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // InitiateMultipartUpload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InitiateMultipartUpload_WhenServiceSucceeds_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var request = new InitiateMultipartRequest { FileName = "video.mp4", FileSize = 100_000_000, MimeType = "video/mp4" };
        var expected = new InitiateMultipartResponse
        {
            UploadId = Guid.NewGuid().ToString(),
            FileId = Guid.NewGuid(),
            PresignedUrls = new List<string> { "url1", "url2" },
            ExpirationSeconds = 7200,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
        };

        _uploadServiceMock
            .Setup(s => s.InitiateMultipartUploadAsync(request, userId))
            .ReturnsAsync(expected);

        var result = await _sut.InitiateMultipartUpload(request, userId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task InitiateMultipartUpload_WhenModelStateInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("FileName", "Required");

        var result = await _sut.InitiateMultipartUpload(new InitiateMultipartRequest(), Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // -------------------------------------------------------------------------
    // CompleteMultipartUpload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteMultipartUpload_WhenServiceSucceeds_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var uploadId = Guid.NewGuid().ToString();
        var request = new CompleteMultipartRequest
        {
            Parts = new List<AppPartETag> { new() { PartNumber = 1, ETag = "etag1" } },
        };
        var expected = SampleFileMetadataDto();

        _uploadServiceMock
            .Setup(s => s.CompleteMultipartUploadAsync(uploadId, request, userId))
            .ReturnsAsync(expected);

        var result = await _sut.CompleteMultipartUpload(uploadId, request, userId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CompleteMultipartUpload_WhenSessionNotFound_ReturnsNotFound()
    {
        var uploadId = Guid.NewGuid().ToString();
        _uploadServiceMock
            .Setup(s => s.CompleteMultipartUploadAsync(uploadId, It.IsAny<CompleteMultipartRequest>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Upload session not found."));

        var result = await _sut.CompleteMultipartUpload(uploadId, new CompleteMultipartRequest(), Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CompleteMultipartUpload_WhenUserNotAuthorized_ReturnsForbidden()
    {
        var uploadId = Guid.NewGuid().ToString();
        _uploadServiceMock
            .Setup(s => s.CompleteMultipartUploadAsync(uploadId, It.IsAny<CompleteMultipartRequest>(), It.IsAny<Guid>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied."));

        var result = await _sut.CompleteMultipartUpload(uploadId, new CompleteMultipartRequest(), Guid.NewGuid());

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CompleteMultipartUpload_WhenModelStateInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("Parts", "Required");

        var result = await _sut.CompleteMultipartUpload("some-id", new CompleteMultipartRequest(), Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // -------------------------------------------------------------------------
    // UploadFileDirect
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadFileDirect_WhenFileIsNull_ReturnsBadRequest()
    {
        var result = await _sut.UploadFileDirect(null!, Guid.NewGuid());

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "No file provided." });
    }

    [Fact]
    public async Task UploadFileDirect_WhenFileIsEmpty_ReturnsBadRequest()
    {
        var emptyFile = new Mock<IFormFile>();
        emptyFile.Setup(f => f.Length).Returns(0);
        emptyFile.Setup(f => f.FileName).Returns("empty.txt");

        var result = await _sut.UploadFileDirect(emptyFile.Object, Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadFileDirect_WhenFileProvided_ReturnsCreatedWithLocation()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var expected = SampleFileMetadataDto(fileId);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("data.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream("content"u8.ToArray()));

        _uploadServiceMock
            .Setup(s => s.UploadFileDirectAsync("data.pdf", It.IsAny<Stream>(), userId))
            .ReturnsAsync(expected);

        var result = await _sut.UploadFileDirect(fileMock.Object, userId);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(UploadsController.GetFileMetadata));
        createdResult.RouteValues.Should().ContainKey("fileId").WhoseValue.Should().Be(fileId);
        createdResult.Value.Should().BeEquivalentTo(expected);
    }

    // -------------------------------------------------------------------------
    // GetFileMetadata
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFileMetadata_WhenFileExists_ReturnsOk()
    {
        var fileId = Guid.NewGuid();
        var expected = SampleFileMetadataDto(fileId);

        _uploadServiceMock
            .Setup(s => s.GetFileMetadataAsync(fileId))
            .ReturnsAsync(expected);

        var result = await _sut.GetFileMetadata(fileId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetFileMetadata_WhenFileNotFound_ReturnsNotFound()
    {
        var fileId = Guid.NewGuid();
        _uploadServiceMock
            .Setup(s => s.GetFileMetadataAsync(fileId))
            .ThrowsAsync(new InvalidOperationException($"File '{fileId}' not found."));

        var result = await _sut.GetFileMetadata(fileId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // -------------------------------------------------------------------------
    // DeleteFile
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteFile_WhenSuccessful_ReturnsNoContent()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _uploadServiceMock
            .Setup(s => s.DeleteFileAsync(fileId, userId))
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteFile(fileId, userId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteFile_WhenFileNotFound_ReturnsNotFound()
    {
        var fileId = Guid.NewGuid();
        _uploadServiceMock
            .Setup(s => s.DeleteFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException($"File '{fileId}' not found."));

        var result = await _sut.DeleteFile(fileId, Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteFile_WhenUserNotAuthorized_ReturnsForbidden()
    {
        var fileId = Guid.NewGuid();
        _uploadServiceMock
            .Setup(s => s.DeleteFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not authorized."));

        var result = await _sut.DeleteFile(fileId, Guid.NewGuid());

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
