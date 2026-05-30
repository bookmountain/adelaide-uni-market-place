using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadPost;

public sealed record GetThreadPostQuery(Guid PostId) : IRequest<ThreadPostDetailResponse?>;
