using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinOrganizationPermissionCatalogPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatalogPolicyVersion",
                schema: "shared",
                table: "OrganizationRolePermissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CkOrganizationRolePermissionsCatalogPolicyVersion",
                schema: "shared",
                table: "OrganizationRolePermissions",
                sql: "\"CatalogPolicyVersion\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CkOrganizationRolePermissionsCatalogPolicyVersion",
                schema: "shared",
                table: "OrganizationRolePermissions");

            migrationBuilder.DropColumn(
                name: "CatalogPolicyVersion",
                schema: "shared",
                table: "OrganizationRolePermissions");
        }
    }
}
