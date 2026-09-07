using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Equipes;

public static class EquipesEndpoints
{
    public static void MapEquipesEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/equipes").WithTags("Equipes");

        groupe.MapGet("/", (EquipeService service, CancellationToken annulation) =>
            service.ListerAsync(annulation))
        .WithSummary("Liste les equipes avec leurs deux membres.");

        groupe.MapGet("/{id:int}", async (
            int id,
            EquipeService service,
            CancellationToken annulation) =>
        {
            var equipe = await service.ObtenirAsync(id, annulation);
            return equipe is null ? Results.NotFound() : Results.Ok(equipe);
        })
        .WithSummary("Detail d'une equipe.")
        .Produces<EquipeDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        groupe.MapPost("/", async (
            EquipeUpsertRequest requete,
            EquipeService service,
            CancellationToken annulation) =>
        {
            var equipe = await service.CreerAsync(requete, annulation);
            return Results.Created($"/api/equipes/{equipe.Id}", equipe);
        })
        .RequireAuthorization()
        .WithSummary("Cree une equipe. Un joueur ne peut appartenir qu'a une seule equipe.")
        .Produces<EquipeDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        groupe.MapPut("/{id:int}", async (
            int id,
            EquipeUpsertRequest requete,
            EquipeService service,
            CancellationToken annulation) =>
        {
            var equipe = await service.ModifierAsync(id, requete, annulation);
            return equipe is null ? Results.NotFound() : Results.Ok(equipe);
        })
        .RequireAuthorization()
        .WithSummary("Change les membres d'une equipe.")
        .Produces<EquipeDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        groupe.MapDelete("/{id:int}", async (
            int id,
            EquipeService service,
            CancellationToken annulation) =>
        {
            var supprime = await service.SupprimerAsync(id, annulation);
            return supprime ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization()
        .WithSummary("Supprime une equipe.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
