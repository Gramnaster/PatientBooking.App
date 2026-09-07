using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Api.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ConvertClinicOperatingHoursDayOfWeekToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicOperatingHours_ClinicId_DayOfWeek",
                table: "ClinicOperatingHours");

            migrationBuilder.AlterColumn<string>(
                name: "DayOfWeek",
                table: "ClinicOperatingHours",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // AlterColumn casts existing int values to their numeral text ('0'-'6'), not the
            // enum names EF now reads back. Rewrite them here or every existing row fails to
            // deserialize as DayOfWeek the moment the app queries this table.
            migrationBuilder.Sql("""
                UPDATE [dbo].[ClinicOperatingHours]
                SET [DayOfWeek] = CASE [DayOfWeek]
                    WHEN N'0' THEN N'Sunday'
                    WHEN N'1' THEN N'Monday'
                    WHEN N'2' THEN N'Tuesday'
                    WHEN N'3' THEN N'Wednesday'
                    WHEN N'4' THEN N'Thursday'
                    WHEN N'5' THEN N'Friday'
                    WHEN N'6' THEN N'Saturday'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOperatingHours_ClinicId_DayOfWeek",
                table: "ClinicOperatingHours",
                columns: new[] { "ClinicId", "DayOfWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicOperatingHours_ClinicId_DayOfWeek",
                table: "ClinicOperatingHours");

            migrationBuilder.Sql("""
                UPDATE [dbo].[ClinicOperatingHours]
                SET [DayOfWeek] = CASE [DayOfWeek]
                    WHEN N'Sunday' THEN N'0'
                    WHEN N'Monday' THEN N'1'
                    WHEN N'Tuesday' THEN N'2'
                    WHEN N'Wednesday' THEN N'3'
                    WHEN N'Thursday' THEN N'4'
                    WHEN N'Friday' THEN N'5'
                    WHEN N'Saturday' THEN N'6'
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "DayOfWeek",
                table: "ClinicOperatingHours",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(9)",
                oldMaxLength: 9);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOperatingHours_ClinicId_DayOfWeek",
                table: "ClinicOperatingHours",
                columns: new[] { "ClinicId", "DayOfWeek" },
                unique: true);
        }
    }
}
