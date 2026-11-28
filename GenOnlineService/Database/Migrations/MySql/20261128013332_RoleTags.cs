using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class RoleTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tag",
                table: "roles",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Keeps the look the badges had while they came from configuration defaults, and a colour staff already chose
            migrationBuilder.Sql("UPDATE roles SET tag = '★★GO STAFF★★', color = COALESCE(color, '#f5c542') WHERE name IN ('owner', 'admin') AND tag IS NULL");
            migrationBuilder.Sql("UPDATE roles SET tag = 'MOD', color = COALESCE(color, '#4aa3ff') WHERE name = 'moderator' AND tag IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tag",
                table: "roles");
        }
    }
}
