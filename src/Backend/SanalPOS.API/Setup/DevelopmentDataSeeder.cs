using Microsoft.EntityFrameworkCore;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.ValueObjects;
using SanalPOS.Infrastructure.BankAdapters;
using SanalPOS.Infrastructure.EfCore;

namespace SanalPOS.API.Setup;

/// <summary>
/// Development ortamı için şema + demo veri hazırlığı. Sadece EF Core provider aktifken çalışır;
/// NHibernate seçiliyse şema infra/scripts/db-init.sql veya Flyway ile hazırlanmalıdır
/// (bkz. docs/03-veritabani-tasarimi.md §5).
/// </summary>
public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetService<SanalPosDbContext>();
        if (context is null)
        {
            logger.LogInformation("EF Core provider aktif değil; seed atlandı.");
            return;
        }

        try
        {
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
                await context.Database.MigrateAsync();
            else
                await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Veritabanına erişilemedi; seed atlandı. (Docker Compose çalışıyor mu?)");
            return;
        }

        if (await context.Users.AnyAsync())
            return;

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        // Roller
        var roles = Role.All.Select(name => new Role(name)).ToList();
        context.Roles.AddRange(roles);

        // Demo merchant + mağaza + terminal + komisyon kuralı
        var merchant = new Merchant("Demo Ticaret A.Ş.", "1234567890", new Iban("TR330006100519786457841326"), 2.50m);
        context.Merchants.Add(merchant);

        var store = new Store(merchant.Id, "Merkez Mağaza", "İstanbul");
        context.Stores.Add(store);

        var terminal = new Terminal(store.Id, "TERM-0001", MockBankAdapter.Code);
        context.Terminals.Add(terminal);

        context.CommissionRules.Add(new CommissionRule(
            merchant.Id, 3, 3.25m, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)), null));

        // Kullanıcılar: sistem admini + merchant operatörü
        var admin = new User("admin", "admin@sanalpos.local", "Sistem Yöneticisi",
            passwordHasher.Hash("Admin123!*"), merchantId: null);
        admin.AssignRole(roles.First(r => r.Name == Role.SystemAdmin));
        context.Users.Add(admin);

        var operatorUser = new User("operator", "operator@sanalpos.local", "Demo Operatör",
            passwordHasher.Hash("Operator123!*"), merchant.Id);
        operatorUser.AssignRole(roles.First(r => r.Name == Role.Operator));
        context.Users.Add(operatorUser);

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Demo veri seed edildi. Kullanıcılar: admin/Admin123!* (SystemAdmin), operator/Operator123!* (Operator). " +
            "MerchantId: {MerchantId}, TerminalId: {TerminalId}", merchant.Id, terminal.Id);
    }
}
