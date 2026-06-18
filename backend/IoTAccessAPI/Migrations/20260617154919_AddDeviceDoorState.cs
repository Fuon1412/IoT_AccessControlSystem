using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTAccessAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceDoorState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DoorState",
                table: "Devices",
                type: "text",
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDoorStateChange",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoorState",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastDoorStateChange",
                table: "Devices");
        }
    }
}
