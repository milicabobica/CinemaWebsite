using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaWebsite.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoupleSeatSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PairSeatId",
                table: "Seats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Seats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PairSeatId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Seats");
        }
    }
}
