namespace AuthService.Providers.Interfaces;

public interface IExternalAuthProvider
{
    string Name { get; }

    Task ChallengeAsync(HttpContext context, string redirectUri);
}