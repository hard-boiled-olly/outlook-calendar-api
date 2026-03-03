using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OutlookCalendarApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_of_life = table.Column<string>(type: "text", nullable: false),
                    statement = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    abandoned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    frequency = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habits", x => x.id);
                    table.ForeignKey(
                        name: "FK_habits_identities_identity_id",
                        column: x => x.identity_id,
                        principalTable: "identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "summits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    proof_criteria = table.Column<string>(type: "text", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_summits", x => x.id);
                    table.ForeignKey(
                        name: "FK_summits_identities_identity_id",
                        column: x => x.identity_id,
                        principalTable: "identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    summit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    proof_criteria = table.Column<string>(type: "text", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    proved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestones", x => x.id);
                    table.ForeignKey(
                        name: "FK_milestones_summits_summit_id",
                        column: x => x.summit_id,
                        principalTable: "summits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reflection = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprints", x => x.id);
                    table.ForeignKey(
                        name: "FK_sprints_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "milestones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_prescriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    habit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_prescriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_habit_prescriptions_habits_habit_id",
                        column: x => x.habit_id,
                        principalTable: "habits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_habit_prescriptions_sprints_sprint_id",
                        column: x => x.sprint_id,
                        principalTable: "sprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    duration_mins = table.Column<int>(type: "integer", nullable: true),
                    calendar_event_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_sprint_tasks_sprints_sprint_id",
                        column: x => x.sprint_id,
                        principalTable: "sprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    habit_prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    duration_mins = table.Column<int>(type: "integer", nullable: false),
                    calendar_event_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_habit_events_habit_prescriptions_habit_prescription_id",
                        column: x => x.habit_prescription_id,
                        principalTable: "habit_prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_habit_events_sprints_sprint_id",
                        column: x => x.sprint_id,
                        principalTable: "sprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_habit_events_habit_prescription_id",
                table: "habit_events",
                column: "habit_prescription_id");

            migrationBuilder.CreateIndex(
                name: "IX_habit_events_sprint_id",
                table: "habit_events",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_habit_prescriptions_habit_id",
                table: "habit_prescriptions",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "IX_habit_prescriptions_sprint_id",
                table: "habit_prescriptions",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_habits_identity_id",
                table: "habits",
                column: "identity_id");

            migrationBuilder.CreateIndex(
                name: "IX_identities_user_id",
                table: "identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_milestones_summit_id",
                table: "milestones",
                column: "summit_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_tasks_sprint_id",
                table: "sprint_tasks",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprints_milestone_id",
                table: "sprints",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "IX_summits_identity_id",
                table: "summits",
                column: "identity_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "habit_events");

            migrationBuilder.DropTable(
                name: "sprint_tasks");

            migrationBuilder.DropTable(
                name: "habit_prescriptions");

            migrationBuilder.DropTable(
                name: "habits");

            migrationBuilder.DropTable(
                name: "sprints");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropTable(
                name: "summits");

            migrationBuilder.DropTable(
                name: "identities");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
