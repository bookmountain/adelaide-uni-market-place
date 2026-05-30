using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadCategories;

public sealed record GetThreadCategoriesQuery() : IRequest<IReadOnlyList<ThreadCategoryResponse>>;
