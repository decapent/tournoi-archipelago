namespace TournoiArchipelago.Api.Services;

/// <summary>Critere de tri du classement general.</summary>
public enum TriClassement
{
    /// <summary>Victoires decroissantes, puis temps cumule croissant.</summary>
    Victoires = 0,

    /// <summary>Temps cumule croissant, puis victoires decroissantes.</summary>
    Temps = 1,
}
