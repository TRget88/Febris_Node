using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Gives the node's device row its own hardware kind, as an enum column.
    ///
    /// <para>
    /// The node had no source of truth for what kind of machine a device is. It carried
    /// <c>HardwareTypeId</c>, an UNENFORCED raw <c>long</c> pointing into the <c>HardwareType</c>
    /// lookup: the real device table has no foreign key to that lookup at all, because the only
    /// relationship EF knew about belonged to the central <c>Hardware</c> entity, which a 2022
    /// name collision exiled to the orphan <c>Hardware1</c> table. So the pointer could dangle and
    /// nothing would stop it.
    /// </para>
    ///
    /// <para>
    /// <c>HardwareKind</c> is now that source of truth. <c>HardwareTypeId</c> and
    /// <c>HardwareTypeUUID</c> are deliberately KEPT as inert hub-reconciliation carriers so a
    /// device registered while the hub is absent can still be matched against the hub's catalog if
    /// it returns. They are not read for behaviour and are not dropped here.
    /// </para>
    ///
    /// <para>
    /// BACKFILL. The column is NOT NULL with a 0 default, so every existing row lands on
    /// <c>HardwareKind.Unknown</c> first. The UPDATE then maps each row through its existing
    /// <c>HardwareTypeId</c> to the seeded type NAME, because the name is the only thing the node
    /// and the hub ever agreed on: the UUIDs were generated per database until the catalog froze
    /// them, so joining on UUID would match nothing on a pre-existing database. A row whose type is
    /// missing, or is an operator-added type outside the seeded three, correctly stays Unknown
    /// rather than being guessed at. The values 100/200/300 are
    /// <c>Febris.EnumLibrary.HardwareKind</c> and must stay in step with it.
    /// </para>
    ///
    /// <para>
    /// This migration does NOT touch <c>Hardware1</c>, the two link foreign keys that point at it,
    /// or the missing <c>Hardware</c>-to-<c>HardwareType</c> foreign key. That teardown is
    /// destructive, fixes a latent rather than a live defect, and ships separately so a rollback of
    /// one does not force a rollback of the other.
    /// </para>
    /// </summary>
    public partial class LocalHardwareKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HardwareKind",
                table: "Hardware",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE ""Hardware"" h
                   SET ""HardwareKind"" = CASE t.""Name""
                       WHEN 'Laptop PC'     THEN 100
                       WHEN 'Desktop PC'    THEN 200
                       WHEN 'Mobile Server' THEN 300
                       ELSE 0
                   END
                  FROM ""HardwareType"" t
                 WHERE t.""Id"" = h.""HardwareTypeId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardwareKind",
                table: "Hardware");
        }
    }
}
