using Contracts.DTO.Users;
using MediatR;

namespace Application.Users.Queries.GetUserReviews;

public sealed record GetUserReviewsQuery(Guid UserId) : IRequest<List<ReviewResponse>>;
