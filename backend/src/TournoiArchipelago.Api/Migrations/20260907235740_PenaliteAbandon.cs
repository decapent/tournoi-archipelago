using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class PenaliteAbandon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.Sql("""
                DECLARE @contrainte sysname = (
                    SELECT dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c
                        ON c.object_id = dc.parent_object_id
                       AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'[MatchJeu]')
                      AND c.name = N'temps_final_secs');

                IF @contrainte IS NOT NULL
                    EXEC(N'ALTER TABLE [MatchJeu] DROP CONSTRAINT [' + @contrainte + N'];');
                """);

            migrationBuilder.AddColumn<bool>(
                name: "est_abandon",
                table: "MatchJeu",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu",
                sql: "[temps_final_secs] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchJeu_temps_positif",
                table: "MatchJeu");

            migrationBuilder.DropColumn(
                name: "est_abandon",
                table: "MatchJeu");

            migrationBuilder.AlterColumn<int>(
                name: "temps_final_secs",
                table: "MatchJeu",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
