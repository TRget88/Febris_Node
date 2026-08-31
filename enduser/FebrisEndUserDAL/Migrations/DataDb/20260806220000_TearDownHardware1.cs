using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Removes the orphan <c>Hardware1</c> table and repoints the two foreign keys that pointed at
    /// it back onto the real device table.
    ///
    /// <para>
    /// WHAT Hardware1 WAS. Nobody named it. It is an EF Core 3.1 auto-disambiguation artifact,
    /// created in <c>20220709225005_hardwarelinking</c> (Up at :99, dropped only in Down at :200,
    /// never converged). On 2026-07-02 the node had one hardware entity, the central
    /// <c>Hardware</c>, mapped to table <c>Hardware</c>. Seven days later <c>LocalHardware</c> was
    /// introduced and its DbSet claimed the name <c>Hardware</c>. The central entity was still
    /// being pulled into the node model by the shared link entities' <c>public Hardware Hardware</c>
    /// navigations, so EF needed somewhere to put it and silently appended a "1".
    /// </para>
    ///
    /// <para>
    /// WHAT IT BROKE. <c>HardwareLinkedCurriculum</c> and <c>LocationLinkedHardware</c> had their
    /// foreign keys created against <c>Hardware1</c>, which has no writer and is permanently empty.
    /// Linking a device to a curriculum or to a location could only ever fail with
    /// <c>23503</c> on every node. Verified on the dev database and on a scratch database
    /// provisioned from the migration chain alone, so this was not a dev-box artifact. It was
    /// LATENT rather than live only because neither feature is wired: no DI registration in either
    /// host, no controller, no view.
    /// </para>
    ///
    /// <para>
    /// WHY IT IS SAFE TO DROP. <c>Hardware1</c> has never had a writer in any tier and is empty in
    /// every database inspected. Both link tables are empty. The node now uses its own
    /// <c>LocalHardwareLinkedCurriculum</c> and <c>LocalLocationLinkedHardware</c> twins, which
    /// navigate <c>LocalHardware</c>, so nothing pulls the central aggregate into the node model
    /// and the collision cannot recur.
    /// </para>
    ///
    /// <para>
    /// WHAT THIS DELIBERATELY DOES NOT DO: add a foreign key from <c>Hardware</c> to
    /// <c>HardwareType</c>. That FK is genuinely absent, and it stays absent on purpose.
    /// <c>HardwareTypeId</c> is now an INERT hub-reconciliation carrier rather than the node's
    /// source of truth, and it is allowed to be unset when the lookup has no matching row.
    /// <c>LocalHardwareKindTests.Registering_WithAnUnseededLookup_StillStoresTheKind</c> pins that.
    /// Adding the constraint would convert a deliberate, tested, degraded-but-correct registration
    /// into a hard failure.
    /// </para>
    ///
    /// <para>
    /// The two operation lists below are EF's own, taken from
    /// <c>IMigrationsModelDiffer.GetDifferences</c> against the updated snapshot, not hand-guessed.
    /// The delete behaviours preserve what the 2022 constraints had: Cascade for the curriculum
    /// link, whose HardwareId is NOT NULL, and NoAction for the location link, whose HardwareId is
    /// nullable.
    /// </para>
    /// </summary>
    public partial class TearDownHardware1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationLinkedHardware_Hardware1_HardwareId",
                table: "LocationLinkedHardware");

            migrationBuilder.DropTable(
                name: "Hardware1");

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationLinkedHardware_Hardware_HardwareId",
                table: "LocationLinkedHardware",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationLinkedHardware_Hardware_HardwareId",
                table: "LocationLinkedHardware");

            migrationBuilder.CreateTable(
                name: "Hardware1",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<System.Guid>(type: "uuid", nullable: false),
                    TimeStamp = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUpdateTimeStamp = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    HardwareTypeUUID = table.Column<System.Guid>(type: "uuid", nullable: true),
                    HardwareTypeId = table.Column<long>(type: "bigint", nullable: true),
                    DescriptiveName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PhysicalLicense = table.Column<string>(type: "text", nullable: true),
                    HardwareCondition = table.Column<int>(type: "integer", nullable: false),
                    IsLockedOut = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hardware1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hardware1_HardwareType_HardwareTypeId",
                        column: x => x.HardwareTypeId,
                        principalTable: "HardwareType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hardware1_HardwareTypeId",
                table: "Hardware1",
                column: "HardwareTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationLinkedHardware_Hardware1_HardwareId",
                table: "LocationLinkedHardware",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id");
        }
    }
}
