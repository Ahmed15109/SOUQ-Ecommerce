using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePharmacyStatusAndQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert Status string values to int string values
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '0' WHERE Status = 'New'");
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '1' WHERE Status = 'Processing'");
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '2' WHERE Status = 'Shipped'");
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '3' WHERE Status = 'Delivered'");
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '4' WHERE Status = 'Cancelled'");
            // Default valid fallback
            migrationBuilder.Sql("UPDATE PharmacyRequests SET Status = '0' WHERE TRY_CAST(Status AS INT) IS NULL");

            // Clean Quantity data
            migrationBuilder.Sql("UPDATE PharmacyRequestItems SET Quantity = '1' WHERE Quantity IS NULL OR TRY_CAST(Quantity AS INT) IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "PharmacyRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "PharmacyRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Quantity",
                table: "PharmacyRequestItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
