using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OutlookCalendarApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSummitIdToInterviewSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SummitId",
                table: "interview_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_interview_sessions_SummitId",
                table: "interview_sessions",
                column: "SummitId");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_sessions_summits_SummitId",
                table: "interview_sessions",
                column: "SummitId",
                principalTable: "summits",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_interview_sessions_summits_SummitId",
                table: "interview_sessions");

            migrationBuilder.DropIndex(
                name: "IX_interview_sessions_SummitId",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "SummitId",
                table: "interview_sessions");
        }
    }
}
