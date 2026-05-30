using MediatR;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed record UpdateThreadCategoryCommand(
    Guid Id, string Name, string Description, string IconKey, int SortOrder, bool IsActive) : IRequest;
