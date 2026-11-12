using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowProjectGuestInvitationKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries");

            migrationBuilder.AddCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries",
                sql: "\"RequestKind\" IN ('verification','organization-invitation','project-guest-invitation') AND \"InvitationStatusSequence\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries");

            migrationBuilder.AddCheckConstraint(
                name: "CkEmailDeliveryRequestKind",
                schema: "notification",
                table: "EmailDeliveries",
                sql: "\"RequestKind\" IN ('verification','organization-invitation') AND \"InvitationStatusSequence\" >= 0");
        }
    }
}
