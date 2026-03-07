using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualLibrary.Migrations
{
    /// <inheritdoc />
    public partial class BookAudioFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Audiobooks_ProductId",
                table: "Audiobooks");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "Audiobooks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audiobooks_ProductId",
                table: "Audiobooks",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Audiobooks_ProductId",
                table: "Audiobooks");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "Audiobooks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audiobooks_ProductId",
                table: "Audiobooks",
                column: "ProductId");
        }
    }
}
