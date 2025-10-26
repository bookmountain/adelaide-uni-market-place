using Application.Common.Interfaces;
using Contracts.DTO.Categories;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Categories.Queries;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryResponse>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCategoriesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<CategoryResponse>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ProjectToType<CategoryResponse>()
            .ToListAsync(cancellationToken);

        return categories;
    }
}
