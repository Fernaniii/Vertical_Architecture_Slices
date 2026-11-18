using MyApp.Api.Common;
using MyApp.Api.Common.Auth;
using MyApp.Api.Common.Caching;
using MyApp.Api.Common.Http;
using MyApp.Api.Persistence;
using MyApp.Api.Persistence.Identity;
using MyApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

var assembly = typeof(Program).Assembly;

builder.AddServiceDefaults();
builder.AddPersistence();
builder.AddCaching();
builder.Services.AddJwtAuthentication();
builder.Services.AddHttpErrorHandling();
builder.Services.AddValidators(assembly);
builder.Services.AddHandlers(assembly);
builder.Services.AddEndpoints(assembly);
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.ApplyMigrationsAsync();
}

await app.SeedIdentityAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Before UseAuthorization, so the fallback policy doesn't block the UI's static files.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "MyApp API v1"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // /openapi/v1.json, rendered by Swagger UI at /swagger
}

app.MapGet("/", () => "MyApp.Api is running").AllowAnonymous();
app.MapEndpoints();

app.Run();
