using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadComments;

public sealed record GetThreadCommentsQuery(Guid PostId) : IRequest<IReadOnlyList<ThreadCommentResponse>>;
