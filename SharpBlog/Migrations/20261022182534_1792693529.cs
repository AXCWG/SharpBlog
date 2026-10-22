using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpBlog.Migrations
{
    /// <inheritdoc />
    public partial class _1792693529 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Unlisted",
                table: "Articles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unlisted",
                table: "Articles");
        }
    }
}
