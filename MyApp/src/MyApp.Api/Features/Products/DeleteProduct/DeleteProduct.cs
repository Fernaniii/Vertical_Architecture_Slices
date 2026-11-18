using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Auth;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Persistence;

namespace MyApp.Api.Features.Products.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : ICommand;

internal sealed class DeleteProductHandler(AppDbContext db, HybridCache cache) : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var deleted = await db.Products
            .Where(p => p.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return Result.Failure(Error.NotFound("Product.NotFound", $"Product '{command.Id}' was not found."));
        }

        await cache.RemoveByTagAsync(ProductCache.Tag, cancellationToken);
        return Result.Success();
    }
}

internal sealed class DeleteProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/products/{id:guid}", async (
                Guid id,
                ICommandHandler<DeleteProductCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new DeleteProductCommand(id), cancellationToken);
                return result.Match(Results.NoContent);
            })
            .RequireAuthorization(AuthPolicies.AdminOnly)
            .WithName("DeleteProduct")
            .WithTags("Products");
}
