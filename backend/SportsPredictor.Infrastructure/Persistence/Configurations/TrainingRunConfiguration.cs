using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence.Configurations;

public class TrainingRunConfiguration : IEntityTypeConfiguration<TrainingRun>
{
    public void Configure(EntityTypeBuilder<TrainingRun> builder)
    {
        builder.ToTable("TrainingRuns");

        builder.Property(t => t.TrainingParametersJson).IsRequired();

        builder.HasOne(t => t.ModelVersion)
            .WithMany()
            .HasForeignKey(t => t.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ModelVersionId);
    }
}
