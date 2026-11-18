using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Domain;
using MyApp.Api.Persistence;

namespace MyApp.Api.Features.Products.UpdateProduct;

public sealed record UpdateProductRequest(string Name, string? Description, decimal Price);

public sealed record UpdateProductCommand(Guid Id, string Name, string? Description, decimal Price)
    : ICommand<UpdateProductResponse>;

public sealed record UpdateProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

internal sealed class UpdateProductValidator : IValidator<UpdateProductCommand>
{
    public IEnumerable<Error> Validate(UpdateProductCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return Error.Validation("Product.NameRequired", "Name is required.");
        }
        else if (command.Name.Length > Product.MaxNameLength)
        {
            yield return Error.Validation("Product.NameTooLong", $"Name cannot exceed {Product.MaxNameLength} characters.");
        }

        if (command.Description is { Length: > Product.MaxDescriptionLength })
        {
            yield return Error.Validation("Product.DescriptionTooLong", $"Description cannot exceed {Product.MaxDescriptionLength} characters.");
        }

        if (command.Price <= 0)
        {
            yield return Error.Validation("Product.PriceInvalid", "Price must be greater than zero.");
        }
    }
}

/// <summary>Input is already validated by <see cref="UpdateProductValidator"/> (via the validation decorator).</summary>
internal sealed class UpdateProductHandler(AppDbContext db, HybridCache cache)
    : ICommandHandler<UpdateProductCommand, UpdateProductResponse>
{
    public async Task<Result<UpdateProductResponse>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (product is null)
        {
            return Error.NotFound("Product.NotFound", $"Product '{command.Id}' was not found.");
        }

        product.Update(command.Name, command.Description, command.Price);
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(ProductCache.Tag, cancellationToken);

        return new UpdateProductResponse(
            product.Id, product.Name, product.Description, product.Price, product.CreatedAtUtc, product.UpdatedAtUtc);
    }
}

internal sealed class UpdateProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/api/products/{id:guid}", async (
                Guid id,
                UpdateProductRequest request,
                ICommandHandler<UpdateProductCommand, UpdateProductResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateProductCommand(id, request.Name, request.Description, request.Price);
                var result = await handler.Handle(command, cancellationToken);
                return result.Match(Results.Ok);
            })
            .WithName("UpdateProduct")
            .WithTags("Products");
}
