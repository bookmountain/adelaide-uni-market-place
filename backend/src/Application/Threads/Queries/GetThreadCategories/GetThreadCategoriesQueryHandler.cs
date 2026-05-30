using Application.Common.Interfaces;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadCategories;

public sealed class GetThreadCategoriesQueryHandler
    : IRequestHandler<GetThreadCategoriesQuery, IReadOnlyList<ThreadCategoryResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetThreadCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ThreadCategoryResponse>> Handle(GetThreadCategoriesQuery request, CancellationToken ct)
    {
        return await _db.ThreadCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ThreadCategoryResponse(c.Id, c.Slug, c.Name, c.Description, c.IconKey, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
    }
}
