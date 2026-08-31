using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class DropMessageBoardCentralUuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccreditationBodyUUID",
                table: "MessageBoard");

            migrationBuilder.DropColumn(
                name: "ContentDeveloperUUID",
                table: "MessageBoard");

            migrationBuilder.DropColumn(
                name: "InstitutionUUID",
                table: "MessageBoard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccreditationBodyUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentDeveloperUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: true);
        }
    }
}
