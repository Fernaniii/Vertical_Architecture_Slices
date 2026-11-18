using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Decorators;
using MyApp.Api.Common.Results;
using MyApp.Api.Features.Products.CreateProduct;
using NSubstitute;
using static MyApp.Api.Tests.TestApp;

namespace MyApp.Api.Tests.Decorators;

public sealed class ValidationDecoratorTests
{
    private readonly ICommandHandler<CreateProductCommand, CreateProductResponse> _inner =
        Substitute.For<ICommandHandler<CreateProductCommand, CreateProductResponse>>();

    private ValidationCommandHandler<CreateProductCommand, CreateProductResponse> CreateSut() =>
        new(_inner, [new CreateProductValidator()]);

    [Fact]
    public async Task Invalid_command_short_circuits_before_the_handler()
    {
        var result = await CreateSut().Handle(new CreateProductCommand("", null, 1m), Ct);

        Assert.Equal("Product.NameRequired", result.Error.Code);
        await _inner.DidNotReceiveWithAnyArgs().Handle(default!, Ct);
    }

    [Fact]
    public async Task Valid_command_is_passed_to_the_handler()
    {
        var command = new CreateProductCommand("Keyboard", null, 1m);
        var expected = Result.Success(new CreateProductResponse(Guid.NewGuid(), "Keyboard", null, 1m, DateTime.UtcNow));
        _inner.Handle(command, Ct).Returns(expected);

        var result = await CreateSut().Handle(command, Ct);

        Assert.Same(expected, result);
    }
}
