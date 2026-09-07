using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class ApiRawSnapshotConfiguration : IEntityTypeConfiguration<ApiRawSnapshot>
{
    public void Configure(EntityTypeBuilder<ApiRawSnapshot> builder)
    {
        builder.ToTable("ApiRawSnapshots");

        builder.Property(a => a.Provider).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Endpoint).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ExternalEntityId).HasMaxLength(100);
        builder.Property(a => a.PayloadJson).IsRequired();

        builder.HasIndex(a => new { a.Provider, a.Endpoint, a.ExternalEntityId, a.CapturedAt });
    }
}
