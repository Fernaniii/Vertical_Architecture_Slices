namespace MyApp.Api.Domain;

public sealed class Product : Entity
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    private Product()
    {
        // Required by EF Core.
        Name = string.Empty;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public DateTime CreatedAtUtc { get; private init; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static Product Create(string name, string? description, decimal price)
    {
        var product = new Product { CreatedAtUtc = DateTime.UtcNow };
        product.Apply(name, description, price);
        return product;
    }

    public void Update(string name, string? description, decimal price)
    {
        Apply(name, description, price);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void Apply(string name, string? description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new DomainException($"Product name cannot exceed {MaxNameLength} characters.");
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            throw new DomainException($"Product description cannot exceed {MaxDescriptionLength} characters.");
        }

        if (price <= 0)
        {
            throw new DomainException("Product price must be greater than zero.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
    }
}
