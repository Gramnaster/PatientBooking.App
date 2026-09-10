using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientBooking.Api.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberately NOT dropping BookingOutboxMessages / RegistrationOutboxMessages /
            // SentBookingNotifications / SentRegistrationNotifications here (the auto-scaffolded
            // migration proposed DropTable for all four - removed by hand). This is a solo,
            // low-volume application: the chosen transition is a documented maintenance-window
            // cutover (drain both old outboxes and both old RabbitMQ queues to zero before deploying
            // this migration - see docs/deployment.md), not a dual-running compatibility shim. The
            // four old tables are retained purely as an inert historical/audit record so nothing is
            // silently destroyed even if that preflight check is skipped; nothing in this codebase
            // reads or writes them after this migration. A future migration MAY drop them once an
            // operator has confirmed on the actual deployment target that they are empty and no
            // longer needed for audit - that is a deliberate follow-up, not automatic here.
            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SentEmailNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentEmailNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_DispatchedAtUtc",
                table: "EmailOutboxMessages",
                column: "DispatchedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_Recipient",
                table: "EmailOutboxMessages",
                column: "Recipient");

            // Carries every historical "already sent" record forward so a message that somehow gets
            // replayed against the new shared consumer (e.g. from a legacy failed queue) is still
            // recognized as a duplicate rather than resent. Idempotent (safe to re-run): the WHERE
            // NOT EXISTS guard means running this migration twice, or partially re-running it after a
            // failure, never throws a duplicate-key error and never double-inserts.
            migrationBuilder.Sql(
                """
                INSERT INTO SentEmailNotifications (Id, SentAtUtc)
                SELECT b.Id, b.SentAtUtc FROM SentBookingNotifications b
                WHERE NOT EXISTS (SELECT 1 FROM SentEmailNotifications e WHERE e.Id = b.Id);
                """
            );

            migrationBuilder.Sql(
                """
                INSERT INTO SentEmailNotifications (Id, SentAtUtc)
                SELECT r.Id, r.SentAtUtc FROM SentRegistrationNotifications r
                WHERE NOT EXISTS (SELECT 1 FROM SentEmailNotifications e WHERE e.Id = r.Id);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only undoes what Up() actually added. The four old tables were never dropped by Up(),
            // so there is nothing to recreate here - doing so would duplicate them (or fail outright
            // if they still exist, which they always do on this migration's Down path).
            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropTable(
                name: "SentEmailNotifications");
        }
    }
}
