using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class MatchEquipeEtSaisieProgressive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu");

            migrationBuilder.AlterColumn<int>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "est_capitaine",
                table: "EquipeJoueur",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MatchEquipe",
                columns: table => new
                {
                    match_id = table.Column<int>(type: "int", nullable: false),
                    equipe_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEquipe", x => new { x.match_id, x.equipe_id });
                    table.ForeignKey(
                        name: "FK_MatchEquipe_Equipe",
                        column: x => x.equipe_id,
                        principalTable: "Equipe",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_MatchEquipe_Match",
                        column: x => x.match_id,
                        principalTable: "Match",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu",
                sql: "[temps_final_secs] IS NULL OR [temps_final_secs] > 0");

            migrationBuilder.CreateIndex(
                name: "UQ_EquipeJoueur_capitaine",
                table: "EquipeJoueur",
                column: "equipe_id",
                unique: true,
                filter: "[est_capitaine] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEquipe_equipe_id",
                table: "MatchEquipe",
                column: "equipe_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchEquipe");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu");

            migrationBuilder.DropIndex(
                name: "UQ_EquipeJoueur_capitaine",
                table: "EquipeJoueur");

            migrationBuilder.DropColumn(
                name: "est_capitaine",
                table: "EquipeJoueur");

            migrationBuilder.AlterColumn<int>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu",
                sql: "[temps_final_secs] > 0");
        }
    }
}
