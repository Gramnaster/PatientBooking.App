using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Api.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullMedicalRecordNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
              name: "IX_Patients_MedicalRecordNumber",
              table: "Patients");

            migrationBuilder.AlterColumn<string>(
                name: "MedicalRecordNumber",
                table: "Patients",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
              name: "IX_Patients_MedicalRecordNumber",
              table: "Patients",
              column: "MedicalRecordNumber",
              unique: true,
              filter: "[MedicalRecordNumber] IS NOT NULL");

            migrationBuilder.Sql("""
                UPDATE [dbo].[Patients]
                SET [MedicalRecordNumber] = NULL
                WHERE [MedicalRecordNumber] = N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
              name: "IX_Patients_MedicalRecordNumber",
              table: "Patients");

            migrationBuilder.Sql("""
              IF (SELECT COUNT(*) FROM [dbo].[Patients]
                  WHERE [MedicalRecordNumber] IS NULL) > 1
              BEGIN
                  ;THROW 50000,
                      'Cannot revert AllowNullMedicalRecordNumber:
                      more than one patient has no medical record
                      number.',
                      1;
              END;

              UPDATE [dbo].[Patients]
              SET [MedicalRecordNumber] = N''
              WHERE [MedicalRecordNumber] IS NULL;
              """);

            migrationBuilder.AlterColumn<string>(
                name: "MedicalRecordNumber",
                table: "Patients",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
              name: "IX_Patients_MedicalRecordNumber",
              table: "Patients",
              column: "MedicalRecordNumber",
              unique: true);
        }
    }
}
