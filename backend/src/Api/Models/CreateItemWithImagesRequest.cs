using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace Api.Models;

public sealed class CreateItemWithImagesRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required, MaxLength(160)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; init; }

    [Required]
    public List<IFormFile> Images { get; init; } = new();
}
