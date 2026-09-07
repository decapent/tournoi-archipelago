using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Data;

/// <summary>
/// Jeux inseres a la creation du schema, pour ne pas partir d'une liste vide.
/// La liste reste modifiable depuis le panneau d'admin.
/// </summary>
public static class SeedData
{
    public static readonly Jeu[] Jeux =
    [
        new() { Id = 1, Nom = "A Link to the Past" },
        new() { Id = 2, Nom = "Ocarina of Time" },
        new() { Id = 3, Nom = "Super Metroid" },
        new() { Id = 4, Nom = "Super Mario World" },
        new() { Id = 5, Nom = "Hollow Knight" },
        new() { Id = 6, Nom = "Timespinner" },
        new() { Id = 7, Nom = "Risk of Rain 2" },
        new() { Id = 8, Nom = "Factorio" },
        new() { Id = 9, Nom = "Stardew Valley" },
        new() { Id = 10, Nom = "Donkey Kong Country 3" },
        new() { Id = 11, Nom = "Pokemon Red and Blue" },
        new() { Id = 12, Nom = "Castlevania 64" },
    ];
}
