using MyApp.Api.Common.Results;

namespace MyApp.Api.Common.Abstractions;

/// <summary>Hand-written validator for a command. Returns no errors when the command is valid.</summary>
public interface IValidator<in T>
{
    IEnumerable<Error> Validate(T instance);
}
