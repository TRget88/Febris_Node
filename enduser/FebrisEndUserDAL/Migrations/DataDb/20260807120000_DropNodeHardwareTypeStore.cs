using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Drops the node's copy of the HardwareType vocabulary. Owner ruling 2026-08-07: the hardware
    /// type truth lives on the hub node, and the node should never have carried a copy.
    ///
    /// <para>
    /// The table has been in the node's own database since <c>20220702011028_Initial:138</c>. On
    /// 2026-07-21, commit <c>98b3281</c> ("local-first HardwareType lookup") added a node-side
    /// seeder so the three rows were populated locally instead of pulled from the hub, and
    /// <c>NODE_REMOTE_TEARDOWN_PLAN.md:392</c> put <c>IHardwareTypeLogic</c> on the explicit KEEP
    /// list. That decision is SUPERSEDED by this migration, and the reasoning behind it no longer
    /// holds: it was kept because the node's registration dropdown needed the list, and the
    /// dropdown now renders from the <c>HardwareKind</c> enum.
    /// </para>
    ///
    /// <para>
    /// Nothing on the node ever read a hardware type to make a decision. Verified across
    /// <c>enduser/</c> and <c>shared/</c>: no WHERE clause, no branch, no entitlement gate and no
    /// launcher check narrowed anything by type. The table had two writers (the seeder and the
    /// registration form), six display sites and zero behavioural readers, which is the
    /// write-side-with-no-read-side shape this audit exists to remove.
    /// </para>
    ///
    /// <para>
    /// Removed alongside it: <c>HardwareTypeSeeder</c>, <c>HardwareTypeQueries</c>,
    /// <c>HardwareTypeLogic</c> and its DI registration, the DbSet, three orphan Razor button
    /// partials, one orphan JS file, and an unused <c>IHardwareTypeLogic</c> field on
    /// <c>HardwareController</c> plus an unused <c>IHardwareTypeQueries</c> field on
    /// <c>HardwareLinkedModuleLogic</c> whose only reads were already commented out.
    /// </para>
    ///
    /// <para>
    /// <c>LocalHardware.HardwareTypeId</c> and <c>.HardwareTypeUUID</c> are RETAINED as columns per
    /// the owner's instruction to keep the id in case the hub returns. They are now genuinely
    /// inert: nothing writes them and nothing reads them. If the hub does return, a device is
    /// matched by its <c>HardwareKind</c> member name against the hub's catalog, which needs
    /// nothing persisted on the node.
    /// </para>
    ///
    /// <para>
    /// SAFE TO DROP: the only foreign key that ever targeted this table belonged to
    /// <c>Hardware1</c>, which the preceding migration <c>20260806220000_TearDownHardware1</c>
    /// removes. After that migration the table has no dependents. The three rows are seed data
    /// reproduced from <c>DataBaseSeedDataInitalizer.CreateOriginalHardwareType()</c>, so nothing
    /// operator-authored is lost. Down recreates the table and its rows are re-seeded by nothing --
    /// see the note on Down below.
    /// </para>
    /// </summary>
    public partial class DropNodeHardwareTypeStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HardwareType");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Down recreates the SHAPE only. The node-side seeder is deleted, so a rolled-back database
        /// gets an empty table rather than the three rows. That is deliberate: re-adding the seeder
        /// on a rollback would reinstate exactly the copy this migration exists to remove. Anything
        /// needing those rows back should take them from the hub, which owns them.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HardwareType",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareType", x => x.Id);
                });
        }
    }
}
