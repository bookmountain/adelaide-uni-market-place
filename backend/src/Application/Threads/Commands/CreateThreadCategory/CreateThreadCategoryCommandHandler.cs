using Application.Common.Interfaces;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed class CreateThreadCategoryCommandHandler : IRequestHandler<CreateThreadCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    public CreateThreadCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateThreadCategoryCommand request, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.ThreadCategories.AnyAsync(c => c.Slug == slug, ct))
        {
            throw new InvalidOperationException($"A category with slug '{slug}' already exists.");
        }

        var category = new ThreadCategory(Guid.NewGuid(), slug, request.Name, request.Description, request.IconKey, request.SortOrder);
        _db.ThreadCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category.Id;
    }
}
