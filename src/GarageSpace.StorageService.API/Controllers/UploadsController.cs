using GarageSpace.StorageService.Application.DTOs;
using GarageSpace.StorageService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GarageSpace.StorageService.API.Controllers;

[ApiController]
[Route("api")]
public class UploadsController : ControllerBase
{
    private readonly IUploadService _uploadService;

    public UploadsController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    // -------------------------------------------------------------------------
    // Presigned upload
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initiates a presigned URL upload. The caller uploads the file directly to S3 using the
    /// returned URL.
    /// </summary>
    [HttpPost("uploads/presigned")]
    [ProducesResponseType(typeof(InitiateUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePresignedUpload(
        [FromBody] InitiateUploadRequest request,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _uploadService.InitiatePresignedUploadAsync(request, userId);
        return Ok(response);
    }

    // -------------------------------------------------------------------------
    // Multipart upload
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initiates a multipart upload. Returns presigned URLs for each part so the caller can
    /// upload chunks directly to S3.
    /// </summary>
    [HttpPost("uploads/multipart")]
    [ProducesResponseType(typeof(InitiateMultipartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateMultipartUpload(
        [FromBody] InitiateMultipartRequest request,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _uploadService.InitiateMultipartUploadAsync(request, userId);
        return Ok(response);
    }

    /// <summary>
    /// Completes a multipart upload. The caller provides the ETags for each uploaded part.
    /// </summary>
    [HttpPost("uploads/{uploadId}/complete")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteMultipartUpload(
        [FromRoute] string uploadId,
        [FromBody] CompleteMultipartRequest request,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _uploadService.CompleteMultipartUploadAsync(uploadId, request, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    // -------------------------------------------------------------------------
    // Direct upload
    // -------------------------------------------------------------------------

    /// <summary>
    /// Uploads a file directly via the service (streamed through the API).
    /// </summary>
    [HttpPost("uploads/direct")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB limit for direct uploads
    public async Task<IActionResult> UploadFileDirect(
        IFormFile file,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        await using var stream = file.OpenReadStream();
        var result = await _uploadService.UploadFileDirectAsync(file.FileName, stream, userId);
        return CreatedAtAction(nameof(GetFileMetadata), new { fileId = result.Id }, result);
    }

    // -------------------------------------------------------------------------
    // File metadata & deletion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns metadata for a previously uploaded file.
    /// </summary>
    [HttpGet("files/{fileId:guid}")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileMetadata([FromRoute] Guid fileId)
    {
        try
        {
            var result = await _uploadService.GetFileMetadataAsync(fileId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Soft-deletes a file (marks it as Deleted and removes the S3 object).
    /// </summary>
    [HttpDelete("files/{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        [FromRoute] Guid fileId,
        [FromHeader(Name = "X-User-Id")] Guid userId)
    {
        try
        {
            await _uploadService.DeleteFileAsync(fileId, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
