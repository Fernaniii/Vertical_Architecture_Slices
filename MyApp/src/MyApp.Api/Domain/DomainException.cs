namespace MyApp.Api.Domain;

/// <summary>Thrown when an entity invariant is violated. Handlers validate first, so this is a safety net.</summary>
public sealed class DomainException(string message) : Exception(message);
