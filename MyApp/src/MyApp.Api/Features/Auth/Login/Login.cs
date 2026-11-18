using Microsoft.AspNetCore.Identity;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Auth;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Domain;
using MyApp.Api.Persistence;
using MyApp.Api.Persistence.Identity;

namespace MyApp.Api.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);

internal sealed class LoginHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokens,
    AppDbContext db) : ICommandHandler<LoginCommand, LoginResponse>
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Email or password is incorrect.");

    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Error.Validation("Auth.CredentialsRequired", "Email and password are required.");
        }

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Error.Unauthorized("Auth.LockedOut", "The account is temporarily locked. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokens.CreateAccessToken(user.Id, user.Email!, roles);

        var refreshToken = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(UserRefreshToken.Create(user.Id, tokens.HashRefreshToken(refreshToken), tokens.GetRefreshTokenExpiryUtc()));
        await db.SaveChangesAsync(cancellationToken);

        return new LoginResponse(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken);
    }
}

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", async (
                LoginCommand command,
                ICommandHandler<LoginCommand, LoginResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(command, cancellationToken);
                return result.Match(Results.Ok);
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithTags("Auth");
}
