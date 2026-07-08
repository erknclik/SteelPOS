using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

/// <summary>
/// Uygulama kullanıcısı. Parola hash'i ASP.NET Core Identity PasswordHasher ile üretilir;
/// merchant_id null ise sistem kullanıcısıdır (SystemAdmin).
/// </summary>
public class User : BaseEntity, IAuditableEntity
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Guid? MerchantId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }

    public virtual ICollection<UserRole> Roles { get; protected set; } = new List<UserRole>();

    protected User()
    {
    }

    public User(string userName, string email, string fullName, string passwordHash, Guid? merchantId)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new DomainException("Kullanıcı adı boş olamaz.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parola hash'i boş olamaz.");

        UserName = userName.Trim().ToLowerInvariant();
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        MerchantId = merchantId;
    }

    public bool IsLockedOut => LockoutEndAt is not null && LockoutEndAt > DateTime.UtcNow;

    public void RegisterFailedLogin()
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
        {
            LockoutEndAt = DateTime.UtcNow.Add(LockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutEndAt = null;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("Parola hash'i boş olamaz.");
        PasswordHash = newPasswordHash;
    }

    public void AssignRole(Role role)
    {
        if (Roles.Any(r => r.RoleId == role.Id))
            return;
        Roles.Add(new UserRole(Id, role.Id));
    }

    public void Deactivate() => IsActive = false;
}

public class Role : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    protected Role()
    {
    }

    public Role(string name) => Name = name;

    public const string SystemAdmin = "SystemAdmin";
    public const string MerchantAdmin = "MerchantAdmin";
    public const string Operator = "Operator";
    public const string ReadOnly = "ReadOnly";

    public static readonly string[] All = [SystemAdmin, MerchantAdmin, Operator, ReadOnly];
}

public class UserRole : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public virtual Role Role { get; protected set; } = null!;

    protected UserRole()
    {
    }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }
}
