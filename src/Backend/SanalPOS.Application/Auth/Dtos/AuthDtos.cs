namespace SanalPOS.Application.Auth.Dtos;

public sealed record LoginResultDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserInfoDto User);

public sealed record UserInfoDto(Guid Id, string UserName, string FullName, Guid? MerchantId, IReadOnlyCollection<string> Roles);
