using FluentAssertions;
using GarageSpace.StorageService.Application.Services;
using GarageSpace.StorageService.Infrastructure.Services;

namespace GarageSpace.StorageService.UnitTests.Services;

public class FileValidationServiceTests
{
    private readonly FileValidationService _sut = new();

    // -------------------------------------------------------------------------
    // File name validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidateFileAsync_WithValidInputs_ReturnsValidResult()
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "document.pdf",
            fileSize: 1024,
            mimeType: "application/pdf",
            fileStream: Stream.Null);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateFileAsync_WithEmptyOrWhitespaceFileName_AddsError(string fileName)
    {
        var result = await _sut.ValidateFileAsync(
            fileName: fileName,
            fileSize: 1024,
            mimeType: "application/pdf",
            fileStream: Stream.Null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e == "File name is required.");
    }

    // -------------------------------------------------------------------------
    // File size validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidateFileAsync_WithNonPositiveFileSize_AddsError(long fileSize)
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "file.pdf",
            fileSize: fileSize,
            mimeType: "application/pdf",
            fileStream: Stream.Null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e == "File size must be greater than zero.");
    }

    [Fact]
    public async Task ValidateFileAsync_WithFileSizeExceedingMaximum_AddsError()
    {
        const long overMaxSize = 5L * 1024 * 1024 * 1024 + 1; // 5 GB + 1 byte

        var result = await _sut.ValidateFileAsync(
            fileName: "huge.pdf",
            fileSize: overMaxSize,
            mimeType: "application/pdf",
            fileStream: Stream.Null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("exceeds the maximum allowed size"));
    }

    [Fact]
    public async Task ValidateFileAsync_WithFileSizeAtExactMaximum_IsValid()
    {
        const long exactMaxSize = 5L * 1024 * 1024 * 1024; // 5 GB

        var result = await _sut.ValidateFileAsync(
            fileName: "exactly-max.pdf",
            fileSize: exactMaxSize,
            mimeType: "application/pdf",
            fileStream: Stream.Null);

        result.Errors.Should().NotContain(e => e.Contains("exceeds the maximum allowed size"));
    }

    // -------------------------------------------------------------------------
    // MIME type validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("text/plain")]
    [InlineData("application/zip")]
    [InlineData("application/octet-stream")]
    public async Task ValidateFileAsync_WithAllowedMimeType_DoesNotAddMimeTypeError(string mimeType)
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "file.bin",
            fileSize: 1024,
            mimeType: mimeType,
            fileStream: Stream.Null);

        result.Errors.Should().NotContain(e => e.Contains("is not allowed"));
    }

    [Theory]
    [InlineData("application/x-executable")]
    [InlineData("application/x-msdownload")]
    [InlineData("text/html")]
    [InlineData("unknown/type")]
    [InlineData("")]
    public async Task ValidateFileAsync_WithDisallowedMimeType_AddsError(string mimeType)
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "file.bin",
            fileSize: 1024,
            mimeType: mimeType,
            fileStream: Stream.Null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("is not allowed"));
    }

    [Fact]
    public async Task ValidateFileAsync_MimeTypeComparison_IsCaseInsensitive()
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "image.jpg",
            fileSize: 1024,
            mimeType: "IMAGE/JPEG",
            fileStream: Stream.Null);

        result.Errors.Should().NotContain(e => e.Contains("is not allowed"));
    }

    // -------------------------------------------------------------------------
    // Content hash computation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidateFileAsync_WithReadableStream_ComputesContentHash()
    {
        var bytes = "hello world"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await _sut.ValidateFileAsync(
            fileName: "test.txt",
            fileSize: bytes.Length,
            mimeType: "text/plain",
            fileStream: stream);

        result.ContentHash.Should().NotBeNullOrEmpty();
        result.ContentHash.Should().HaveLength(64); // SHA-256 hex string length
        result.ContentHash.Should().MatchRegex("^[0-9a-f]+$"); // lowercase hex
    }

    [Fact]
    public async Task ValidateFileAsync_SameContent_ProducesSameHash()
    {
        var bytes = "deterministic content"u8.ToArray();
        using var stream1 = new MemoryStream(bytes);
        using var stream2 = new MemoryStream(bytes);

        var result1 = await _sut.ValidateFileAsync("f.txt", bytes.Length, "text/plain", stream1);
        var result2 = await _sut.ValidateFileAsync("f.txt", bytes.Length, "text/plain", stream2);

        result1.ContentHash.Should().Be(result2.ContentHash);
    }

    [Fact]
    public async Task ValidateFileAsync_WithNullStream_DoesNotComputeHash()
    {
        var result = await _sut.ValidateFileAsync(
            fileName: "file.txt",
            fileSize: 1024,
            mimeType: "text/plain",
            fileStream: Stream.Null);

        result.ContentHash.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Multiple errors
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidateFileAsync_WithMultipleInvalidFields_CollectsAllErrors()
    {
        var result = await _sut.ValidateFileAsync(
            fileName: string.Empty,
            fileSize: 0,
            mimeType: "application/x-executable",
            fileStream: Stream.Null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }
}
