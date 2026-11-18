namespace MyApp.Api.Features.Products;

/// <summary>
/// Cache keys and the invalidation tag for the Products feature. Lives at feature level (not inside a slice)
/// because the query slices write these entries and the command slices invalidate them.
/// </summary>
public static class ProductCache
{
    public const string Tag = "products";

    public const string ListKey = "products:list";

    public static string ByIdKey(Guid id) => $"products:{id}";
}
