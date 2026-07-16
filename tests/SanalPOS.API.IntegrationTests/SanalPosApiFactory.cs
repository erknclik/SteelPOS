using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.ValueObjects;
using SanalPOS.Infrastructure.BankAdapters;
using SanalPOS.Infrastructure.EfCore;

namespace SanalPOS.API.IntegrationTests;

/// <summary>
/// Dış bağımlılık (PostgreSQL/Redis/RabbitMQ) gerektirmeyen smoke test host'u:
/// EF InMemory + in-memory cache/lock + MassTransit InMemory transport.
/// Gerçek altyapı ile uçtan uca testler için Testcontainers tabanlı ayrı bir
/// SanalPOS.Infrastructure.IntegrationTests projesi planlanmıştır (bkz. docs/12-test-stratejisi.md §3).
/// </summary>
public class SanalPosApiFactory : WebApplicationFactory<Program>
{
    public Guid SeededMerchantId { get; private set; }
    public Guid SeededTerminalId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Messaging:Provider", "InMemory");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-32-chars!!");
        builder.UseSetting("ConnectionStrings:Redis", "localhost:1,abortConnect=false,connectTimeout=200");

        builder.ConfigureServices(services =>
        {
            // EF Core Npgsql kaydını InMemory provider ile değiştir.
            // (Veritabanı adı bir kez üretilir; scope başına farklı DB oluşmasın.)
            var databaseName = $"sanalpos-tests-{Guid.NewGuid()}";
            services.RemoveAll<DbContextOptions<SanalPosDbContext>>();
            services.AddDbContext<SanalPosDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            // Redis yerine in-memory cache/lock.
            services.RemoveAll<ICacheService>();
            services.RemoveAll<IDistributedLockService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.AddSingleton<IDistributedLockService, NoopLockService>();
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SanalPosDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        if (context.Users.Any())
            return;

        var roles = Role.All.Select(name => new Role(name)).ToList();
        context.Roles.AddRange(roles);

        var merchant = new Merchant("Test Merchant", "1234567890", new Iban("TR330006100519786457841326"), 2.5m);
        var store = new Store(merchant.Id, "Test Store", null);
        var terminal = new Terminal(store.Id, "TERM-TEST", MockBankAdapter.Code);
        context.Merchants.Add(merchant);
        context.Stores.Add(store);
        context.Terminals.Add(terminal);

        var admin = new User("admin", "admin@test.local", "Test Admin", passwordHasher.Hash("Admin123!*"), null);
        admin.AssignRole(roles.First(r => r.Name == Role.SystemAdmin));
        context.Users.Add(admin);

        await context.SaveChangesAsync();

        SeededMerchantId = merchant.Id;
        SeededTerminalId = terminal.Id;
    }

    /// <summary>Seed edilen mağazaya, verilen banka sağlayıcı koduyla ek bir terminal ekler.</summary>
    public async Task<Guid> SeedTerminalAsync(string bankProviderCode)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SanalPosDbContext>();

        var store = context.Stores.First();
        var existing = context.Terminals.FirstOrDefault(t => t.BankProviderCode == bankProviderCode);
        if (existing is not null)
            return existing.Id;

        var terminal = new Terminal(store.Id, $"TERM-{bankProviderCode}", bankProviderCode);
        context.Terminals.Add(terminal);
        await context.SaveChangesAsync();
        return terminal.Id;
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, string> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var value)
                ? System.Text.Json.JsonSerializer.Deserialize<T>(value)
                : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        {
            _store[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.ContainsKey(key));

        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult(_store.TryAdd(key, System.Text.Json.JsonSerializer.Serialize(value)));
    }

    private sealed class NoopLockService : IDistributedLockService
    {
        public Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult<IAsyncDisposable?>(new NoopHandle());

        private sealed class NoopHandle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
