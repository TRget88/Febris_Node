using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class HardwareCredentialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Hardware_PhysicalLicense",
                table: "Hardware",
                column: "PhysicalLicense",
                unique: true,
                filter: "\"PhysicalLicense\" IS NOT NULL AND \"PhysicalLicense\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hardware_PhysicalLicense",
                table: "Hardware");
        }
    }
}
