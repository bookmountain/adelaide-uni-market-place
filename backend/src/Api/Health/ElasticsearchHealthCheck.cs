using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Health;

/// <summary>
/// Custom health check that pings the Elasticsearch cluster.
/// Uses the already-registered <see cref="ElasticsearchClient"/> singleton —
/// no extra NuGet package required.
/// </summary>
public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly ElasticsearchClient _client;

    public ElasticsearchHealthCheck(ElasticsearchClient client)
        => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            return response.IsValidResponse
                ? HealthCheckResult.Healthy("Elasticsearch is reachable.")
                : HealthCheckResult.Unhealthy($"Elasticsearch ping returned an invalid response: {response.ElasticsearchServerError?.Error?.Reason}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch ping threw an exception.", ex);
        }
    }
}
