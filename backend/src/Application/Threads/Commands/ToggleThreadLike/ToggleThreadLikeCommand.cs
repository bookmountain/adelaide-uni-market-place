using Contracts.DTO.Threads;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Threads.Commands.ToggleThreadLike;

public sealed record ToggleThreadLikeCommand(Guid UserId, ThreadLikeTarget Target, Guid TargetId)
    : IRequest<LikeResponse>;
