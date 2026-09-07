using System.Security.Claims;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var groupe = routes.MapGroup("/api/auth").WithTags("Auth");

        groupe.MapPost("/login", (LoginRequest requete, AuthService auth) =>
        {
            var reponse = auth.Authentifier(requete);

            return reponse is null
                ? Results.Problem(
                    title: "Identifiants refuses",
                    detail: "Nom d'utilisateur ou mot de passe incorrect.",
                    statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(reponse);
        })
        .WithSummary("Ouvre une session d'admin et renvoie un jeton JWT.")
        .Produces<LoginResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        groupe.MapGet("/me", (ClaimsPrincipal utilisateur) =>
            Results.Ok(new UtilisateurCourantResponse(utilisateur.Identity?.Name ?? string.Empty)))
        .RequireAuthorization()
        .WithSummary("Verifie que le jeton courant est encore valide.")
        .Produces<UtilisateurCourantResponse>();
    }
}
