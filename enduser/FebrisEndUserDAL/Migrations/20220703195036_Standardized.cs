using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Febris.UserNode.DataAccessLayer.Migrations
{
    public partial class Standardized : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdateTimeStamp",
                table: "UserAnalytics");

            migrationBuilder.DropColumn(
                name: "UpdateTimeStamp",
                table: "LocalAnalytics");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "UserAnalytics",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "LocalAnalytics",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdateTimeStamp",
                table: "UserAnalytics");

            migrationBuilder.DropColumn(
                name: "LastUpdateTimeStamp",
                table: "LocalAnalytics");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateTimeStamp",
                table: "UserAnalytics",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateTimeStamp",
                table: "LocalAnalytics",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
