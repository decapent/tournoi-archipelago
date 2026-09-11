using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Matchs;

public static class ImportLogEndpoints
{
    public static void MapImportLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api").WithTags("Journaux");

        groupe.MapPost("/logs/analyse", (AnalyseLogRequest requete) =>
            Results.Ok(ImportLogService.Analyser(requete)))
        .RequireAuthorization()
        .WithSummary("Analyse un journal Archipelago sans rien enregistrer, pour preparer l'import.")
        .Produces<RapportLogDto>()
        .ProducesValidationProblem();

        groupe.MapPost("/matchs/{matchId:int}/equipes/{equipeId:int}/log", async (
            int matchId,
            int equipeId,
            ImportLogRequest requete,
            ImportLogService service,
            CancellationToken annulation) =>
        {
            var match = await service.ImporterAsync(matchId, equipeId, requete, annulation);
            return match is null ? Results.NotFound() : Results.Ok(match);
        })
        .RequireAuthorization()
        .WithSummary("Reporte un journal sur les resultats d'une equipe : checks, total et temps.")
        .Produces<MatchDetailDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        groupe.MapGet("/matchs/{matchId:int}/progression", async (
            int matchId,
            ImportLogService service,
            CancellationToken annulation) =>
        {
            var progression = await service.ProgressionAsync(matchId, annulation);
            return progression is null ? Results.NotFound() : Results.Ok(progression);
        })
        .WithSummary("Progression des checks dans le temps, par joueur.")
        .Produces<ProgressionMatchDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
