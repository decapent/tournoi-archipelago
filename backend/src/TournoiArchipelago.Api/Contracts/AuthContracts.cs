namespace TournoiArchipelago.Api.Contracts;

public record LoginRequest(string? Username, string? Password);

public record LoginResponse(string Token, DateTimeOffset ExpireLe, string Username);

public record UtilisateurCourantResponse(string Username);
