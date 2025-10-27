using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

public sealed class R2ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;
    private readonly string _publicBaseUrl;
    private bool _disposed;

    public R2ObjectStorageService(IOptions<R2Options> options)
    {
        _options = options.Value;

        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
            ? $"https://{_options.AccountId}.r2.cloudflarestorage.com"
            : _options.Endpoint.TrimEnd('/');

        _publicBaseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{endpoint}/{_options.Bucket}"
            : _options.PublicBaseUrl.TrimEnd('/');

        var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task<ObjectStorageUploadResult> UploadAsync(string prefix, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var key = BuildKey(prefix, fileName, contentType);

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Headers = { CacheControl = "public, max-age=31536000" }
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        var url = GetPublicUrl(key);
        return new ObjectStorageUploadResult(key, url);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var request = new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);
    }

    public string GetPublicUrl(string key)
    {
        return $"{_publicBaseUrl}/{key}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _s3Client.Dispose();
        _disposed = true;
    }

    private static string BuildKey(string prefix, string fileName, string contentType)
    {
        var extension = GuessExtension(contentType);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GuessExtensionFromFileName(fileName);
        }
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            builder.Append(prefix.Trim('/'));
            builder.Append('/');
        }

        builder.Append(Guid.NewGuid().ToString("N"));
        if (!string.IsNullOrWhiteSpace(extension))
        {
            builder.Append('.');
            builder.Append(extension);
        }

        return builder.ToString();
    }

    private static string GuessExtension(string contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => "jpg",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/webp" => "webp",
            _ => string.Empty
        };
    }

    private static string GuessExtensionFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var lastDot = fileName.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= fileName.Length - 1)
        {
            return string.Empty;
        }

        return fileName[(lastDot + 1)..].ToLowerInvariant();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(R2ObjectStorageService));
        }
    }
}
