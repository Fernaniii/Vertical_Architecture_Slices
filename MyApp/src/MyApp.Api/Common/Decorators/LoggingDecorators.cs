using System.Diagnostics;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Results;

namespace MyApp.Api.Common.Decorators;

internal static class RequestLogging
{
    public static async Task<TResult> Run<TResult>(ILogger logger, string requestName, Func<Task<TResult>> handle)
        where TResult : Result
    {
        logger.LogInformation("Handling {RequestName}", requestName);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await handle();
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (result.IsSuccess)
            {
                logger.LogInformation("Handled {RequestName} in {ElapsedMs:0.0} ms", requestName, elapsedMs);
            }
            else
            {
                logger.LogWarning("{RequestName} failed with {ErrorCode} in {ElapsedMs:0.0} ms",
                    requestName, result.Error.Code, elapsedMs);
            }

            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{RequestName} threw after {ElapsedMs:0.0} ms",
                requestName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}

internal sealed class LoggingCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<LoggingCommandHandler<TCommand>> logger) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
        RequestLogging.Run(logger, typeof(TCommand).Name, () => inner.Handle(command, cancellationToken));
}

internal sealed class LoggingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandler<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
        RequestLogging.Run(logger, typeof(TCommand).Name, () => inner.Handle(command, cancellationToken));
}

internal sealed class LoggingQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryHandler<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
        RequestLogging.Run(logger, typeof(TQuery).Name, () => inner.Handle(query, cancellationToken));
}
