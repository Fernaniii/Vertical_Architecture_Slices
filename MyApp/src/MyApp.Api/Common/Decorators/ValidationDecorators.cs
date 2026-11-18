using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Results;

namespace MyApp.Api.Common.Decorators;

internal static class RequestValidation
{
    /// <summary>Runs every validator for the request. Returns null when the request is valid.</summary>
    public static Error? Validate<T>(IEnumerable<IValidator<T>> validators, T request)
    {
        var errors = validators.SelectMany(v => v.Validate(request)).ToList();

        return errors.Count switch
        {
            0 => null,
            1 => errors[0],
            _ => Error.Validation("Validation.Failed", string.Join(" ", errors.Select(e => e.Description)))
        };
    }
}

internal sealed class ValidationCommandHandler<TCommand>(
    ICommandHandler<TCommand> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
        RequestValidation.Validate(validators, command) is { } error
            ? Task.FromResult(Result.Failure(error))
            : inner.Handle(command, cancellationToken);
}

internal sealed class ValidationCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
        RequestValidation.Validate(validators, command) is { } error
            ? Task.FromResult(Result.Failure<TResponse>(error))
            : inner.Handle(command, cancellationToken);
}
