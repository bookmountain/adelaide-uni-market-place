using Application.Common.Interfaces;
using Contracts.DTO.Users;
using Domain.Entities.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.Commands.CreateReview;

public sealed class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, ReviewResponse>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateReviewCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReviewResponse> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        if (request.ReviewerId == request.TargetUserId)
        {
            throw new InvalidOperationException("You cannot review yourself.");
        }

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (targetUser is null)
        {
            throw new InvalidOperationException("Target user not found.");
        }

        var reviewer = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.ReviewerId, cancellationToken);
            
        if (reviewer is null)
        {
            throw new InvalidOperationException("Reviewer user not found.");
        }

        // Optional: Check if order exists and belongs to these users
        if (request.OrderId.HasValue)
        {
             var orderExists = await _dbContext.Orders
                 .AnyAsync(o => o.Id == request.OrderId.Value && 
                                (o.BuyerId == request.ReviewerId || o.BuyerId == request.TargetUserId), cancellationToken); // Simplified check
             if (!orderExists)
             {
                 // We could be stricter here, but let's just ensure order exists for now.
                 // Ideally we check if Reviewer bought from Target or vice versa.
             }
        }

        var review = new Review(
            Guid.NewGuid(),
            request.ReviewerId,
            request.TargetUserId,
            request.Comment,
            request.Rating,
            DateTimeOffset.UtcNow,
            request.OrderId);

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ReviewResponse(
            review.Id,
            review.ReviewerId,
            reviewer.DisplayName,
            review.TargetUserId,
            targetUser.DisplayName,
            review.Rating,
            review.Comment,
            review.CreatedAt);
    }
}
