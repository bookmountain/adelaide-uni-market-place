using MediatR;

namespace Application.Users.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, string? Bio, bool AppearInDrawPool) : IRequest;
