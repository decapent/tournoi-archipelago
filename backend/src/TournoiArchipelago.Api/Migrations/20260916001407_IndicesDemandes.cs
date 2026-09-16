using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDemandes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "nb_indices_demandes",
                table: "MatchJeu",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nb_indices_obtenus",
                table: "MatchJeu",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IndiceHorodate",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_id = table.Column<int>(type: "int", nullable: false),
                    joueur_id = table.Column<int>(type: "int", nullable: false),
                    secondes = table.Column<int>(type: "int", nullable: false),
                    resultat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    points_restants = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndiceHorodate", x => x.id);
                    table.ForeignKey(
                        name: "FK_IndiceHorodate_Joueur",
                        column: x => x.joueur_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_IndiceHorodate_Match",
                        column: x => x.match_id,
                        principalTable: "Match",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndiceHorodate_joueur_id",
                table: "IndiceHorodate",
                column: "joueur_id");

            migrationBuilder.CreateIndex(
                name: "IX_IndiceHorodate_match_joueur",
                table: "IndiceHorodate",
                columns: new[] { "match_id", "joueur_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndiceHorodate");

            migrationBuilder.DropColumn(
                name: "nb_indices_demandes",
                table: "MatchJeu");

            migrationBuilder.DropColumn(
                name: "nb_indices_obtenus",
                table: "MatchJeu");
        }
    }
}
