using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthSessions",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RefreshExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RefreshTokenVerifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    RefreshRotatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkAuthSessions", x => x.Id);
                    table.CheckConstraint("CkAuthSessionsRefreshDeadline", "\"RefreshExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CkAuthSessionsRefreshTokenVerifier", "octet_length(\"RefreshTokenVerifier\") = 32");
                    table.ForeignKey(
                        name: "FkAuthSessionsUsersUserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IxAuthSessionsUserId",
                schema: "shared",
                table: "AuthSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthSessions",
                schema: "shared");
        }
    }
}
