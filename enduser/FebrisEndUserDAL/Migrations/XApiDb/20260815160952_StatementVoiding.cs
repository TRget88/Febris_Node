using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    /// <inheritdoc />
    public partial class StatementVoiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "LocalStatement",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "LocalStatement",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_VoidedAt",
                table: "LocalStatement",
                column: "VoidedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocalStatement_VoidedAt",
                table: "LocalStatement");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "LocalStatement");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "LocalStatement");
        }
    }
}
