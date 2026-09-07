using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class NomsNonVides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF a cree cette contrainte en ajoutant la colonne nom (AddColumn defaultValue),
            // sans la declarer dans le modele : elle doit donc etre retiree a la main. C'est
            // elle qui permettait d'inserer une equipe sans nom.
            migrationBuilder.Sql("""
                DECLARE @contrainte sysname = (
                    SELECT dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c
                        ON c.object_id = dc.parent_object_id
                       AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'[Equipe]')
                      AND c.name = N'nom');

                IF @contrainte IS NOT NULL
                    EXEC(N'ALTER TABLE [Equipe] DROP CONSTRAINT [' + @contrainte + N'];');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Joueur_nom_non_vide",
                table: "Joueur",
                sql: "[nom] <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Jeu_nom_non_vide",
                table: "Jeu",
                sql: "[nom] <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipe_nom_non_vide",
                table: "Equipe",
                sql: "[nom] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Joueur_nom_non_vide",
                table: "Joueur");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Jeu_nom_non_vide",
                table: "Jeu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipe_nom_non_vide",
                table: "Equipe");
        }
    }
}
