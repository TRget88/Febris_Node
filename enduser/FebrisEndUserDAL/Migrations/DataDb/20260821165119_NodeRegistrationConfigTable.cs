using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (node initialization design 2026-08-18): the node's persisted registration policy --
    /// exactly one row, written by the portal's Registration admin page (no seeder: absence means
    /// the operator never touched the page, and the configured <c>Identity:Registration</c> section
    /// keeps governing). When the row exists it GOVERNS; when it cannot be READ the policy resolves
    /// AdminOnly, which is the asymmetry the whole feature turns on.
    ///
    /// <para>
    /// SCAFFOLD NOTE, recorded because the two previous settings-table migrations
    /// (<c>NodeLocalIdentity</c>, <c>HubFederationConfigTable</c>) had to be hand-trimmed against
    /// known pre-existing DataDb snapshot drift, and <c>ParentLinkedStudentTable</c> records that
    /// the same hand-trimming reasoning once shipped a MISSING TABLE. That was not necessary here:
    /// the raw scaffold emitted exactly this one <c>CreateTable</c> and a purely additive
    /// 49-line snapshot delta for the new entity, with no drift re-emitted. Nothing was
    /// hand-edited except this comment, and the table was verified present in a real migrated
    /// database rather than assumed.
    /// </para>
    ///
    /// <para>
    /// The migration class is named <c>NodeRegistrationConfigTable</c> (not
    /// <c>NodeRegistrationConfig</c>) so it does not duplicate the model type's name across
    /// projects, per the duplicate-type ratchet -- the same reason
    /// <c>HubFederationConfigTable</c> carries the suffix.
    /// </para>
    /// </summary>
    public partial class NodeRegistrationConfigTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeRegistrationConfig",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Mode = table.Column<string>(type: "text", nullable: true),
                    AllowedEmailDomains = table.Column<string>(type: "text", nullable: true),
                    RequireAdminApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AutoProvisionJit = table.Column<bool>(type: "boolean", nullable: false),
                    OpenUntilUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedByEmail = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeRegistrationConfig", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeRegistrationConfig");
        }
    }
}
