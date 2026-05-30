using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.Commands.GetOrCreateAnonHandle;

public sealed class GetOrCreateAnonHandleCommandHandler : IRequestHandler<GetOrCreateAnonHandleCommand, string>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _dbContext;
    private readonly IAnonHandleGenerator _generator;

    public GetOrCreateAnonHandleCommandHandler(IApplicationDbContext dbContext, IAnonHandleGenerator generator)
    {
        _dbContext = dbContext;
        _generator = generator;
    }

    public async Task<string> Handle(GetOrCreateAnonHandleCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!string.IsNullOrWhiteSpace(user.AnonHandle))
        {
            return user.AnonHandle;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = _generator.Generate();
            var taken = await _dbContext.Users.AnyAsync(u => u.AnonHandle == candidate, cancellationToken);
            if (taken)
            {
                continue;
            }

            user.AssignAnonHandle(candidate);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return candidate;
        }

        throw new InvalidOperationException("Could not allocate a unique anonymous handle after multiple attempts.");
    }
}
