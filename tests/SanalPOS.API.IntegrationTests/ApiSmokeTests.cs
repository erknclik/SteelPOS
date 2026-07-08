using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SanalPOS.API.IntegrationTests;

public class ApiSmokeTests : IClassFixture<SanalPosApiFactory>
{
    private readonly SanalPosApiFactory _factory;
    private readonly HttpClient _client;

    public ApiSmokeTests(SanalPosApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthLive_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwaggerJson_ShouldBeServed()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Version_ShouldReturnApplicationInfo()
    {
        var response = await _client.GetAsync("/api/v1/version");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SanalPOS");
    }

    [Fact]
    public async Task Login_WithWrongCredentials_ShouldReturn401ProblemDetails()
    {
        await _factory.SeedAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Payments_WithoutToken_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/v1/payments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullFlow_LoginAndCreatePayment_ShouldSucceed()
    {
        await _factory.SeedAsync();

        // 1) Login
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "Admin123!*" });
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginBody);

        var login = JsonDocument.Parse(loginBody).RootElement;
        var accessToken = login.GetProperty("accessToken").GetString();
        accessToken.Should().NotBeNullOrEmpty();

        // 2) Ödeme oluştur (Idempotency-Key zorunlu)
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            merchantId = _factory.SeededMerchantId,
            terminalId = _factory.SeededTerminalId,
            orderReference = "SIP-TEST-001",
            amount = 1250.50m,
            currency = "TRY",
            installmentCount = 1,
            cardNumber = "4111111111111111",
            cardHolderName = "TEST USER",
            expireMonth = 12,
            expireYear = DateTime.UtcNow.Year + 2,
            cvv = "123"
        });

        var paymentResponse = await _client.SendAsync(request);
        var body = await paymentResponse.Content.ReadAsStringAsync();

        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var payment = JsonDocument.Parse(body).RootElement;
        payment.GetProperty("status").GetString().Should().Be("Approved");
        payment.GetProperty("commissionAmount").GetDecimal().Should().Be(31.26m);

        // 3) İşlem detayı okunabilmeli
        var transactionId = payment.GetProperty("transactionId").GetGuid();
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/payments/{transactionId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync()).RootElement;
        detail.GetProperty("maskedCardNumber").GetString().Should().Be("4111 11** **** 1111");
    }

    [Fact]
    public async Task CreatePayment_WithoutIdempotencyKey_ShouldReturn400()
    {
        await _factory.SeedAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "admin", password = "Admin123!*" });
        var login = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync()).RootElement;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", login.GetProperty("accessToken").GetString());
        request.Content = JsonContent.Create(new { });

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
