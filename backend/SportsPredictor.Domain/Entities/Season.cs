using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

public class Season : Entity
{
    public required Guid CompetitionId { get; set; }

    public required string Name { get; set; }

    public required DateOnly StartDate { get; set; }

    public required DateOnly EndDate { get; set; }

    public Competition? Competition { get; set; }
}
