using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    public partial class hardwaremodulelinkupdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "HardwareLinkedModule",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "HardwareLinkedModule",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "HardwareLinkedModule",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "HardwareLinkedModule",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "HardwareLinkedModule",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "HardwareLinkedModule",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
