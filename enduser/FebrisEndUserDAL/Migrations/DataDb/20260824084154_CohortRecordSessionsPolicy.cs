using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (2026-08-24, ROADMAP 22) The educator's per-cohort recording policy. A simulation launched
    /// for a cohort with this set is recorded, and the node derives that decision server-side
    /// rather than accepting the client's word for it.
    ///
    /// <para>
    /// NOT NULLABLE, defaulting to FALSE, so every cohort that already exists backfills to
    /// not-recording. Recording learner session video is opt-in per cohort by an educator who can
    /// be asked why, never something a schema change switches on for a node that upgrades.
    /// </para>
    ///
    /// <para>
    /// A plain scalar on Cohort rather than its own table, matching Archive and LockMembers: the
    /// policy is one bit per cohort with no history requirement. Note the trap those two flags
    /// taught (docs/BUGS.md, cohort retirement flags): the Edit POST binds a fixed property list
    /// and CohortQueries.Update writes every scalar, so a flag missing from that bind list is
    /// silently cleared whenever an educator renames the cohort. RecordSessions is in the bind
    /// list, and a regression test pins it.
    /// </para>
    /// </summary>
    public partial class CohortRecordSessionsPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordSessions",
                table: "Cohort",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordSessions",
                table: "Cohort");
        }
    }
}
