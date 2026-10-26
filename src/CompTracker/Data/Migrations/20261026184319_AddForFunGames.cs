using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForFunGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForFun",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForFun",
                table: "Games");
        }
    }
}
