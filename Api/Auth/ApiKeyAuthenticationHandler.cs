using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chess.Api.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
    public string ExpectedKeyHash { get; set; } = "";
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var extractedApiKey) ||
            string.IsNullOrWhiteSpace(extractedApiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(extractedApiKey.ToString())));

        // If ExpectedKeyHash is configured, validate against it; otherwise allow for development demo
        if (!string.IsNullOrEmpty(Options.ExpectedKeyHash) &&
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedHash),
                Encoding.UTF8.GetBytes(Options.ExpectedKeyHash)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-user"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Coach"),
            new Claim(ClaimTypes.Role, "Player")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
