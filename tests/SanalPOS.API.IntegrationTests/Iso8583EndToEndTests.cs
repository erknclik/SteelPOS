using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SanalPOS.BankSimulator;
using SanalPOS.Infrastructure.Iso8583.Dialects;
using Xunit;

namespace SanalPOS.API.IntegrationTests;

/// <summary>
/// Uçtan uca ISO 8583 doğrulaması: HTTP API -> CreatePaymentCommandHandler ->
/// Iso8583BankAdapter -> TcpIso8583Channel -> (gerçek TCP) -> BankSimulatorEngine.
/// Simülatör in-process, rastgele bir loopback portunda çalışır; API konfigürasyonu
/// (Iso8583:Banks) bu porta yönlendirilir.
/// </summary>
public class Iso8583ApiFactory : SanalPosApiFactory
{
    public const string ProviderCode = "SIMBANK";

    private readonly BankSimulatorEngine _simulator;

    public Iso8583ApiFactory()
    {
        _simulator = new BankSimulatorEngine(Iso8583Dialects.Iso87Ascii, NullLogger.Instance);
        _simulator.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Iso8583:Banks:0:Enabled", "true");
        builder.UseSetting("Iso8583:Banks:0:ProviderCode", ProviderCode);
        builder.UseSetting("Iso8583:Banks:0:Dialect", Iso8583Dialects.Iso87AsciiName);
        builder.UseSetting("Iso8583:Banks:0:TerminalId", "TERM0001");
        builder.UseSetting("Iso8583:Banks:0:MerchantId", "000000000000001");
        builder.UseSetting("Iso8583:Banks:0:MerchantNameLocation", "SanalPOS E2E Test Istanbul TR");
        builder.UseSetting("Iso8583:Banks:0:Channel:Host", "127.0.0.1");
        builder.UseSetting("Iso8583:Banks:0:Channel:Port", _simulator.Port.ToString());
        builder.UseSetting("Iso8583:Banks:0:Channel:ResponseTimeout", "00:00:02");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _simulator.DisposeAsync();
    }
}

public class Iso8583EndToEndTests : IClassFixture<Iso8583ApiFactory>
{
    private readonly Iso8583ApiFactory _factory;
    private readonly HttpClient _client;

    public Iso8583EndToEndTests(Iso8583ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string AccessToken, Guid TerminalId)> PrepareAsync()
    {
        await _factory.SeedAsync();
        var terminalId = await _factory.SeedTerminalAsync(Iso8583ApiFactory.ProviderCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "Admin123!*" });
        var login = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync()).RootElement;

        return (login.GetProperty("accessToken").GetString()!, terminalId);
    }

    private async Task<JsonElement> CreatePaymentAsync(string accessToken, Guid terminalId, string cardNumber, string cvv)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            merchantId = _factory.SeededMerchantId,
            terminalId,
            orderReference = $"E2E-{Guid.NewGuid():N}",
            amount = 249.90m,
            currency = "TRY",
            installmentCount = 1,
            cardNumber,
            cardHolderName = "E2E TEST USER",
            expireMonth = 12,
            expireYear = DateTime.UtcNow.Year + 2,
            cvv
        });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Payment_OverRealIso8583Wire_IsApproved()
    {
        var (token, terminalId) = await PrepareAsync();

        var payment = await CreatePaymentAsync(token, terminalId, "4111111111111111", "123");

        payment.GetProperty("status").GetString().Should().Be("Approved");
        payment.GetProperty("bankAuthCode").GetString().Should().StartWith("S");
    }

    [Fact]
    public async Task Payment_WithInsufficientFundsCard_IsDeclined()
    {
        var (token, terminalId) = await PrepareAsync();

        // Luhn-geçerli, "0002" ile biten senaryo kartı -> DE39=51.
        var payment = await CreatePaymentAsync(token, terminalId, "4000000000000002", "123");

        payment.GetProperty("status").GetString().Should().Be("Declined");
    }

    [Fact]
    public async Task Payment_WithCvvFailureScenario_IsDeclined()
    {
        var (token, terminalId) = await PrepareAsync();

        var payment = await CreatePaymentAsync(token, terminalId, "4111111111111111", "999");

        payment.GetProperty("status").GetString().Should().Be("Declined");
    }

    [Fact]
    public async Task Payment_WhenBankStaysSilent_IsDeclinedAfterAutoReversal()
    {
        var (token, terminalId) = await PrepareAsync();

        // Luhn-geçerli, "0004" ile biten senaryo kartı -> simülatör yanıt vermez;
        // adaptör 2 sn sonra timeout'a düşer, otomatik reversal gönderir ve işlemi reddeder.
        var payment = await CreatePaymentAsync(token, terminalId, "4000000800000004", "123");

        payment.GetProperty("status").GetString().Should().Be("Declined");
    }
}
