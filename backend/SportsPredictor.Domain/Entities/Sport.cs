using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

public class Sport : Entity
{
    public required string Name { get; set; }
}
