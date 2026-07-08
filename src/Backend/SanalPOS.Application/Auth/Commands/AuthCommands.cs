using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Auth.Dtos;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Auth.Commands;

// ---- Login ----

public sealed record LoginCommand(string UserName, string Password) : IRequest<LoginResultDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByUserNameAsync(request.UserName.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Kullanıcı adı veya parola hatalı.");

        if (user.IsLockedOut)
            throw new UnauthorizedException("Hesap geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.RegisterFailedLogin();
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogWarning("Başarısız giriş denemesi. UserName: {UserName}", user.UserName);
            throw new UnauthorizedException("Kullanıcı adı veya parola hatalı.");
        }

        user.RegisterSuccessfulLogin();

        var roles = user.Roles.Select(r => r.Role.Name).ToArray();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _refreshTokenRepository.AddAsync(new RefreshToken(
            user.Id, _tokenService.HashRefreshToken(refreshToken), DateTime.UtcNow.Add(RefreshTokenLifetime)), ct);

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Kullanıcı giriş yaptı. UserId: {UserId}", user.Id);

        return new LoginResultDto(
            accessToken.Token, refreshToken, accessToken.ExpiresAtUtc,
            new UserInfoDto(user.Id, user.UserName, user.FullName, user.MerchantId, roles));
    }
}

// ---- Refresh (rotating refresh token) ----

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResultDto>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResultDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LoginResultDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct)
                     ?? throw new UnauthorizedException("Geçersiz refresh token.");

        if (!stored.IsActive)
        {
            // Tekrar kullanım tespiti: iptal edilmiş token yeniden kullanıldıysa
            // tüm oturumlar sonlandırılır (bkz. docs/11-guvenlik.md §2).
            await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogWarning("Refresh token tekrar kullanımı tespit edildi; tüm oturumlar iptal edildi. UserId: {UserId}", stored.UserId);
            throw new UnauthorizedException("Geçersiz refresh token.");
        }

        var user = await _userRepository.GetWithRolesAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Kullanıcı bulunamadı veya pasif.");

        var roles = user.Roles.Select(r => r.Role.Name).ToArray();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);

        stored.Revoke(newRefreshTokenHash);
        _refreshTokenRepository.Update(stored);
        await _refreshTokenRepository.AddAsync(new RefreshToken(
            user.Id, newRefreshTokenHash, DateTime.UtcNow.Add(RefreshTokenLifetime)), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new LoginResultDto(
            accessToken.Token, newRefreshToken, accessToken.ExpiresAtUtc,
            new UserInfoDto(user.Id, user.UserName, user.FullName, user.MerchantId, roles));
    }
}

// ---- Logout ----

public sealed record LogoutCommand(string RefreshToken, string? AccessTokenJti, DateTime? AccessTokenExpiresAt) : IRequest<Unit>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ICacheService cache,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(
            _tokenService.HashRefreshToken(request.RefreshToken), ct);
        if (stored is not null && stored.IsActive)
        {
            stored.Revoke();
            _refreshTokenRepository.Update(stored);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Access token'ı kalan ömrü boyunca blacklist'e al.
        if (!string.IsNullOrEmpty(request.AccessTokenJti) && request.AccessTokenExpiresAt is not null)
        {
            var remaining = request.AccessTokenExpiresAt.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                await _cache.SetIfNotExistsAsync(CacheKeys.JwtBlacklist(request.AccessTokenJti), "revoked", remaining, ct);
        }

        return Unit.Value;
    }
}

// ---- Parola değiştirme ----

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Parola en az 10 karakter olmalıdır.")
            .Matches("[A-Z]").WithMessage("Parola en az bir büyük harf içermelidir.")
            .Matches("[a-z]").WithMessage("Parola en az bir küçük harf içermelidir.")
            .Matches(@"\d").WithMessage("Parola en az bir rakam içermelidir.")
            .Matches(@"[^\w\s]").WithMessage("Parola en az bir özel karakter içermelidir.");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasherService passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        if (!_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new UnauthorizedException("Mevcut parola hatalı.");

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        _userRepository.Update(user);

        // Parola değişince tüm oturumlar sonlandırılır.
        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
