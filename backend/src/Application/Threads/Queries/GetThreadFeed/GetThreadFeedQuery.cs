using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadFeed;

public sealed record GetThreadFeedQuery(string? CategorySlug, string Sort, string? Cursor, int PageSize)
    : IRequest<ThreadFeedResponse>;
