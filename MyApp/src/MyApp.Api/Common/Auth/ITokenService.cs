namespace MyApp.Api.Common.Auth;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Issues JWT access tokens and opaque refresh tokens. Takes primitives so Common stays independent of Persistence.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, string email, IEnumerable<string> roles);

    /// <summary>Returns a new random refresh token (the raw value that goes to the client).</summary>
    string CreateRefreshToken();

    /// <summary>Returns the value to store for a raw refresh token.</summary>
    string HashRefreshToken(string refreshToken);

    DateTime GetRefreshTokenExpiryUtc();
}
