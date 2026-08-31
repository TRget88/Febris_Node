using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Audit C-04 -- the parent-link table that every layer above it assumed existed.
    ///
    /// <para>
    /// <c>DataDbContext:350</c> declares the DbSet, and the DAL, BLL, controller and views all
    /// ship, but NO migration ever created the table and the snapshot did not describe it either.
    /// Any parent-link operation failed with <c>relation "ParentLinkedStudent" does not exist</c>.
    /// Parent access is FERPA-relevant, so this is not a cosmetic gap.
    /// </para>
    ///
    /// <para>
    /// How it was missed, recorded because the reasoning was explicit and wrong: four earlier
    /// migrations (LocalModuleCatalog, NodeArtifactStore, NodeLocalIdentity,
    /// HubFederationConfigTable, ModuleLinkedCurriculumTable) were hand-trimmed against "KNOWN
    /// pre-existing DataDb snapshot drift ... describing schema the shipped DBs already have", and
    /// ParentLinkedStudent was named in that exclusion list every time. It was not drift. The
    /// table had never been created anywhere, which the audit confirmed against the live sandbox
    /// database. Excluding a scaffolded CreateTable as "already shipped" needs the database
    /// checked, not the assumption repeated.
    /// </para>
    ///
    /// <para>
    /// Column shape matches the entity EXACTLY. Unlike the ~121 entities configured in
    /// <c>OnModelCreating</c>, ParentLinkedStudent has no configuration block, so it gets NO
    /// <c>uuid_generate_v4()</c> or <c>CURRENT_TIMESTAMP</c> defaults -- and adding them here would
    /// create the very model-versus-schema disagreement that C-01 was. <c>ParentLinkLogic</c>
    /// assigns UUID, TimeStamp and LastUpdateTimeStamp itself on insert. Bringing this entity into
    /// line with the other 121 is a deliberate follow-up, not something to smuggle into the
    /// migration that makes the feature work at all.
    /// </para>
    /// </summary>
    public partial class ParentLinkedStudentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentLinkedStudent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentLinkedStudent", x => x.Id);
                });

            // Indexes follow the predicates ParentLinkedStudentQueries actually uses: three reads
            // filter on ParentUserId (:60, :84, :105) and two on StudentActorId (:60, :125).
            // Deliberately NO index on StudentUserId -- it is stored but no query filters by it.
            migrationBuilder.CreateIndex(
                name: "IX_ParentLinkedStudent_ParentUserId",
                table: "ParentLinkedStudent",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentLinkedStudent_StudentActorId",
                table: "ParentLinkedStudent",
                column: "StudentActorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentLinkedStudent");
        }
    }
}
