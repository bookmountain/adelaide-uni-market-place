using Application.Common.Interfaces;
using Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.TestDoubles;

public static class CreateThreadPostTestsFakes
{
    public sealed class FakeStorage : IObjectStorageService
    {
        public List<string> Keys { get; } = new();
        public Task<ObjectStorageUploadResult> UploadAsync(string prefix, Stream content, string fileName, string contentType, CancellationToken ct = default)
        {
            var key = $"{prefix}/{fileName}";
            Keys.Add(key);
            return Task.FromResult(new ObjectStorageUploadResult(key, $"https://cdn/{key}"));
        }
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"https://cdn/{key}";
    }

    // Minimal ISender that resolves GetOrCreateAnonHandleCommand by assigning a fixed handle.
    public sealed class FakeSender : ISender
    {
        private readonly MarketplaceDbContext _db;
        public FakeSender(MarketplaceDbContext db) => _db = db;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            dynamic r = request;
            Guid userId = r.UserId;
            var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(user.AnonHandle)) user.AssignAnonHandle("quiet-koala-4821");
            await _db.SaveChangesAsync(ct);
            return (TResponse)(object)user.AnonHandle!;
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
