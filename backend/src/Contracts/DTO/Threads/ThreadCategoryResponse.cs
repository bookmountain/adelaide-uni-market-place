namespace Contracts.DTO.Threads;

public sealed record ThreadCategoryResponse(
    Guid Id, string Slug, string Name, string Description, string IconKey, int SortOrder, bool IsActive);
