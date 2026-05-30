using MediatR;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed record CreateThreadCategoryCommand(
    string Slug, string Name, string Description, string IconKey, int SortOrder) : IRequest<Guid>;
