using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api.Models;

public sealed class CreateThreadPostRequest
{
    [Required] public Guid CategoryId { get; init; }
    [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
    [Required] public string Body { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public List<IFormFile>? Images { get; init; }
}
