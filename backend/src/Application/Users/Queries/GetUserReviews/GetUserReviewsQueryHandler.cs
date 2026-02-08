using Application.Common.Interfaces;
using Contracts.DTO.Users;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.Queries.GetUserReviews;

public sealed class GetUserReviewsQueryHandler : IRequestHandler<GetUserReviewsQuery, List<ReviewResponse>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetUserReviewsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ReviewResponse>> Handle(GetUserReviewsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.TargetUserId == request.UserId)
            .Include(r => r.Reviewer)
            .Include(r => r.TargetUser)
            .ProjectToType<ReviewResponse>()
            .ToListAsync(cancellationToken);
    }
}
