using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Auth;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Domain;
using MyApp.Api.Persistence;
using MyApp.Api.Persistence.Identity;

namespace MyApp.Api.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>;

public sealed record RefreshTokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);

internal sealed class RefreshTokenHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokens,
    AppDbContext db) : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private static readonly Error Invalid =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Error.Validation("Auth.RefreshTokenRequired", "Refresh token is required.");
        }

        var hash = tokens.HashRefreshToken(command.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return Invalid;
        }

        var now = DateTime.UtcNow;

        if (stored.RevokedAtUtc is not null)
        {
            // A rotated token was presented again: assume it leaked and end every session of this user.
            await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
            return Invalid;
        }

        if (stored.IsExpired(now))
        {
            return Invalid;
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return Invalid;
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokens.CreateAccessToken(user.Id, user.Email!, roles);

        var newRefreshToken = tokens.CreateRefreshToken();
        var newHash = tokens.HashRefreshToken(newRefreshToken);

        stored.Revoke(newHash);
        db.RefreshTokens.Add(UserRefreshToken.Create(user.Id, newHash, tokens.GetRefreshTokenExpiryUtc()));
        await db.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResponse(accessToken.Value, accessToken.ExpiresAtUtc, newRefreshToken);
    }
}

internal sealed class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/refresh", async (
                RefreshTokenCommand command,
                ICommandHandler<RefreshTokenCommand, RefreshTokenResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(command, cancellationToken);
                return result.Match(Results.Ok);
            })
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithTags("Auth");
}
