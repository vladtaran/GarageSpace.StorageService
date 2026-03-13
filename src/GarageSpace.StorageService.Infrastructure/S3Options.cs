namespace GarageSpace.StorageService.Infrastructure;

public class S3Options
{
    public const string SectionName = "S3";

    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>
    /// Optional custom endpoint URL (e.g. for LocalStack or MinIO).
    /// Leave empty to use the standard AWS endpoint for the configured region.
    /// </summary>
    public string? ServiceUrl { get; set; }
}
