using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Matchs;

public static class MatchsEndpoints
{
    public static void MapMatchsEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/matchs").WithTags("Matchs");

        groupe.MapGet("/", (
            MatchService service,
            CancellationToken annulation,
            TypeMatch? type = null,
            DateOnly? du = null,
            DateOnly? au = null) =>
            service.ListerAsync(type, du, au, annulation))
        .WithSummary("Historique des matchs, du plus recent au plus ancien.");

        groupe.MapGet("/{id:int}", async (
            int id,
            MatchService service,
            CancellationToken annulation) =>
        {
            var match = await service.ObtenirAsync(id, annulation);
            return match is null ? Results.NotFound() : Results.Ok(match);
        })
        .WithSummary("Detail d'un match : les quatre resultats et le classement des deux equipes.")
        .Produces<MatchDetailDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        groupe.MapPost("/", async (
            MatchUpsertRequest requete,
            MatchService service,
            CancellationToken annulation) =>
        {
            var match = await service.CreerAsync(requete, annulation);
            return Results.Created($"/api/matchs/{match.Id}", match);
        })
        .RequireAuthorization()
        .WithSummary("Enregistre un match complet : deux equipes et quatre resultats.")
        .Produces<MatchDetailDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        groupe.MapPut("/{id:int}", async (
            int id,
            MatchUpsertRequest requete,
            MatchService service,
            CancellationToken annulation) =>
        {
            var match = await service.ModifierAsync(id, requete, annulation);
            return match is null ? Results.NotFound() : Results.Ok(match);
        })
        .RequireAuthorization()
        .WithSummary("Corrige un match deja saisi. Les quatre resultats sont remplaces.")
        .Produces<MatchDetailDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        groupe.MapDelete("/{id:int}", async (
            int id,
            MatchService service,
            CancellationToken annulation) =>
        {
            var supprime = await service.SupprimerAsync(id, annulation);
            return supprime ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization()
        .WithSummary("Supprime un match et ses resultats.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
