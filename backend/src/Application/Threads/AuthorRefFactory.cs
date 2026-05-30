using Contracts.DTO.Threads;
using Domain.Entities.Users;

namespace Application.Threads;

public static class AuthorRefFactory
{
    public static AuthorRef Create(bool isAnonymous, User author)
    {
        if (isAnonymous)
        {
            return new AuthorRef(
                IsAnonymous: true,
                Handle: string.IsNullOrWhiteSpace(author.AnonHandle) ? "anonymous" : author.AnonHandle,
                UserId: null,
                DisplayName: null,
                AvatarUrl: null);
        }

        return new AuthorRef(
            IsAnonymous: false,
            Handle: null,
            UserId: author.Id,
            DisplayName: author.DisplayName,
            AvatarUrl: author.AvatarUrl);
    }
}
