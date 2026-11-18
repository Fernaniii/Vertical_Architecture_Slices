using System.Reflection;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Decorators;

namespace MyApp.Api.Common;

public static class DependencyInjection
{
    /// <summary>
    /// Decorators per handler interface, innermost first. The resolved chain is
    /// Logging -> Validation -> Handler for commands and Logging -> Handler for queries.
    /// </summary>
    private static readonly Dictionary<Type, Type[]> Decorators = new()
    {
        [typeof(ICommandHandler<>)] = [typeof(ValidationCommandHandler<>), typeof(LoggingCommandHandler<>)],
        [typeof(ICommandHandler<,>)] = [typeof(ValidationCommandHandler<,>), typeof(LoggingCommandHandler<,>)],
        [typeof(IQueryHandler<,>)] = [typeof(LoggingQueryHandler<,>)]
    };

    /// <summary>
    /// Registers every concrete command/query handler in the assembly against its handler interface,
    /// wrapped in the decorators listed in <see cref="Decorators"/>.
    /// </summary>
    public static IServiceCollection AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in handlers)
        {
            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType ||
                    !Decorators.TryGetValue(@interface.GetGenericTypeDefinition(), out var decorators))
                {
                    continue;
                }

                services.AddScoped(type);
                services.AddScoped(@interface, sp => Decorate(sp, type, @interface, decorators));
            }
        }

        return services;
    }

    /// <summary>Registers every concrete <see cref="IValidator{T}"/> in the assembly.</summary>
    public static IServiceCollection AddValidators(this IServiceCollection services, Assembly assembly)
    {
        var validators = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in validators)
        {
            foreach (var @interface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            {
                services.AddScoped(@interface, type);
            }
        }

        return services;
    }

    /// <summary>Registers every concrete <see cref="IEndpoint"/> in the assembly.</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpoints = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IEndpoint)));

        foreach (var type in endpoints)
        {
            services.AddTransient(typeof(IEndpoint), type);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }

    private static object Decorate(IServiceProvider services, Type handlerType, Type handlerInterface, Type[] decorators)
    {
        var current = services.GetRequiredService(handlerType);
        var typeArguments = handlerInterface.GetGenericArguments();

        foreach (var decorator in decorators)
        {
            current = ActivatorUtilities.CreateInstance(services, decorator.MakeGenericType(typeArguments), current);
        }

        return current;
    }
}
