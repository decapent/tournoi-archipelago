using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournoiArchipelago.Api.Migrations
{
    /// <inheritdoc />
    public partial class RafaleDansLaCourbe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "est_rafale",
                table: "CheckHorodate",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "est_rafale",
                table: "CheckHorodate");
        }
    }
}
