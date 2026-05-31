using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Search;

/// <summary>Creates the 'threads' index with a mapping once, on startup.</summary>
public sealed class ThreadSearchIndexBootstrapper : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ThreadSearchIndexBootstrapper> _logger;

    public ThreadSearchIndexBootstrapper(IServiceProvider services, ILogger<ThreadSearchIndexBootstrapper> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
            var exists = await client.Indices.ExistsAsync(ElasticThreadSearchIndex.IndexName, ct);
            if (exists.Exists) return;

            await client.Indices.CreateAsync<ElasticThreadDocument>(ElasticThreadSearchIndex.IndexName, c => c
                .Mappings(m => m
                    .Properties(p => p
                        .Keyword(k => k.CategorySlug)
                        .Text(t => t.Title)
                        .Text(t => t.Body)
                        .IntegerNumber(i => i.LikeCount)
                        .IntegerNumber(i => i.CommentCount)
                        .DoubleNumber(d => d.HotRank)
                        .Date(dt => dt.CreatedAt)
                        .Date(dt => dt.LastActivityAt)
                    )
                ), ct);

            _logger.LogInformation("Created Elasticsearch index '{Index}'.", ElasticThreadSearchIndex.IndexName);
        }
        catch (Exception ex)
        {
            // Don't crash startup if ES is briefly unavailable; the indexer will still upsert and create the index lazily.
            _logger.LogWarning(ex, "Could not bootstrap Elasticsearch index; continuing.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
