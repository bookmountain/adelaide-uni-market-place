using Api.Auth;
using Application.Auth.Commands.ActivateUser;
using Application.Auth.Commands.AuthenticateUser;
using Application.Auth.Commands.RegisterUser;
using Application.Auth.Commands.ResendActivationEmail;
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

    public AuthController(ISender sender, IOptions<AuthOptions> options)
    {
        _sender = sender;
        _options = options.Value;
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
            var response = await _sender.Send(command, cancellationToken);
            return Ok(response);
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
        var command = new ResendActivationEmailCommand(request.Email, _options.ActivationBaseUrl);

        try
        {
            var response = await _sender.Send(command, cancellationToken);
            return Ok(response);
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
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _sender.Send(new AuthenticateUserCommand(request.Email, request.Password), cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid credentials or account inactive." });
        }

        var token = IssueToken(user);
        return Ok(new AuthResponse(token, user));
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

            var jwt = IssueToken(user);
            return Ok(new AuthResponse(jwt, user));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private string IssueToken(AuthUserDto user)
    {
        return AppJwt.Issue(
            issuer: _options.AppJwtIssuer,
            signingKey: _options.AppJwtSigningKey,
            userId: user.UserId.ToString(),
            email: user.Email,
            role: user.Role);
    }
}
