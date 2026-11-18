namespace MyApp.Api.Common.Abstractions;

/// <summary>One implementation per slice. Discovered by reflection and mapped at startup.</summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
