using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> builder)
    {
        builder.ToTable("ModelVersions");

        builder.Property(m => m.ModelName).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Sport).IsRequired().HasMaxLength(50);
        builder.Property(m => m.Version).IsRequired().HasMaxLength(50);
        builder.Property(m => m.Algorithm).IsRequired().HasMaxLength(50);
        builder.Property(m => m.ArtifactPath).IsRequired().HasMaxLength(500);

        // A model version, once trained, is a permanent record — never overwritten.
        builder.HasIndex(m => new { m.ModelName, m.Version }).IsUnique();
    }
}
