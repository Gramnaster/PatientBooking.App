using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Api.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentDateUtcToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ClinicId_AppointmentStartUtc_BookingNumber",
                table: "Bookings");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AppointmentDateUtc",
                table: "Bookings",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClinicId_AppointmentDateUtc_BookingNumber",
                table: "Bookings",
                columns: new[] { "ClinicId", "AppointmentDateUtc", "BookingNumber" },
                unique: true,
                filter: "[DeletedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ClinicId_AppointmentDateUtc_BookingNumber",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AppointmentDateUtc",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClinicId_AppointmentStartUtc_BookingNumber",
                table: "Bookings",
                columns: new[] { "ClinicId", "AppointmentStartUtc", "BookingNumber" },
                unique: true,
                filter: "[DeletedAtUtc] IS NULL");
        }
    }
}
