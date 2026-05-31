namespace Contracts.DTO.Moderation;

/// <summary>Admin-only author projection. Unlike the public AuthorRef, this ALWAYS carries real identity (the moderation anon-break).</summary>
public sealed record ModerationAuthor(Guid UserId, string DisplayName);
