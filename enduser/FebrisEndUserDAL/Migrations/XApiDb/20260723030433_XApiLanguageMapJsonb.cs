using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    /// <inheritdoc />
    public partial class XApiLanguageMapJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xAPI 1.0.3 typing uplift: convert the Language-Map columns (Verb.Display,
            // Definition.Name/Description, Attachment.Display/Description) and the interaction
            // correct-response array (Definition.CorrectResponsesPattern) from text to jsonb.
            //
            // Existing rows already hold JSON (the seeder/ingest wrote {"en":"..."}), so the change is
            // an in-place cast -- NOT a data migration. The USING clauses are defensive so the ALTER
            // can never fail on legacy/malformed data:
            //   * Language maps: a value already shaped as a JSON object is cast as-is; any stray
            //     non-object text is wrapped as {"und": <text>} (xAPI "undetermined" locale) -- a valid map.
            //   * CorrectResponsesPattern: every existing value becomes a one-element JSON array
            //     (to_jsonb(ARRAY[value])), matching the new write path (new List<string> { pattern }),
            //     so even non-JSON legacy forms like "[,]" convert cleanly.

            migrationBuilder.Sql(
                "ALTER TABLE \"Verb\" ALTER COLUMN \"Display\" TYPE jsonb USING " +
                "(CASE WHEN \"Display\" IS NULL THEN NULL " +
                "WHEN left(btrim(\"Display\"), 1) = '{' THEN \"Display\"::jsonb " +
                "ELSE jsonb_build_object('und', \"Display\") END);");

            migrationBuilder.Sql(
                "ALTER TABLE \"Definition\" ALTER COLUMN \"Name\" TYPE jsonb USING " +
                "(CASE WHEN \"Name\" IS NULL THEN NULL " +
                "WHEN left(btrim(\"Name\"), 1) = '{' THEN \"Name\"::jsonb " +
                "ELSE jsonb_build_object('und', \"Name\") END);");

            migrationBuilder.Sql(
                "ALTER TABLE \"Definition\" ALTER COLUMN \"Description\" TYPE jsonb USING " +
                "(CASE WHEN \"Description\" IS NULL THEN NULL " +
                "WHEN left(btrim(\"Description\"), 1) = '{' THEN \"Description\"::jsonb " +
                "ELSE jsonb_build_object('und', \"Description\") END);");

            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" ALTER COLUMN \"Display\" TYPE jsonb USING " +
                "(CASE WHEN \"Display\" IS NULL THEN NULL " +
                "WHEN left(btrim(\"Display\"), 1) = '{' THEN \"Display\"::jsonb " +
                "ELSE jsonb_build_object('und', \"Display\") END);");

            migrationBuilder.Sql(
                "ALTER TABLE \"Attachments\" ALTER COLUMN \"Description\" TYPE jsonb USING " +
                "(CASE WHEN \"Description\" IS NULL THEN NULL " +
                "WHEN left(btrim(\"Description\"), 1) = '{' THEN \"Description\"::jsonb " +
                "ELSE jsonb_build_object('und', \"Description\") END);");

            migrationBuilder.Sql(
                "ALTER TABLE \"Definition\" ALTER COLUMN \"CorrectResponsesPattern\" TYPE jsonb USING " +
                "(CASE WHEN \"CorrectResponsesPattern\" IS NULL THEN NULL " +
                "ELSE to_jsonb(ARRAY[\"CorrectResponsesPattern\"]) END);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // jsonb -> text is a lossless cast in every case.
            migrationBuilder.AlterColumn<string>(
                name: "Display", table: "Verb", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "Name", table: "Definition", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "Description", table: "Definition", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "CorrectResponsesPattern", table: "Definition", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "Display", table: "Attachments", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "Description", table: "Attachments", type: "text", nullable: true,
                oldClrType: typeof(string), oldType: "jsonb", oldNullable: true);
        }
    }
}
