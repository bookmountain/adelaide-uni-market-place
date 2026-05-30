using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed class UpdateThreadCategoryCommandHandler : IRequestHandler<UpdateThreadCategoryCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateThreadCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateThreadCategoryCommand request, CancellationToken ct)
    {
        var category = await _db.ThreadCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new InvalidOperationException("Category not found.");
        category.Update(request.Name, request.Description, request.IconKey, request.SortOrder, request.IsActive);
        await _db.SaveChangesAsync(ct);
    }
}
