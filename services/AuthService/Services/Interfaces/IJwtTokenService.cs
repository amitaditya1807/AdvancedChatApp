namespace AuthService.Services.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(string userId, string email, string name, string provider);
}