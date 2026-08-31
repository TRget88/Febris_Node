using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    /// <summary>
    /// Local-first vocabulary -- a CONVERGENCE migration, not a create.
    ///
    /// <para>
    /// The 2022 Initial migration ALREADY creates the Verb / Object / Version tables (and
    /// IX_Object_DefinitionId): the tables have been in the tenant schema all along -- only the
    /// DbSets were commented out of XApiDbContext after Initial was scaffolded, so the model
    /// snapshot no longer knew about them. The as-scaffolded version of this migration therefore
    /// re-created all three tables and failed on a real database with 42P07 "relation already
    /// exists" (caught by the LAN-Postgres smoke run; the InMemory tests could not see it).
    /// </para>
    ///
    /// <para>
    /// Hand-edited to apply only the REAL delta between the Initial-created schema and the
    /// re-mapped model: the db-generated UUID defaults (uuid_generate_v4()) on the three UUID
    /// columns, which Initial omitted for these tables (it already sets them for Definition
    /// etc.). The accompanying Designer/snapshot are untouched -- they correctly describe the
    /// target model. Edited before ever being merged or deployed, so the never-edit-a-shipped-
    /// migration rule does not apply.
    /// </para>
    /// </summary>
    public partial class LocalVocabularyStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Verb",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Object",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Version",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Verb",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Object",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "Version",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "uuid_generate_v4()");
        }
    }
}
