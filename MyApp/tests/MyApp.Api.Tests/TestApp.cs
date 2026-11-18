using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyApp.Api.Common;
using MyApp.Api.Common.Auth;
using MyApp.Api.Domain;
using MyApp.Api.Persistence;
using MyApp.Api.Persistence.Identity;

namespace MyApp.Api.Tests;

/// <summary>
/// The real handler pipeline (handlers + validation/logging decorators + HybridCache + Identity)
/// over a private in-memory SQLite database. One instance per test.
/// </summary>
internal sealed class TestApp : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly List<AsyncServiceScope> _scopes = [];

    private TestApp(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        _services = services;
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestApp> CreateAsync()
    {
        // The database lives as long as this connection stays open.
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(Ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        // Mirrors Persistence.AddPersistence(), which can't be called here because it wires the Aspire Npgsql client.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddHybridCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            Key = new string('k', 64)
        }));
        services.AddSingleton<ITokenService, JwtTokenService>();

        var apiAssembly = typeof(AppDbContext).Assembly;
        services.AddValidators(apiAssembly);
        services.AddHandlers(apiAssembly);

        var app = new TestApp(connection, services.BuildServiceProvider(validateScopes: true));

        var db = app.Get<AppDbContext>();
        await db.Database.EnsureCreatedAsync(Ct);

        var roles = app.Get<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            await roles.CreateAsync(new IdentityRole<Guid>(role));
        }

        return app;
    }

    /// <summary>Resolves from a fresh scope, so every call gets a new DbContext (like separate HTTP requests).</summary>
    public T Get<T>() where T : notnull
    {
        var scope = _services.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }

        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
