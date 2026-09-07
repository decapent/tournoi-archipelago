using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TournoiArchipelago.Api.Infrastructure;

/// <summary>
/// Traduit les exceptions metier en reponses ProblemDetails, sans exposer de trace technique.
/// </summary>
public class GestionnaireExceptions(
    IProblemDetailsService problemDetails,
    ILogger<GestionnaireExceptions> logger) : IExceptionHandler
{
    /// <summary>Codes SQL Server pour une violation de contrainte d'unicite.</summary>
    private static readonly int[] CodesUnicite = [2601, 2627];

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexte,
        Exception exception,
        CancellationToken annulation)
    {
        switch (exception)
        {
            case RequeteInvalideException invalide:
                contexte.Response.StatusCode = StatusCodes.Status400BadRequest;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = contexte,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Requete invalide",
                        Detail = invalide.Message,
                        Extensions = { ["errors"] = invalide.Erreurs },
                    },
                });
                return true;

            case DbUpdateException update when EstViolationUnicite(update):
                logger.LogWarning(update, "Violation de contrainte d'unicite.");
                contexte.Response.StatusCode = StatusCodes.Status409Conflict;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = contexte,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Doublon",
                        Detail = "Cet enregistrement existe deja.",
                    },
                });
                return true;

            default:
                return false;
        }
    }

    private static bool EstViolationUnicite(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && CodesUnicite.Contains(sql.Number);
}
