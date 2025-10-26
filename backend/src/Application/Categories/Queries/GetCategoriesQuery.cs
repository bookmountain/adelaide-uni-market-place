using Contracts.DTO.Categories;
using MediatR;

namespace Application.Categories.Queries;

public sealed record GetCategoriesQuery() : IRequest<IReadOnlyCollection<CategoryResponse>>;
