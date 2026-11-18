using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Persistence;

namespace MyApp.Api.Features.Products.GetProducts;

public sealed record GetProductsQuery : IQuery<IReadOnlyList<GetProductsResponse>>;

public sealed record GetProductsResponse(Guid Id, string Name, string? Description, decimal Price, DateTime CreatedAtUtc);

internal sealed class GetProductsHandler(AppDbContext db, HybridCache cache)
    : IQueryHandler<GetProductsQuery, IReadOnlyList<GetProductsResponse>>
{
    private static readonly string[] Tags = [ProductCache.Tag];

    public async Task<Result<IReadOnlyList<GetProductsResponse>>> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var products = await cache.GetOrCreateAsync(
            ProductCache.ListKey,
            db,
            static async (db, ct) => await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new GetProductsResponse(p.Id, p.Name, p.Description, p.Price, p.CreatedAtUtc))
                .ToListAsync(ct),
            tags: Tags,
            cancellationToken: cancellationToken);

        return products;
    }
}

internal sealed class GetProductsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/products", async (
                IQueryHandler<GetProductsQuery, IReadOnlyList<GetProductsResponse>> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetProductsQuery(), cancellationToken);
                return result.Match(Results.Ok);
            })
            .WithName("GetProducts")
            .WithTags("Products");
}
