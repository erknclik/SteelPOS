using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SanalPOS.Infrastructure.ThreeDSecure;

namespace SanalPOS.API.Controllers;

/// <summary>
/// SADECE GELİŞTİRME: sahte ACS (Access Control Server) sayfası. Gerçek akışta bu sayfa
/// kart hamilinin bankasına aittir (OTP girilir). Simülasyon, PaReq içine gömülen
/// sonucu (Y/N) PaRes olarak TermUrl'e otomatik form-post eder.
/// ThreeDSecure:Provider "Simulated" değilse 404 döner; üretim konfigürasyonunda kapalıdır.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/acs-simulator")]
[AllowAnonymous]
public class AcsSimulatorController : ControllerBase
{
    private readonly bool _enabled;

    public AcsSimulatorController(IConfiguration configuration) =>
        _enabled = (configuration["ThreeDSecure:Provider"] ?? SimulatedThreeDSecureProvider.ProviderName)
                   == SimulatedThreeDSecureProvider.ProviderName;

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Authenticate(
        [FromForm(Name = "MD")] string md,
        [FromForm(Name = "PaReq")] string paReq,
        [FromForm(Name = "TermUrl")] string termUrl)
    {
        if (!_enabled)
            return NotFound();

        // PaReq: base64("{transactionId}|{Y|N}") — simüle MPI'ın gömdüğü doğrulama sonucu.
        string paRes;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(paReq));
            paRes = decoded.Split('|') is [_, var outcome] ? outcome : "N";
        }
        catch (FormatException)
        {
            paRes = "N";
        }

        var html = $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="utf-8"><title>ACS Simülatörü</title></head>
            <body onload="document.forms[0].submit()">
              <p>3D Secure doğrulaması simüle ediliyor, yönlendiriliyorsunuz...</p>
              <form method="post" action="{System.Net.WebUtility.HtmlEncode(termUrl)}">
                <input type="hidden" name="MD" value="{System.Net.WebUtility.HtmlEncode(md)}" />
                <input type="hidden" name="PaRes" value="{System.Net.WebUtility.HtmlEncode(paRes)}" />
                <noscript><button type="submit">Devam</button></noscript>
              </form>
            </body>
            </html>
            """;

        return Content(html, "text/html", Encoding.UTF8);
    }
}
