using Microsoft.Extensions.Logging;
using PatientBooking.Api.Application.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace PatientBooking.Api.Application.Services;

public sealed class HaveIBeenPwnedPasswordChecker(
    HttpClient httpClient,
    ILogger<HaveIBeenPwnedPasswordChecker> logger
) : IBreachedPasswordChecker
{
    public async Task<bool> IsBreachedAsync(string password, CancellationToken ct)
    {
#pragma warning disable CA5350, S4790
        var sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350, S4790
        var prefix = sha1[..5];
        var suffix = sha1[5..];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            // Pads response with decoy entries so a network observer can't infer which password was checked
            // from the response alone - HIBP's own recommendation
            request.Headers.Add("Add-Padding", "true");

            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);

            return body
                .Split('\n')
                .Select(line => line.Split(':')[0].Trim())
                .Any(candidateSuffix => candidateSuffix.Equals(suffix, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Fail open: HIBP outage shouldn't block every registration/reset in the app
            logger.BreachCheckFailed(ex);
            return false;
        }
    }
}
