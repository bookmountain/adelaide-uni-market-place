using Contracts.DTO.Users;
using MediatR;

namespace Application.Users.Commands.CreateReview;

public sealed record CreateReviewCommand(
    Guid ReviewerId,
    Guid TargetUserId,
    Guid? OrderId,
    int Rating,
    string Comment) : IRequest<ReviewResponse>;
