using Microsoft.EntityFrameworkCore;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Results;
using MyApp.Api.Features.Products.CreateProduct;
using MyApp.Api.Features.Products.DeleteProduct;
using MyApp.Api.Features.Products.GetProductById;
using MyApp.Api.Features.Products.GetProducts;
using MyApp.Api.Features.Products.UpdateProduct;
using MyApp.Api.Persistence;
using static MyApp.Api.Tests.TestApp;

namespace MyApp.Api.Tests.Products;

public sealed class ProductHandlerTests : IAsyncLifetime
{
    private TestApp _app = null!;

    public async ValueTask InitializeAsync() => _app = await CreateAsync();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private Task<Result<CreateProductResponse>> Create(string name = "Keyboard", decimal price = 89.99m) =>
        _app.Get<ICommandHandler<CreateProductCommand, CreateProductResponse>>()
            .Handle(new CreateProductCommand(name, "Mechanical", price), Ct);

    private Task<Result<IReadOnlyList<GetProductsResponse>>> List() =>
        _app.Get<IQueryHandler<GetProductsQuery, IReadOnlyList<GetProductsResponse>>>()
            .Handle(new GetProductsQuery(), Ct);

    [Fact]
    public async Task CreateProduct_persists_a_valid_product()
    {
        var result = await Create();

        Assert.True(result.IsSuccess);
        var saved = await _app.Get<AppDbContext>().Products.SingleAsync(Ct);
        Assert.Equal(result.Value.Id, saved.Id);
        Assert.Equal("Keyboard", saved.Name);
        Assert.Equal(89.99m, saved.Price);
    }

    [Theory]
    [InlineData("", 10, "Product.NameRequired")]
    [InlineData("Keyboard", 0, "Product.PriceInvalid")]
    [InlineData("", -1, "Validation.Failed")]
    public async Task CreateProduct_rejects_invalid_input_and_saves_nothing(string name, decimal price, string expectedCode)
    {
        var result = await Create(name, price);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.False(await _app.Get<AppDbContext>().Products.AnyAsync(Ct));
    }

    [Fact]
    public async Task CreateProduct_invalidates_the_cached_product_list()
    {
        Assert.Empty((await List()).Value); // primes the cache with an empty list

        await Create();

        Assert.Single((await List()).Value);
    }

    [Fact]
    public async Task GetProductById_returns_not_found_for_unknown_id()
    {
        var result = await _app.Get<IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>>()
            .Handle(new GetProductByIdQuery(Guid.NewGuid()), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetProductById_returns_the_product()
    {
        var created = (await Create()).Value;

        var result = await _app.Get<IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>>()
            .Handle(new GetProductByIdQuery(created.Id), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Keyboard", result.Value.Name);
    }

    [Fact]
    public async Task UpdateProduct_changes_fields_and_sets_UpdatedAtUtc()
    {
        var created = (await Create()).Value;

        var result = await _app.Get<ICommandHandler<UpdateProductCommand, UpdateProductResponse>>()
            .Handle(new UpdateProductCommand(created.Id, "Keyboard v2", null, 99m), Ct);

        Assert.True(result.IsSuccess);
        var saved = await _app.Get<AppDbContext>().Products.SingleAsync(Ct);
        Assert.Equal("Keyboard v2", saved.Name);
        Assert.Equal(99m, saved.Price);
        Assert.NotNull(saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateProduct_returns_not_found_for_unknown_id()
    {
        var result = await _app.Get<ICommandHandler<UpdateProductCommand, UpdateProductResponse>>()
            .Handle(new UpdateProductCommand(Guid.NewGuid(), "Name", null, 1m), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task DeleteProduct_removes_the_product()
    {
        var created = (await Create()).Value;

        var result = await _app.Get<ICommandHandler<DeleteProductCommand>>()
            .Handle(new DeleteProductCommand(created.Id), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(await _app.Get<AppDbContext>().Products.AnyAsync(Ct));
    }

    [Fact]
    public async Task DeleteProduct_returns_not_found_for_unknown_id()
    {
        var result = await _app.Get<ICommandHandler<DeleteProductCommand>>()
            .Handle(new DeleteProductCommand(Guid.NewGuid()), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
