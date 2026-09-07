using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jeu",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nom = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jeu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Joueur",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nom = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_joueur", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Match",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Match", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Equipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    joueur1_id = table.Column<int>(type: "int", nullable: false),
                    joueur2_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipe", x => x.id);
                    table.ForeignKey(
                        name: "FK_Equipe_Joueur1",
                        column: x => x.joueur1_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Equipe_Joueur2",
                        column: x => x.joueur2_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "MatchJeu",
                columns: table => new
                {
                    match_id = table.Column<int>(type: "int", nullable: false),
                    jeu_id = table.Column<int>(type: "int", nullable: false),
                    joueur_id = table.Column<int>(type: "int", nullable: false),
                    seed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    total_checks = table.Column<int>(type: "int", nullable: true),
                    temps_final_secs = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchJeu", x => new { x.match_id, x.jeu_id, x.joueur_id });
                    table.ForeignKey(
                        name: "FK_MatchJeu_Jeu",
                        column: x => x.jeu_id,
                        principalTable: "Jeu",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_MatchJeu_Joueur",
                        column: x => x.joueur_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_MatchJeu_Match",
                        column: x => x.match_id,
                        principalTable: "Match",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipe_joueur1_id",
                table: "Equipe",
                column: "joueur1_id");

            migrationBuilder.CreateIndex(
                name: "IX_Equipe_joueur2_id",
                table: "Equipe",
                column: "joueur2_id");

            migrationBuilder.CreateIndex(
                name: "IX_MatchJeu_jeu_id",
                table: "MatchJeu",
                column: "jeu_id");

            migrationBuilder.CreateIndex(
                name: "IX_MatchJeu_joueur_id",
                table: "MatchJeu",
                column: "joueur_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Equipe");

            migrationBuilder.DropTable(
                name: "MatchJeu");

            migrationBuilder.DropTable(
                name: "Jeu");

            migrationBuilder.DropTable(
                name: "Joueur");

            migrationBuilder.DropTable(
                name: "Match");
        }
    }
}
