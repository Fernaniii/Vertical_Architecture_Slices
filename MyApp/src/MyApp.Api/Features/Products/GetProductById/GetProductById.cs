using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Persistence;

namespace MyApp.Api.Features.Products.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdResponse>;

public sealed record GetProductByIdResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

internal sealed class GetProductByIdHandler(AppDbContext db, HybridCache cache)
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>
{
    private static readonly string[] Tags = [ProductCache.Tag];

    public async Task<Result<GetProductByIdResponse>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        // A missing product is cached as null too; any product write clears the tag, so that never goes stale.
        var product = await cache.GetOrCreateAsync(
            ProductCache.ByIdKey(query.Id),
            (db, query.Id),
            static async (state, ct) => await state.db.Products
                .AsNoTracking()
                .Where(p => p.Id == state.Id)
                .Select(p => new GetProductByIdResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAtUtc, p.UpdatedAtUtc))
                .FirstOrDefaultAsync(ct),
            tags: Tags,
            cancellationToken: cancellationToken);

        return product is null
            ? Error.NotFound("Product.NotFound", $"Product '{query.Id}' was not found.")
            : product;
    }
}

internal sealed class GetProductByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/products/{id:guid}", async (
                Guid id,
                IQueryHandler<GetProductByIdQuery, GetProductByIdResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetProductByIdQuery(id), cancellationToken);
                return result.Match(Results.Ok);
            })
            .WithName("GetProductById")
            .WithTags("Products");
}
