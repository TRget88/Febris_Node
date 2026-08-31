using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (2026-08-21) OPTIONAL cohort linkage on an invitation: the accepted account joins this
    /// cohort automatically, so inviting a class is one step instead of two.
    ///
    /// <para>
    /// NULLABLE, and a plain uuid rather than a foreign key. Days pass between issue and
    /// acceptance, and a cohort archived or deleted in the meantime must not make the invitation
    /// unredeemable -- the account is still created, only the linkage is skipped, and that is
    /// logged. A real FK would turn a tidy-up of the cohort table into a broken invitation.
    /// </para>
    /// </summary>
    public partial class NodeUserInviteCohortLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CohortUUID",
                table: "NodeUserInvite",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CohortUUID",
                table: "NodeUserInvite");
        }
    }
}
