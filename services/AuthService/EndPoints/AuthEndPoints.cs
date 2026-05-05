namespace AuthService.Endpoints;

using AuthService.Providers.Interfaces;
using AuthService.Services.Interfaces;
using System.Security.Claims;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // 🔹 List providers
        app.MapGet("/auth/providers", (IEnumerable<IExternalAuthProvider> providers) =>
        {
            var names = providers.Select(p => p.Name);
            return Results.Ok(names);
        });

        // 🔹 Login
        app.MapGet("/auth/{provider}/login", async (
            string provider,
            HttpContext context,
            IEnumerable<IExternalAuthProvider> providers) =>
        {
            var selectedProvider = providers.FirstOrDefault(p =>
                p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (selectedProvider == null)
                return Results.BadRequest("Unknown provider");

            await selectedProvider.ChallengeAsync(context, $"/auth/{provider}/complete");

            return Results.Empty;
        });

        // 🔹 Complete
        app.MapGet("/auth/{provider}/complete", (
            string provider,
            HttpContext context,
            IJwtTokenService jwtService) =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var userId = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var email = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type.Contains("email"))?.Value;

            var name = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            var token = jwtService.CreateToken(userId, email, name, provider);

            return Results.Json(new
            {
                message = "Login successful",
                provider,
                token,
                user = new
                {
                    userId,
                    name,
                    email
                }
            });
        });
    }
}