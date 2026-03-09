using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OutlookCalendarApi.Migrations
{
    /// <inheritdoc />
    public partial class UxOverhaulFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "area_of_life",
                table: "identities");

            migrationBuilder.DropColumn(
                name: "status",
                table: "identities");

            migrationBuilder.RenameColumn(
                name: "abandoned_at",
                table: "identities",
                newName: "deleted_at");

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "identities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "interview_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    type = table.Column<string>(type: "text", nullable: false),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_step = table.Column<int>(type: "integer", nullable: false),
                    conversation_history = table.Column<string>(type: "jsonb", nullable: false),
                    accumulated_data = table.Column<string>(type: "jsonb", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_sessions_identities_identity_id",
                        column: x => x.identity_id,
                        principalTable: "identities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_interview_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_interview_sessions_identity_id",
                table: "interview_sessions",
                column: "identity_id");

            migrationBuilder.CreateIndex(
                name: "IX_interview_sessions_user_id",
                table: "interview_sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "active",
                table: "identities");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "identities",
                newName: "abandoned_at");

            migrationBuilder.AddColumn<string>(
                name: "area_of_life",
                table: "identities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "identities",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
