using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaWebsite.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservationEmail",
                table: "Reservations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservationEmail",
                table: "Reservations");
        }
    }
}
