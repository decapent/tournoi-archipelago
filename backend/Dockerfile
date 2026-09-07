# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY src/TournoiArchipelago.Api/TournoiArchipelago.Api.csproj src/TournoiArchipelago.Api/
RUN dotnet restore src/TournoiArchipelago.Api/TournoiArchipelago.Api.csproj

COPY src/ src/
RUN dotnet publish src/TournoiArchipelago.Api/TournoiArchipelago.Api.csproj \
        -c Release -o /app --no-restore

# Execution
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# curl n'est pas fourni par l'image runtime, et sert au healthcheck de docker-compose.
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Utilisateur non privilegie fourni par l'image.
USER $APP_UID

ENTRYPOINT ["dotnet", "TournoiArchipelago.Api.dll"]
