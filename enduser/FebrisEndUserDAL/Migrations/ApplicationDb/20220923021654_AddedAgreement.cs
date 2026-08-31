using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Febris.UserNode.DataAccessLayer.Migrations.ApplicationDb
{
    public partial class AddedAgreement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceAgreement",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceAgreement",
                table: "AspNetUsers");
        }
    }
}
