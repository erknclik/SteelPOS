using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SanalPOS.API.IntegrationTests;

/// <summary>
/// Uçtan uca gün sonu mutabakatı: ödeme/iade/iptal işlemleri gerçek ISO 8583 hattından
/// simülatöre gider (simülatör kendi defterine yazar); ardından reconciliation/run,
/// 0500 batch-close ile toplamları gönderir ve simülatör defteriyle karşılaştırılır.
/// Bu sınıf kendi factory instance'ını kullanır (xUnit fixture per-class) — simülatör
/// defteri yalnızca bu sınıfın işlemlerini içerir.
/// </summary>
public class ReconciliationEndToEndTests : IClassFixture<Iso8583ApiFactory>
{
    private readonly Iso8583ApiFactory _factory;
    private readonly HttpClient _client;

    public ReconciliationEndToEndTests(Iso8583ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullDay_SaleRefundVoid_ReconciliationIsBalanced()
    {
        await _factory.SeedAsync();
        var terminalId = await _factory.SeedTerminalAsync(Iso8583ApiFactory.ProviderCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "Admin123!*" });
        var token = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accessToken").GetString()!;

        // 1) Üç satış: biri iade edilir, biri void edilir, biri olduğu gibi kalır.
        var tx1 = await PayAsync(token, terminalId, 100.00m);
        var tx2 = await PayAsync(token, terminalId, 50.25m);
        var tx3 = await PayAsync(token, terminalId, 200.50m);

        await PostAsync(token, $"/api/v1/payments/{tx2}/refund", new { amount = 50.25m, reason = "Mutabakat testi" });
        await PostAsync(token, $"/api/v1/payments/{tx3}/void", null);

        // 2) Mutabakat: bugünün toplamları (3 satış, 1 iade, 1 iptal) bankaya gönderilir.
        var response = await PostAsync(token, "/api/v1/reconciliation/run",
            new { day = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") });

        var results = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        results.GetArrayLength().Should().Be(1);

        var result = results[0];
        result.GetProperty("providerCode").GetString().Should().Be(Iso8583ApiFactory.ProviderCode);
        result.GetProperty("saleCount").GetInt32().Should().Be(3, "void edilen işlem de otorize edilmiş bir satıştır");
        result.GetProperty("saleAmount").GetDecimal().Should().Be(350.75m);
        result.GetProperty("refundCount").GetInt32().Should().Be(1);
        result.GetProperty("refundAmount").GetDecimal().Should().Be(50.25m);
        result.GetProperty("voidCount").GetInt32().Should().Be(1);
        result.GetProperty("voidAmount").GetDecimal().Should().Be(200.50m);
        result.GetProperty("isBalanced").GetBoolean().Should().BeTrue("banka defteri ile toplamlar birebir eşleşmeli");

        _ = tx1;
    }

    [Fact]
    public async Task EmptyDay_ReturnsNoResults()
    {
        await _factory.SeedAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "Admin123!*" });
        var token = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accessToken").GetString()!;

        // 30 gün önce hiç işlem yok: sonuç listesi boş döner (banka çağrılmaz).
        var response = await PostAsync(token, "/api/v1/reconciliation/run",
            new { day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)).ToString("yyyy-MM-dd") });

        JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetArrayLength().Should().Be(0);
    }

    private async Task<Guid> PayAsync(string token, Guid terminalId, decimal amount)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            merchantId = _factory.SeededMerchantId,
            terminalId,
            orderReference = $"RECON-{Guid.NewGuid():N}",
            amount,
            currency = "TRY",
            installmentCount = 1,
            cardNumber = "4111111111111111",
            cardHolderName = "RECON TEST USER",
            expireMonth = 12,
            expireYear = DateTime.UtcNow.Year + 2,
            cvv = "123"
        });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var payment = JsonDocument.Parse(body).RootElement;
        payment.GetProperty("status").GetString().Should().Be("Approved");
        return payment.GetProperty("transactionId").GetGuid();
    }

    private async Task<HttpResponseMessage> PostAsync(string token, string url, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body ?? new { });

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        return response;
    }
}
