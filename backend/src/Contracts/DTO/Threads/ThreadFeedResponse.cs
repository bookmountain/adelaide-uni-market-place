namespace Contracts.DTO.Threads;

public sealed record ThreadFeedResponse(IReadOnlyList<ThreadPostSummary> Items, string? NextCursor);
