namespace MyApp.Api.Domain;

/// <summary>A refresh token issued to a user. Only the hash is stored; the raw token is shown to the client once.</summary>
public sealed class UserRefreshToken : Entity
{
    private UserRefreshToken()
    {
        // Required by EF Core.
        TokenHash = string.Empty;
    }

    public Guid UserId { get; private init; }
    public string TokenHash { get; private init; }
    public DateTime CreatedAtUtc { get; private init; }
    public DateTime ExpiresAtUtc { get; private init; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public static UserRefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Refresh token hash is required.");
        }

        return new UserRefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public bool IsExpired(DateTime utcNow) => ExpiresAtUtc <= utcNow;

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAtUtc ??= DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
