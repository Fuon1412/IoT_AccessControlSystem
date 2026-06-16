using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTAccessAPI.Migrations
{
    /// <inheritdoc />
    public partial class CardAssignment_EmployeeRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RfidCards_Users_UserId",
                table: "RfidCards");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "RfidCards",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "IsAssigned",
                table: "RfidCards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_RfidCards_Users_UserId",
                table: "RfidCards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RfidCards_Users_UserId",
                table: "RfidCards");

            migrationBuilder.DropColumn(
                name: "IsAssigned",
                table: "RfidCards");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "RfidCards",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RfidCards_Users_UserId",
                table: "RfidCards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
