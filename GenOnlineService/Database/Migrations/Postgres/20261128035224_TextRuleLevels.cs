using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class TextRuleLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "level",
                table: "text_rule_terms",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // the level of an existing word follows its category, impersonation and reserved words keep none
            migrationBuilder.Sql(
                "UPDATE text_rule_terms SET level = CASE category WHEN 'slur' THEN 'extreme' WHEN 'sexual' THEN 'standard' WHEN 'insult' THEN 'standard' WHEN 'profanity' THEN 'strict' END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "level",
                table: "text_rule_terms");
        }
    }
}
