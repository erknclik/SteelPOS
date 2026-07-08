using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Auth.Commands;
using SanalPOS.Application.Auth.Dtos;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    public sealed record LoginRequest(string UserName, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new LoginCommand(request.UserName, request.Password), ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new RefreshTokenCommand(request.RefreshToken), ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        DateTime? expiresAt = long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
            : null;

        await _sender.Send(new LogoutCommand(request.RefreshToken, jti, expiresAt), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}
