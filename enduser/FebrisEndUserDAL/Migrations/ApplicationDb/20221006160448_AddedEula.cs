using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Febris.UserNode.DataAccessLayer.Migrations.ApplicationDb
{
    public partial class AddedEula : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EULA",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EULA",
                table: "AspNetUsers");
        }
    }
}
