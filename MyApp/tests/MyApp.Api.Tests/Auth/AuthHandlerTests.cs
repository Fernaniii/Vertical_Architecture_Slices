using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Results;
using MyApp.Api.Features.Auth.Login;
using MyApp.Api.Features.Auth.RefreshToken;
using MyApp.Api.Features.Auth.Register;
using static MyApp.Api.Tests.TestApp;

namespace MyApp.Api.Tests.Auth;

public sealed class AuthHandlerTests : IAsyncLifetime
{
    private const string Email = "user@example.com";
    private const string Password = "Passw0rd!";

    private TestApp _app = null!;

    public async ValueTask InitializeAsync() => _app = await CreateAsync();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private Task<Result<RegisterResponse>> Register(string email = Email, string password = Password) =>
        _app.Get<ICommandHandler<RegisterCommand, RegisterResponse>>().Handle(new RegisterCommand(email, password), Ct);

    private Task<Result<LoginResponse>> Login(string password = Password) =>
        _app.Get<ICommandHandler<LoginCommand, LoginResponse>>().Handle(new LoginCommand(Email, password), Ct);

    private Task<Result<RefreshTokenResponse>> Refresh(string refreshToken) =>
        _app.Get<ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>>().Handle(new RefreshTokenCommand(refreshToken), Ct);

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_conflict()
    {
        Assert.True((await Register()).IsSuccess);

        var second = await Register();

        Assert.Equal(ErrorType.Conflict, second.Error.Type);
    }

    [Fact]
    public async Task Register_rejects_a_weak_password()
    {
        var result = await Register(password: "short");

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Login_returns_tokens_for_valid_credentials()
    {
        await Register();

        var result = await Login();

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password()
    {
        await Register();

        var result = await Login("WrongPassw0rd!");

        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        await Register();
        var login = (await Login()).Value;

        var refreshed = await Refresh(login.RefreshToken);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(login.RefreshToken, refreshed.Value.RefreshToken);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_session()
    {
        await Register();
        var original = (await Login()).Value.RefreshToken;
        var rotated = (await Refresh(original)).Value.RefreshToken;

        var reuse = await Refresh(original);
        var afterReuse = await Refresh(rotated);

        Assert.Equal(ErrorType.Unauthorized, reuse.Error.Type);
        Assert.Equal(ErrorType.Unauthorized, afterReuse.Error.Type);
    }
}
