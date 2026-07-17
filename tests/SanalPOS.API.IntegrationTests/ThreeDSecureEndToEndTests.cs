using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace SanalPOS.API.IntegrationTests;

/// <summary>
/// Uçtan uca 3D Secure akışı: initiate -> (sahte) ACS sayfası -> complete callback ->
/// ISO 8583 hattı üzerinden banka simülatörüne otorizasyon. Iso8583ApiFactory'nin
/// kurduğu SIMBANK terminali ve in-process banka simülatörü kullanılır.
/// </summary>
public class ThreeDSecureEndToEndTests : IClassFixture<Iso8583ApiFactory>
{
    private readonly Iso8583ApiFactory _factory;
    private readonly HttpClient _client;

    public ThreeDSecureEndToEndTests(Iso8583ApiFactory factory)
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

    private async Task<JsonElement> InitiateAsync(string accessToken, Guid terminalId, string cardNumber)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/3ds");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            merchantId = _factory.SeededMerchantId,
            terminalId,
            orderReference = $"3DS-{Guid.NewGuid():N}",
            amount = 350.75m,
            currency = "TRY",
            installmentCount = 1,
            cardNumber,
            cardHolderName = "3DS TEST USER",
            expireMonth = 12,
            expireYear = DateTime.UtcNow.Year + 2,
            cvv = "123",
            callbackUrl = "/api/v1/payments/3ds/complete"
        });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement;
    }

    private async Task<(string Md, string PaRes)> RunAcsAsync(JsonElement initiation)
    {
        // Kart hamilinin tarayıcısını simüle et: ACS sayfasına form-post, dönen HTML'den
        // otomatik submit edilecek MD/PaRes değerlerini çıkar.
        var acsResponse = await _client.PostAsync(
            initiation.GetProperty("acsUrl").GetString(),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["MD"] = initiation.GetProperty("md").GetString()!,
                ["PaReq"] = initiation.GetProperty("paReq").GetString()!,
                ["TermUrl"] = "/api/v1/payments/3ds/complete"
            }));

        acsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await acsResponse.Content.ReadAsStringAsync();

        string Extract(string name) =>
            Regex.Match(html, $"""name="{name}" value="([^"]*)""").Groups[1].Value;

        return (Extract("MD"), Extract("PaRes"));
    }

    private async Task<HttpResponseMessage> CompleteAsync(string md, string paRes) =>
        await _client.PostAsync("/api/v1/payments/3ds/complete",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["MD"] = md, ["PaRes"] = paRes }));

    [Fact]
    public async Task FullThreeDSFlow_EnrolledCard_IsApprovedOverIso8583Wire()
    {
        var (token, terminalId) = await PrepareAsync();

        var initiation = await InitiateAsync(token, terminalId, "4111111111111111");
        initiation.GetProperty("requiresRedirect").GetBoolean().Should().BeTrue();

        var (md, paRes) = await RunAcsAsync(initiation);
        paRes.Should().Be("Y");

        var completeResponse = await CompleteAsync(md, paRes);
        var body = await completeResponse.Content.ReadAsStringAsync();
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var payment = JsonDocument.Parse(body).RootElement;
        payment.GetProperty("status").GetString().Should().Be("Approved");
        payment.GetProperty("bankAuthCode").GetString().Should().StartWith("S", "otorizasyon banka simülatöründen gelmeli");
        payment.GetProperty("transactionId").GetGuid().Should().Be(initiation.GetProperty("transactionId").GetGuid());
    }

    [Fact]
    public async Task ThreeDSFlow_WhenAcsAuthenticationFails_IsDeclinedWithoutBankCall()
    {
        var (token, terminalId) = await PrepareAsync();

        // Luhn-geçerli, "0005" ile biten senaryo kartı -> ACS PaRes="N" döner.
        var initiation = await InitiateAsync(token, terminalId, "4000000700000005");
        var (md, paRes) = await RunAcsAsync(initiation);
        paRes.Should().Be("N");

        var completeResponse = await CompleteAsync(md, paRes);
        var payment = JsonDocument.Parse(await completeResponse.Content.ReadAsStringAsync()).RootElement;

        payment.GetProperty("status").GetString().Should().Be("Declined");
        payment.GetProperty("bankAuthCode").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ThreeDSFlow_NotEnrolledCard_FallsBackToDirectAuthorization()
    {
        var (token, terminalId) = await PrepareAsync();

        // "0006" ile biten kart 3DS'e kayıtlı değil: yönlendirme olmadan doğrudan otorize edilir.
        var initiation = await InitiateAsync(token, terminalId, "4000000600000006");

        initiation.GetProperty("requiresRedirect").GetBoolean().Should().BeFalse();
        initiation.GetProperty("payment").GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Complete_WithSameMdTwice_IsRejected()
    {
        var (token, terminalId) = await PrepareAsync();

        var initiation = await InitiateAsync(token, terminalId, "4111111111111111");
        var (md, paRes) = await RunAcsAsync(initiation);

        (await CompleteAsync(md, paRes)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Aynı MD ile tekrar (replay): oturum silindiği için reddedilmeli.
        (await CompleteAsync(md, paRes)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_WithRelativeReturnUrl_RedirectsToResultPage()
    {
        var (token, terminalId) = await PrepareAsync();
        var initiation = await InitiateAsync(token, terminalId, "4111111111111111");
        var (md, paRes) = await RunAcsAsync(initiation);

        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            "/api/v1/payments/3ds/complete?returnUrl=" + Uri.EscapeDataString("/payments/3ds/result"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["MD"] = md, ["PaRes"] = paRes }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("/payments/3ds/result?");
        location.Should().Contain("status=Approved").And.Contain(
            "transactionId=" + initiation.GetProperty("transactionId").GetGuid());
    }

    [Fact]
    public async Task Complete_WithDisallowedAbsoluteReturnUrl_Returns400()
    {
        await PrepareAsync();

        var response = await _client.PostAsync(
            "/api/v1/payments/3ds/complete?returnUrl=" + Uri.EscapeDataString("https://evil.example/phish"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["MD"] = "x", ["PaRes"] = "Y" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_WithUnknownMd_Returns404()
    {
        await PrepareAsync();

        var response = await CompleteAsync(Guid.NewGuid().ToString("N"), "Y");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
