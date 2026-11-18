using Microsoft.Extensions.Caching.Hybrid;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Domain;
using MyApp.Api.Persistence;

namespace MyApp.Api.Features.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, string? Description, decimal Price)
    : ICommand<CreateProductResponse>;

public sealed record CreateProductResponse(Guid Id, string Name, string? Description, decimal Price, DateTime CreatedAtUtc);

internal sealed class CreateProductValidator : IValidator<CreateProductCommand>
{
    public IEnumerable<Error> Validate(CreateProductCommand command)
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

/// <summary>Input is already validated by <see cref="CreateProductValidator"/> (via the validation decorator).</summary>
internal sealed class CreateProductHandler(AppDbContext db, HybridCache cache)
    : ICommandHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<Result<CreateProductResponse>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = Product.Create(command.Name, command.Description, command.Price);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(ProductCache.Tag, cancellationToken);

        return new CreateProductResponse(product.Id, product.Name, product.Description, product.Price, product.CreatedAtUtc);
    }
}

internal sealed class CreateProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/products", async (
                CreateProductCommand command,
                ICommandHandler<CreateProductCommand, CreateProductResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(command, cancellationToken);
                return result.Match(response => Results.Created($"/api/products/{response.Id}", response));
            })
            .WithName("CreateProduct")
            .WithTags("Products");
}
