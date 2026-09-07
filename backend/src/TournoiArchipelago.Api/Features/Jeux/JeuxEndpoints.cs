using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Jeux;

public static class JeuxEndpoints
{
    public static void MapJeuxEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/jeux").WithTags("Jeux");

        groupe.MapGet("/", (ReferentielService service, CancellationToken annulation) =>
            service.ListerJeuxAsync(annulation))
        .WithSummary("Liste les jeux par ordre alphabetique.");

        groupe.MapPost("/", async (
            JeuUpsertRequest requete,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var jeu = await service.CreerJeuAsync(requete, annulation);
            return Results.Created($"/api/jeux/{jeu.Id}", jeu);
        })
        .RequireAuthorization()
        .WithSummary("Ajoute un jeu.")
        .Produces<JeuDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        groupe.MapPut("/{id:int}", async (
            int id,
            JeuUpsertRequest requete,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var jeu = await service.ModifierJeuAsync(id, requete, annulation);
            return jeu is null ? Results.NotFound() : Results.Ok(jeu);
        })
        .RequireAuthorization()
        .WithSummary("Renomme un jeu.")
        .Produces<JeuDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        groupe.MapDelete("/{id:int}", async (
            int id,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var supprime = await service.SupprimerJeuAsync(id, annulation);
            return supprime ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization()
        .WithSummary("Supprime un jeu, s'il n'a jamais ete joue.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
    }
}
