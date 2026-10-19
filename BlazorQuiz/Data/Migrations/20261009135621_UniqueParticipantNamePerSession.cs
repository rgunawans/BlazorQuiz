using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorQuiz.Migrations
{
    /// <inheritdoc />
    public partial class UniqueParticipantNamePerSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename (never delete) pre-existing duplicate names so the unique index can be created:
            // the oldest row keeps its name, later ones become "<name>#<Id>".
            migrationBuilder.Sql(@"
                UPDATE p
                SET PlayerName = LEFT(p.PlayerName, 90) + '#' + CAST(p.Id AS nvarchar(10))
                FROM QuizParticipants p
                WHERE p.Id > (SELECT MIN(p2.Id) FROM QuizParticipants p2
                              WHERE p2.QuizSessionId = p.QuizSessionId AND p2.PlayerName = p.PlayerName);");

            migrationBuilder.DropIndex(
                name: "IX_QuizParticipants_QuizSessionId_PlayerName",
                table: "QuizParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_QuizParticipants_QuizSessionId_PlayerName",
                table: "QuizParticipants",
                columns: new[] { "QuizSessionId", "PlayerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizParticipants_QuizSessionId_PlayerName",
                table: "QuizParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_QuizParticipants_QuizSessionId_PlayerName",
                table: "QuizParticipants",
                columns: new[] { "QuizSessionId", "PlayerName" });
        }
    }
}
