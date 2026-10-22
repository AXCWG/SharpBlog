using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpBlog.Migrations
{
    /// <inheritdoc />
    public partial class _1792633713 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Articles_Tags",
                table: "Articles",
                column: "Tags");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_Tags",
                table: "Articles");
        }
    }
}
