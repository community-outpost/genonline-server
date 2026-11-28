using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AdminDeletePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The admin role gets the new delete permissions as a fresh seed gives them, other roles stay as staff set them
            foreach (string permission in Permissions)
            {
                migrationBuilder.Sql(
                    "INSERT INTO role_permissions (role, permission) SELECT name, '" + permission + "' FROM roles WHERE name = 'admin' " +
                    "AND NOT EXISTS (SELECT 1 FROM role_permissions WHERE role = 'admin' AND permission = '" + permission + "')");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string permission in Permissions)
            {
                migrationBuilder.Sql("DELETE FROM role_permissions WHERE role = 'admin' AND permission = '" + permission + "'");
            }
        }

        private static readonly string[] Permissions = ["sanctions.delete", "rules.delete"];
    }
}
