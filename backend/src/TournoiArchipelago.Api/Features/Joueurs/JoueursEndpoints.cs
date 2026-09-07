using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Joueurs;

public static class JoueursEndpoints
{
    public static void MapJoueursEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/joueurs").WithTags("Joueurs");

        groupe.MapGet("/", (ReferentielService service, CancellationToken annulation) =>
            service.ListerJoueursAsync(annulation))
        .WithSummary("Liste les joueurs par ordre alphabetique.");

        groupe.MapPost("/", async (
            JoueurUpsertRequest requete,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var joueur = await service.CreerJoueurAsync(requete, annulation);
            return Results.Created($"/api/joueurs/{joueur.Id}", joueur);
        })
        .RequireAuthorization()
        .WithSummary("Ajoute un joueur.")
        .Produces<JoueurDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        groupe.MapPut("/{id:int}", async (
            int id,
            JoueurUpsertRequest requete,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var joueur = await service.ModifierJoueurAsync(id, requete, annulation);
            return joueur is null ? Results.NotFound() : Results.Ok(joueur);
        })
        .RequireAuthorization()
        .WithSummary("Renomme un joueur.")
        .Produces<JoueurDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        groupe.MapDelete("/{id:int}", async (
            int id,
            ReferentielService service,
            CancellationToken annulation) =>
        {
            var supprime = await service.SupprimerJoueurAsync(id, annulation);
            return supprime ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization()
        .WithSummary("Supprime un joueur, s'il n'a ni equipe ni resultat.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
    }
}
