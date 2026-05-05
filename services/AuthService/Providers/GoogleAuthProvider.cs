using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using AuthService.Providers.Interfaces;

namespace AuthService.Providers;

public class GoogleAuthProvider : IExternalAuthProvider
{
    public string Name => "google";

    public Task ChallengeAsync(HttpContext context, string redirectUri)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return context.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties);
    }
}