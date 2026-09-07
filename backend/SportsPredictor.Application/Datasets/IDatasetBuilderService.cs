namespace SportsPredictor.Application.Datasets;

/// <summary>
/// Corresponds to CLAUDE.md section 18's "Dataset Builder": the boundary between
/// C#-owned SQLite persistence and the Python ML service. Python never touches SQLite
/// directly — it only ever consumes what this service exports (CLAUDE.md section 1:
/// "C# es el orquestador principal").
/// </summary>
public interface IDatasetBuilderService
{
    /// <summary>
    /// Computes and persists FeatureValue snapshots (CLAUDE.md section 22's catalog,
    /// see <see cref="FootballFeatureNames"/>) for every finished match in the tracked
    /// competition that doesn't have features yet. Anti-leakage guaranteed: every
    /// feature is computed strictly from data timestamped before that match's kickoff.
    /// </summary>
    Task<BuildFeaturesResultDto> BuildFeaturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Exports a reproducible, flat CSV — one row per finished match with features
    /// (latest FeatureValue per name) pivoted into columns plus the actual 1X2 result.
    /// This is the only way the Python ML service should ever see this data.
    /// </summary>
    Task<string> ExportDatasetCsvAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="ExportDatasetCsvAsync"/> but pools every finished match with
    /// built features across ALL tracked competitions (every league/cup/season), not
    /// just one competition. A single league's history is too small a sample for a
    /// reliable 1X2 classifier; pooling adds sample size and cross-league generalization
    /// without any leakage, since every row's features were already computed using only
    /// data strictly before that specific match's own kickoff.
    /// </summary>
    Task<string> ExportGlobalDatasetCsvAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Computes (without persisting) the same feature catalog for a single match,
    /// regardless of its status — this is what powers a real prediction for an
    /// upcoming match, using only whatever standings/form/injuries/odds exist right now.
    /// </summary>
    Task<IReadOnlyDictionary<string, double>> ComputeFeaturesForPredictionAsync(Guid matchId, CancellationToken cancellationToken);
}
