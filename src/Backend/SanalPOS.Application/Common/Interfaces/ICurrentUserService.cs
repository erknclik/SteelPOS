namespace SanalPOS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string UserName { get; }
    Guid? MerchantId { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}

public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}
