namespace MyApp.Api.Common.Http;

public static class DependencyInjection
{
    public static IServiceCollection AddHttpErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
