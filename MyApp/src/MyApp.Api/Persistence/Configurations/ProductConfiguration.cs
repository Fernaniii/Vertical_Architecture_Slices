using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Api.Domain;

namespace MyApp.Api.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Product.MaxDescriptionLength);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
    }
}
