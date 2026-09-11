using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TournoiArchipelago.Api.Data;
using TournoiArchipelago.Api.Features.Auth;
using TournoiArchipelago.Api.Features.Equipes;
using TournoiArchipelago.Api.Features.Jeux;
using TournoiArchipelago.Api.Features.Joueurs;
using TournoiArchipelago.Api.Features.Matchs;
using TournoiArchipelago.Api.Features.Stats;
using TournoiArchipelago.Api.Infrastructure;
using TournoiArchipelago.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------------------
builder.Services.AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Persistance -------------------------------------------------------------------------
// La cible est un SQL Server local : pas de reprise sur erreur transitoire. L'activer
// (sql => sql.EnableRetryOnFailure()) exigerait de rendre les delegues passes a
// CreateExecutionStrategy() idempotents, notamment MatchService.ModifierAsync.
builder.Services.AddDbContext<TournoiDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Tournoi")));

// --- Services metier ---------------------------------------------------------------------
builder.Services.AddScoped<ReferentielService>();
builder.Services.AddScoped<EquipeService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<ImportLogService>();
builder.Services.AddSingleton<AuthService>();

// --- Authentification --------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// La validation est configuree depuis IOptions plutot qu'en lisant builder.Configuration ici :
// les surcharges de configuration ajoutees apres la creation du builder (tests, variables
// d'environnement injectees tardivement) doivent etre prises en compte.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// --- Presentation ------------------------------------------------------------------------
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Les enums voyagent en clair (QUALIFICATION / TOURNOI) pour rester lisibles cote client.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GestionnaireExceptions>();

var originesAutorisees = builder.Configuration
    .GetSection("Cors:OriginesAutorisees")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(politique =>
{
    if (originesAutorisees.Length > 0)
    {
        politique.WithOrigins(originesAutorisees).AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tournoi Archipelago",
        Version = "v1",
        Description = "Saisie des matchs et statistiques du tournoi.",
    });

    var schemaJwt = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Jeton obtenu via POST /api/auth/login.",
        Reference = new OpenApiReference { Id = JwtBearerDefaults.AuthenticationScheme, Type = ReferenceType.SecurityScheme },
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, schemaJwt);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [schemaJwt] = [] });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tournoi Archipelago v1"));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { statut = "ok" }))
    .WithTags("Diagnostic")
    .WithSummary("Sonde de disponibilite.");

app.MapAuthEndpoints();
app.MapJoueursEndpoints();
app.MapJeuxEndpoints();
app.MapEquipesEndpoints();
app.MapMatchsEndpoints();
app.MapImportLogEndpoints();
app.MapStatsEndpoints();

app.Run();
