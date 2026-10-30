using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationDeliveryKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "InvitationStatusSequence",
                schema: "notification",
                table: "EmailDeliveries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "RequestKind",
                schema: "notification",
                table: "EmailDeliveries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "verification");

            migrationBuilder.AddCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries",
                sql: "\"RequestKind\" IN ('verification','organization-invitation') AND \"InvitationStatusSequence\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "InvitationStatusSequence",
                schema: "notification",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "RequestKind",
                schema: "notification",
                table: "EmailDeliveries");
        }
    }
}
