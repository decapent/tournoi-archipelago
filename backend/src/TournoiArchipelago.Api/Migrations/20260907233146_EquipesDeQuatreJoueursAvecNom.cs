using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class EquipesDeQuatreJoueursAvecNom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipe_Joueur1",
                table: "Equipe");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipe_Joueur2",
                table: "Equipe");

            migrationBuilder.DropIndex(
                name: "IX_Equipe_joueur2_id",
                table: "Equipe");

            migrationBuilder.DropIndex(
                name: "UQ_Equipe_joueurs",
                table: "Equipe");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipe_membres_distincts",
                table: "Equipe");

            migrationBuilder.DropColumn(
                name: "joueur1_id",
                table: "Equipe");

            migrationBuilder.DropColumn(
                name: "joueur2_id",
                table: "Equipe");

            migrationBuilder.AddColumn<string>(
                name: "nom",
                table: "Equipe",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EquipeJoueur",
                columns: table => new
                {
                    equipe_id = table.Column<int>(type: "int", nullable: false),
                    joueur_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipeJoueur", x => new { x.equipe_id, x.joueur_id });
                    table.ForeignKey(
                        name: "FK_EquipeJoueur_Equipe",
                        column: x => x.equipe_id,
                        principalTable: "Equipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquipeJoueur_Joueur",
                        column: x => x.joueur_id,
                        principalTable: "Joueur",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Equipe_nom",
                table: "Equipe",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EquipeJoueur_joueur",
                table: "EquipeJoueur",
                column: "joueur_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipeJoueur");

            migrationBuilder.DropIndex(
                name: "UQ_Equipe_nom",
                table: "Equipe");

            migrationBuilder.DropColumn(
                name: "nom",
                table: "Equipe");

            migrationBuilder.AddColumn<int>(
                name: "joueur1_id",
                table: "Equipe",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "joueur2_id",
                table: "Equipe",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Equipe_joueur2_id",
                table: "Equipe",
                column: "joueur2_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Equipe_joueurs",
                table: "Equipe",
                columns: new[] { "joueur1_id", "joueur2_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipe_membres_distincts",
                table: "Equipe",
                sql: "[joueur1_id] <> [joueur2_id]");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipe_Joueur1",
                table: "Equipe",
                column: "joueur1_id",
                principalTable: "Joueur",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipe_Joueur2",
                table: "Equipe",
                column: "joueur2_id",
                principalTable: "Joueur",
                principalColumn: "id");
        }
    }
}
