using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class AjoutNbChecksEtAjustements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchJeu_Match",
                table: "MatchJeu");

            migrationBuilder.DropIndex(
                name: "IX_Equipe_joueur1_id",
                table: "Equipe");

            // SQL Server refuse la conversion directe de time vers int : la colonne est
            // supprimee puis recreee. Sans perte de donnees, la table etant vide.
            migrationBuilder.DropColumn(
                name: "temps_final_secs",
                table: "MatchJeu");

            migrationBuilder.AddColumn<int>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "seed",
                table: "MatchJeu",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "nb_checks",
                table: "MatchJeu",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "Match",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "nom",
                table: "Joueur",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "nom",
                table: "Jeu",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Passer une colonne en IDENTITY exige de la recreer. La cle primaire est donc
            // retiree le temps de l'operation. Sans perte de donnees, la table etant vide.
            migrationBuilder.DropPrimaryKey(
                name: "PK_Equipe",
                table: "Equipe");

            migrationBuilder.DropColumn(
                name: "id",
                table: "Equipe");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "Equipe",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Equipe",
                table: "Equipe",
                column: "id");

            migrationBuilder.InsertData(
                table: "Jeu",
                columns: new[] { "id", "nom" },
                values: new object[,]
                {
                    { 1, "A Link to the Past" },
                    { 2, "Ocarina of Time" },
                    { 3, "Super Metroid" },
                    { 4, "Super Mario World" },
                    { 5, "Hollow Knight" },
                    { 6, "Timespinner" },
                    { 7, "Risk of Rain 2" },
                    { 8, "Factorio" },
                    { 9, "Stardew Valley" },
                    { 10, "Donkey Kong Country 3" },
                    { 11, "Pokemon Red and Blue" },
                    { 12, "Castlevania 64" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_MatchJeu_match_joueur",
                table: "MatchJeu",
                columns: new[] { "match_id", "joueur_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Match_date",
                table: "Match",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "UQ_Joueur_nom",
                table: "Joueur",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Jeu_nom",
                table: "Jeu",
                column: "nom",
                unique: true);

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
                name: "FK_MatchJeu_Match",
                table: "MatchJeu",
                column: "match_id",
                principalTable: "Match",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchJeu_Match",
                table: "MatchJeu");

            migrationBuilder.DropIndex(
                name: "UQ_MatchJeu_match_joueur",
                table: "MatchJeu");

            migrationBuilder.DropIndex(
                name: "IX_Match_date",
                table: "Match");

            migrationBuilder.DropIndex(
                name: "UQ_Joueur_nom",
                table: "Joueur");

            migrationBuilder.DropIndex(
                name: "UQ_Jeu_nom",
                table: "Jeu");

            migrationBuilder.DropIndex(
                name: "UQ_Equipe_joueurs",
                table: "Equipe");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipe_membres_distincts",
                table: "Equipe");

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Jeu",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DropColumn(
                name: "nb_checks",
                table: "MatchJeu");

            migrationBuilder.DropColumn(
                name: "temps_final_secs",
                table: "MatchJeu");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "time",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "seed",
                table: "MatchJeu",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "Match",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "nom",
                table: "Joueur",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "nom",
                table: "Jeu",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.DropPrimaryKey(
                name: "PK_Equipe",
                table: "Equipe");

            migrationBuilder.DropColumn(
                name: "id",
                table: "Equipe");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "Equipe",
                type: "int",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Equipe",
                table: "Equipe",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_Equipe_joueur1_id",
                table: "Equipe",
                column: "joueur1_id");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchJeu_Match",
                table: "MatchJeu",
                column: "match_id",
                principalTable: "Match",
                principalColumn: "id");
        }
    }
}
