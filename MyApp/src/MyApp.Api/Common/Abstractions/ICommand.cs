namespace MyApp.Api.Common.Abstractions;

/// <summary>Marker for a command that returns no data.</summary>
public interface ICommand;

/// <summary>Marker for a command that returns <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>;
