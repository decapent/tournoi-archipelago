using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProgressionDesChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckHorodate",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    match_id = table.Column<int>(type: "int", nullable: false),
                    joueur_id = table.Column<int>(type: "int", nullable: false),
                    secondes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckHorodate", x => x.id);
                    table.ForeignKey(
                        name: "FK_CheckHorodate_Joueur",
                        column: x => x.joueur_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_CheckHorodate_Match",
                        column: x => x.match_id,
                        principalTable: "Match",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckHorodate_joueur_id",
                table: "CheckHorodate",
                column: "joueur_id");

            migrationBuilder.CreateIndex(
                name: "IX_CheckHorodate_match_joueur",
                table: "CheckHorodate",
                columns: new[] { "match_id", "joueur_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckHorodate");
        }
    }
}
