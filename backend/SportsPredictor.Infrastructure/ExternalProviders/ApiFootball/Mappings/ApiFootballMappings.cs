using SportsPredictor.Application.ExternalData;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Mappings;

internal static class ApiFootballMappings
{
    public static CountryDto ToDto(this ApiFootballCountryModel model) =>
        new(model.Name, model.Code, model.Flag);

    public static LeagueDto ToDto(this ApiFootballLeagueEntryModel model)
    {
        var currentSeasonYear = model.Seasons.FirstOrDefault(s => s.Current)?.Year;

        return new LeagueDto(
            ExternalLeagueId: model.League.Id,
            Name: model.League.Name,
            Type: model.League.Type,
            CountryName: model.Country?.Name,
            CurrentSeasonYear: currentSeasonYear);
    }
}
