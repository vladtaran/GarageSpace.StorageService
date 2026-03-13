using System.Security.Cryptography;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using GarageSpace.StorageService.Application.Services;
using Microsoft.Extensions.Options;

namespace GarageSpace.StorageService.Infrastructure.Services;

public class S3Service : IS3Service, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;

    public S3Service(IOptions<S3Options> options)
    {
        _options = options.Value;
        _s3Client = BuildClient(_options);
    }

    // Internal constructor for testing with a pre-built client.
    internal S3Service(IAmazonS3 s3Client, IOptions<S3Options> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    // -------------------------------------------------------------------------
    // Direct upload
    // -------------------------------------------------------------------------

    public async Task<S3UploadResult> UploadFileAsync(
        string fileName,
        Stream fileStream,
        string contentType)
    {
        var key = BuildKey(fileName);

        // Buffer stream so we can compute the hash and still upload it.
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer);
        buffer.Position = 0;

        var contentHash = await ComputeSha256Async(buffer);
        buffer.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = buffer,
            ContentType = contentType,
        };

        await _s3Client.PutObjectAsync(request);

        return new S3UploadResult
        {
            S3Key = key,
            S3Bucket = _options.BucketName,
            FileSizeBytes = buffer.Length,
            ContentHash = contentHash,
            UploadedAt = DateTime.UtcNow,
        };
    }

    // -------------------------------------------------------------------------
    // Multipart upload
    // -------------------------------------------------------------------------

    public async Task<string> InitiateMultipartUploadAsync(
        string fileName,
        string contentType)
    {
        var key = BuildKey(fileName);

        var request = new InitiateMultipartUploadRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            ContentType = contentType,
        };

        var response = await _s3Client.InitiateMultipartUploadAsync(request);

        // Encode both the S3 key and the AWS UploadId into a single string so that
        // downstream methods (GeneratePresignedPartUrlsAsync, CompleteMultipartUploadAsync)
        // can retrieve both without requiring an interface change.
        return $"{key}|{response.UploadId}";
    }

    public async Task<List<string>> GeneratePresignedPartUrlsAsync(
        string uploadId,
        int partCount,
        TimeSpan expiration)
    {
        var (s3Key, awsUploadId) = ParseUploadId(uploadId);

        var urls = new List<string>(partCount);
        var expiresAt = DateTime.UtcNow.Add(expiration);

        for (var partNumber = 1; partNumber <= partCount; partNumber++)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key,
                Verb = HttpVerb.PUT,
                Expires = expiresAt,
                UploadId = awsUploadId,
                PartNumber = partNumber,
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            urls.Add(url);
        }

        return urls;
    }

    public async Task<S3UploadResult> CompleteMultipartUploadAsync(
        string uploadId,
        List<GarageSpace.StorageService.Application.Services.PartETag> parts)
    {
        var (s3Key, awsUploadId) = ParseUploadId(uploadId);

        var partETags = parts
            .Select(p => new Amazon.S3.Model.PartETag(p.PartNumber, p.ETag))
            .ToList();

        var request = new CompleteMultipartUploadRequest
        {
            BucketName = _options.BucketName,
            Key = s3Key,
            UploadId = awsUploadId,
            PartETags = partETags,
        };

        var response = await _s3Client.CompleteMultipartUploadAsync(request);

        var metadata = await GetObjectMetadataAsync(response.Key);

        return new S3UploadResult
        {
            S3Key = response.Key,
            S3Bucket = response.BucketName,
            FileSizeBytes = metadata.Size,
            ContentHash = response.ETag.Trim('"'),
            UploadedAt = DateTime.UtcNow,
        };
    }

    // -------------------------------------------------------------------------
    // Presigned URLs
    // -------------------------------------------------------------------------

    public async Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration),
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    // -------------------------------------------------------------------------
    // Object management
    // -------------------------------------------------------------------------

    public async Task DeleteFileAsync(string key)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
        };

        await _s3Client.DeleteObjectAsync(request);
    }

    public async Task<S3ObjectMetadata> GetObjectMetadataAsync(string key)
    {
        var request = new GetObjectMetadataRequest
        {
            BucketName = _options.BucketName,
            Key = key,
        };

        var response = await _s3Client.GetObjectMetadataAsync(request);

        return new S3ObjectMetadata
        {
            Key = key,
            Size = response.ContentLength,
            ContentType = response.Headers.ContentType,
            LastModified = response.LastModified,
            ETag = response.ETag.Trim('"'),
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string BuildKey(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return $"{Guid.NewGuid()}{ext}";
    }

    /// <summary>
    /// Parses the compound upload-id string returned by <see cref="InitiateMultipartUploadAsync"/>
    /// which has the form <c>"{s3Key}|{awsUploadId}"</c>.
    /// </summary>
    private static (string s3Key, string awsUploadId) ParseUploadId(string compoundId)
    {
        var separator = compoundId.IndexOf('|');
        if (separator < 0)
            throw new ArgumentException(
                "Invalid uploadId format. Expected '{s3Key}|{awsUploadId}'.", nameof(compoundId));

        return (compoundId[..separator], compoundId[(separator + 1)..]);
    }

    private static async Task<string> ComputeSha256Async(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static IAmazonS3 BuildClient(S3Options options)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.ForcePathStyle = true;
        }

        if (!string.IsNullOrWhiteSpace(options.AccessKey) &&
            !string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
        }

        // Fall back to the default credential resolution chain (IAM role, env vars, etc.)
        return new AmazonS3Client(config);
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }
}
