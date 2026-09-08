using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Api.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminDateTimeOffsetToUpdatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DateTimeOffset",
                table: "Admins",
                newName: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Admins",
                newName: "DateTimeOffset");
        }
    }
}
