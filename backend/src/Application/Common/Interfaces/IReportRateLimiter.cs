namespace Application.Common.Interfaces;

public interface IReportRateLimiter
{
    /// <summary>Returns true if the user may file another report now (and counts it); false if over the limit.</summary>
    Task<bool> TryConsumeAsync(Guid userId, CancellationToken cancellationToken = default);
}
