using Microsoft.OpenApi;
using MyApp.Api.Common.Http;
using static MyApp.Api.Tests.TestApp;

namespace MyApp.Api.Tests.Http;

public sealed class BearerSecuritySchemeTransformerTests
{
    [Fact]
    public async Task Adds_a_JWT_bearer_scheme_and_requires_it_globally()
    {
        var document = new OpenApiDocument();

        await new BearerSecuritySchemeTransformer().TransformAsync(document, null!, Ct);

        var scheme = Assert.Single(document.Components!.SecuritySchemes!);
        Assert.Equal("Bearer", scheme.Key);
        Assert.Equal(SecuritySchemeType.Http, scheme.Value.Type);
        Assert.Equal("bearer", scheme.Value.Scheme);
        Assert.Equal("JWT", scheme.Value.BearerFormat);

        var requirement = Assert.Single(document.Security!);
        Assert.Equal("Bearer", Assert.Single(requirement).Key.Reference.Id);
    }
}
