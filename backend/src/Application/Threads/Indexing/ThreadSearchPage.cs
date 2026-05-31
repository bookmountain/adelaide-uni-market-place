using Contracts.DTO.Threads;

namespace Application.Threads.Indexing;

public sealed record ThreadSearchPage(IReadOnlyList<ThreadPostSummary> Items, string? NextCursor);
