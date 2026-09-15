using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Stats;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/stats").WithTags("Stats");

        groupe.MapGet("/classement", (
            StatsService service,
            CancellationToken annulation,
            TypeMatch? type = null,
            TriClassement tri = TriClassement.Victoires) =>
            service.ClassementAsync(type, tri, annulation))
        .WithSummary("Classement general par equipe, base sur le temps de completion du duo.");

        groupe.MapGet("/joueurs", (
            StatsService service,
            CancellationToken annulation,
            TypeMatch? type = null) =>
            service.StatsParJoueurAsync(type, annulation))
        .WithSummary("Statistiques agregees par joueur : seeds, temps, checks, abandons.");

        groupe.MapGet("/jeux", (
            StatsService service,
            CancellationToken annulation,
            TypeMatch? type = null) =>
            service.StatsParJeuAsync(type, annulation))
        .WithSummary("Statistiques agregees par jeu : parties, temps, checks, abandons.");
    }
}
