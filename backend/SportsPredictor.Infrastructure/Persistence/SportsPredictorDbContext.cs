using Microsoft.EntityFrameworkCore;
using SportsPredictor.Domain.Entities;

namespace SportsPredictor.Infrastructure.Persistence;

public class SportsPredictorDbContext : DbContext
{
    public SportsPredictorDbContext(DbContextOptions<SportsPredictorDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<StandingSnapshot> StandingSnapshots => Set<StandingSnapshot>();
    public DbSet<OddsSnapshot> OddsSnapshots => Set<OddsSnapshot>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<TrainingRun> TrainingRuns => Set<TrainingRun>();
    public DbSet<FeatureValue> FeatureValues => Set<FeatureValue>();
    public DbSet<TrackedCompetition> TrackedCompetitions => Set<TrackedCompetition>();
    public DbSet<DataAvailability> DataAvailabilities => Set<DataAvailability>();
    public DbSet<ApiQuotaSnapshot> ApiQuotaSnapshots => Set<ApiQuotaSnapshot>();
    public DbSet<ApiRawSnapshot> ApiRawSnapshots => Set<ApiRawSnapshot>();
    public DbSet<InjurySnapshot> InjurySnapshots => Set<InjurySnapshot>();
    public DbSet<LineupSnapshot> LineupSnapshots => Set<LineupSnapshot>();
    public DbSet<ApiFootballPredictionSnapshot> ApiFootballPredictionSnapshots => Set<ApiFootballPredictionSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SportsPredictorDbContext).Assembly);
    }
}
