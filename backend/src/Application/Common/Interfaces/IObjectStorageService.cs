namespace Application.Common.Interfaces;

public interface IObjectStorageService
{
    Task<ObjectStorageUploadResult> UploadAsync(string prefix, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    string GetPublicUrl(string key);
}

public sealed record ObjectStorageUploadResult(string Key, string Url);
