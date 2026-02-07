using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPharmacyToRelational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS PharmacyRequestItems");
            migrationBuilder.Sql("DROP TABLE IF EXISTS PharmacyRequests");

            // PharmacyRequests
            migrationBuilder.CreateTable(
                name: "PharmacyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionImagePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyRequests", x => x.Id);
                });

            // PharmacyRequestItems
            migrationBuilder.CreateTable(
                name: "PharmacyRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PharmacyRequestId = table.Column<int>(type: "int", nullable: false),
                    MedicineName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyRequestItems_PharmacyRequests_PharmacyRequestId",
                        column: x => x.PharmacyRequestId,
                        principalTable: "PharmacyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Index for RequestItems
            migrationBuilder.CreateIndex(
                name: "IX_PharmacyRequestItems_PharmacyRequestId",
                table: "PharmacyRequestItems",
                column: "PharmacyRequestId");

            // Update Notifications
            migrationBuilder.AddColumn<int>(
                name: "PharmacyRequestId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PharmacyRequestId",
                table: "Notifications",
                column: "PharmacyRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_PharmacyRequests_PharmacyRequestId",
                table: "Notifications",
                column: "PharmacyRequestId",
                principalTable: "PharmacyRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
             migrationBuilder.DropForeignKey(
                name: "FK_Notifications_PharmacyRequests_PharmacyRequestId",
                table: "Notifications");

             migrationBuilder.DropIndex(
                name: "IX_Notifications_PharmacyRequestId",
                table: "Notifications");

             migrationBuilder.DropColumn(
                name: "PharmacyRequestId",
                table: "Notifications");

             migrationBuilder.DropTable(name: "PharmacyRequestItems");
             migrationBuilder.DropTable(name: "PharmacyRequests");
        }
    }
}
