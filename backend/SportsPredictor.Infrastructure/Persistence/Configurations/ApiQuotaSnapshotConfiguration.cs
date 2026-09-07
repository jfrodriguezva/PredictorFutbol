using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class ApiQuotaSnapshotConfiguration : IEntityTypeConfiguration<ApiQuotaSnapshot>
{
    public void Configure(EntityTypeBuilder<ApiQuotaSnapshot> builder)
    {
        builder.ToTable("ApiQuotaSnapshots");

        builder.Property(a => a.Provider).IsRequired().HasMaxLength(50);

        builder.HasIndex(a => new { a.Provider, a.CapturedAt });
    }
}
