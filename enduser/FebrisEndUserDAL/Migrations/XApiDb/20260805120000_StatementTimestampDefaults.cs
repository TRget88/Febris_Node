using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    /// <summary>
    /// Audit C-01 -- a CONVERGENCE migration, not a schema change.
    ///
    /// <para>
    /// <c>XApiDbContext</c> has always declared <c>LocalStatement.Stored</c> and
    /// <c>LocalStatement.Timestamp</c> as
    /// <c>HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd()</c>, and the model snapshot
    /// records both. The 2022 Initial migration created the two columns as <c>nullable: false</c>
    /// with NO <c>defaultValueSql</c>, so the model and the database disagreed. This is the same
    /// omission the LocalVocabularyStores migration fixed for the Verb / Object / Version UUID
    /// columns; the sibling <c>Statement</c> table at Initial :392 did get its <c>Stored</c>
    /// default, which is why the gap went unnoticed.
    /// </para>
    ///
    /// <para>
    /// Why it mattered: <c>ValueGeneratedOnAdd</c> tells EF to omit a property from the INSERT when
    /// it still holds the CLR default and let the store supply it. <c>Stored</c> is never assigned
    /// anywhere in the BLL, so it was omitted from EVERY insert, and the column had no default and
    /// was NOT NULL -- on a database provisioned from migrations alone, no learning record could be
    /// stored at all. Statement ingest is the node's reason to exist.
    /// </para>
    ///
    /// <para>
    /// <c>Stored</c> stays store-generated on purpose: it is the LRS's own record-keeping time, not
    /// the producer's. <c>Timestamp</c> is the PRODUCER's time under xAPI 1.0.3 and is now always
    /// assigned in <c>StatementLogic</c>, so EF includes it in the INSERT and the default here only
    /// applies to a statement that genuinely carried none.
    /// </para>
    /// </summary>
    public partial class StatementTimestampDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Stored",
                table: "LocalStatement",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "LocalStatement",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Stored",
                table: "LocalStatement",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "LocalStatement",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
