using Application.Auth.Commands.ActivateUser;
using Application.Auth.Commands.AuthenticateUser;
using Application.Auth.Commands.Logout;
using Application.Auth.Commands.LogoutAll;
using Application.Auth.Commands.RefreshToken;
using Application.Auth.Commands.RegisterUser;
using Application.Auth.Commands.ResendActivationEmail;
using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using Infrastructure.Configuration.Options;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly AuthOptions _options;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthController(
        ISender sender,
        IOptions<AuthOptions> options,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore)
    {
        _sender = sender;
        _options = options.Value;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            request.AvatarUrl,
            request.Department,
            request.Degree,
            request.Sex,
            request.Nationality,
            request.Age,
            _options.AllowedEmailDomain,
            _options.ActivationBaseUrl);

        try
        {
            return Ok(await _sender.Send(command, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("resend-activation")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sender.Send(new ResendActivationEmailCommand(request.Email, _options.ActivationBaseUrl), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _sender.Send(new AuthenticateUserCommand(request.Email, request.Password, ip), cancellationToken);

        if (result.IsRateLimited)
        {
            Response.Headers.RetryAfter = (_options.LoginFailureWindowMinutes * 60).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many failed login attempts. Try again later." });
        }

        if (result.User is null)
        {
            return Unauthorized(new { error = "Invalid credentials or account inactive." });
        }

        return Ok(await IssueAuthResponse(result.User, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("activate")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Activation token is required." });
        }

        try
        {
            var user = await _sender.Send(new ActivateUserCommand(token), cancellationToken);
            if (user is null)
            {
                return NotFound(new { error = "Activation token invalid." });
            }

            return Ok(await IssueAuthResponse(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        await _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
        return Ok(await IssueAuthResponse(user, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new LogoutAllCommand(userId), cancellationToken);
        return NoContent();
    }

    private async Task<AuthResponse> IssueAuthResponse(AuthUserDto user, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.IssueAccessToken(user.UserId, user.Email, user.Role);
        var refreshToken = _tokenService.GenerateRefreshToken();
        await _refreshTokenStore.StoreAsync(
            user.UserId, refreshToken, TimeSpan.FromDays(_options.RefreshTokenDays), cancellationToken);
        return new AuthResponse(accessToken, refreshToken, user);
    }
}
