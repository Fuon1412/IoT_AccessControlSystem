using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTAccessAPI.Migrations
{
    /// <inheritdoc />
    public partial class UserFullName_DeviceNameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "Employee",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "User");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Dedupe devices by Name before adding the unique index.
            // Repoint access logs to the surviving (lowest-Id) device, then delete dups.
            migrationBuilder.Sql(@"
                UPDATE ""AccessLogs"" al
                SET ""DeviceId"" = keep.min_id
                FROM (SELECT ""Name"", MIN(""Id"") AS min_id FROM ""Devices"" GROUP BY ""Name"") keep
                JOIN ""Devices"" d ON d.""Name"" = keep.""Name""
                WHERE al.""DeviceId"" = d.""Id"" AND d.""Id"" <> keep.min_id;

                DELETE FROM ""Devices"" d
                USING (SELECT ""Name"", MIN(""Id"") AS min_id FROM ""Devices"" GROUP BY ""Name"") keep
                WHERE d.""Name"" = keep.""Name"" AND d.""Id"" <> keep.min_id;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Name",
                table: "Devices",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_Name",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "User",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Employee");
        }
    }
}
