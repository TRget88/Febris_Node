using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class RecordingDeviceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HardwareUUID",
                table: "Recording",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Recording_HardwareUUID",
                table: "Recording",
                column: "HardwareUUID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recording_HardwareUUID",
                table: "Recording");

            migrationBuilder.DropColumn(
                name: "HardwareUUID",
                table: "Recording");
        }
    }
}
