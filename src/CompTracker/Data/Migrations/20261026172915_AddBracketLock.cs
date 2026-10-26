using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BracketLocked",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Brackets that already have results were effectively locked in.
            migrationBuilder.Sql("UPDATE Games SET BracketLocked = 1 WHERE Id IN (SELECT GameId FROM BracketResults)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BracketLocked",
                table: "Games");
        }
    }
}
