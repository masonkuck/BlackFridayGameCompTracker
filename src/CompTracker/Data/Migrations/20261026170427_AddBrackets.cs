using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrackets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BracketSlots",
                table: "Games",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BracketResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchKey = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WinnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BracketResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BracketResults_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BracketResults_Players_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BracketResults_GameId_MatchKey",
                table: "BracketResults",
                columns: new[] { "GameId", "MatchKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BracketResults_WinnerId",
                table: "BracketResults",
                column: "WinnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BracketResults");

            migrationBuilder.DropColumn(
                name: "BracketSlots",
                table: "Games");
        }
    }
}
