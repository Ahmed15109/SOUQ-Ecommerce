using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Migrations
{
    /// <inheritdoc />
    public partial class HardenCommerceAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductWeightTiers_ProductId",
                table: "ProductWeightTiers");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DbCartItems_CartId",
                table: "DbCartItems");

            migrationBuilder.DropIndex(
                name: "IX_Carts_UserId",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses");

            // Normalize legacy rows before adding stricter lengths, constraints, and unique indexes.
            // This keeps the migration deployable against databases that already contain application data.
            migrationBuilder.Sql(
                """
                UPDATE [Products]
                SET [Name] = LEFT([Name], 150),
                    [ImageUrl] = LEFT([ImageUrl], 500),
                    [Description] = LEFT([Description], 2000),
                    [SellingMode] = CASE WHEN [SellingMode] IN (0, 1) THEN [SellingMode] ELSE 0 END,
                    [AllowCutting] = CASE WHEN [SellingMode] = 1 THEN [AllowCutting] ELSE 0 END,
                    [CuttingFee] = CASE WHEN [SellingMode] = 1 AND [CuttingFee] >= 0 THEN [CuttingFee] ELSE 0 END,
                    [MinKg] = CASE WHEN [SellingMode] = 1 AND [MinKg] > 0 THEN [MinKg] WHEN [SellingMode] = 1 THEN 0.10 ELSE NULL END,
                    [MaxKg] = CASE WHEN [SellingMode] = 1
                                            AND [MaxKg] > CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
                                   THEN [MaxKg] WHEN [SellingMode] = 1
                                   THEN CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END + 1 ELSE NULL END,
                    [StepKg] = CASE WHEN [SellingMode] = 1
                                         AND [StepKg] > 0
                                         AND [StepKg] <=
                                             (CASE WHEN [MaxKg] > CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
                                                   THEN [MaxKg] ELSE CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END + 1 END)
                                             - CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
                                    THEN [StepKg] WHEN [SellingMode] = 1 THEN 0.10 ELSE NULL END,
                    [PricePerKg] = CASE WHEN [SellingMode] = 1 AND [PricePerKg] > 0 THEN [PricePerKg]
                                       WHEN [SellingMode] = 1 AND [Price] > 0 THEN [Price]
                                       WHEN [SellingMode] = 1 THEN 0.01 ELSE [PricePerKg] END;

                UPDATE [Products]
                SET [ImageUrl] = N'/img/placeholder.png'
                WHERE [ImageUrl] LIKE N'http://via.placeholder.com/%'
                   OR [ImageUrl] LIKE N'https://via.placeholder.com/%';

                UPDATE [ProductWeightTiers]
                SET [FromKg] = CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END,
                    [ToKg] = CASE WHEN [ToKg] > CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END
                                  THEN [ToKg] ELSE CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END + 0.01 END,
                    [PricePerKg] = CASE WHEN [PricePerKg] > 0 THEN [PricePerKg] ELSE 0.01 END;

                ;WITH [DuplicateTiers] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [ProductId], [FromKg], [ToKg]
                               ORDER BY [Id]) AS [RowNumber]
                    FROM [ProductWeightTiers]
                )
                DELETE [tier]
                FROM [ProductWeightTiers] AS [tier]
                INNER JOIN [DuplicateTiers] AS [duplicate] ON [duplicate].[Id] = [tier].[Id]
                WHERE [duplicate].[RowNumber] > 1;

                UPDATE [PharmacyRequests]
                SET [UserPhone] = CASE WHEN [UserPhone] IS NULL THEN NULL ELSE LEFT([UserPhone], 20) END,
                    [PrescriptionImagePath] = CASE WHEN [PrescriptionImagePath] IS NULL THEN NULL ELSE LEFT([PrescriptionImagePath], 255) END,
                    [Notes] = CASE WHEN [Notes] IS NULL THEN NULL ELSE LEFT([Notes], 1000) END,
                    [FullName] = LEFT([FullName], 100),
                    [Address] = LEFT([Address], 250);

                UPDATE [PharmacyRequestItems]
                SET [MedicineName] = LEFT([MedicineName], 150);

                UPDATE [Orders]
                SET [Street] = LEFT([Street], 200),
                    [Phone] = LEFT([Phone], 20),
                    [Notes] = LEFT([Notes], 500),
                    [FullName] = LEFT([FullName], 100),
                    [City] = LEFT([City], 100),
                    [Building] = LEFT([Building], 50),
                    [Area] = LEFT([Area], 100);

                UPDATE [OrderItems]
                SET [ProductName] = LEFT([ProductName], 150),
                    [ImageUrl] = LEFT([ImageUrl], 500),
                    [CuttingSelected] = COALESCE([CuttingSelected], 0);

                UPDATE [Notifications]
                SET [Title] = LEFT([Title], 150),
                    [Message] = LEFT([Message], 1000);

                UPDATE [DbCartItems]
                SET [ProductNameSnapshot] = LEFT([ProductNameSnapshot], 150),
                    [ImageUrlSnapshot] = LEFT([ImageUrlSnapshot], 500),
                    [CuttingSelected] = COALESCE([CuttingSelected], 0),
                    [Quantity] = CASE WHEN [Quantity] < 1 THEN 1 WHEN [Quantity] > 100 THEN 100 ELSE [Quantity] END;

                ;WITH [CartMap] AS
                (
                    SELECT [Id],
                           MIN([Id]) OVER (PARTITION BY [UserId]) AS [KeepId]
                    FROM [Carts]
                )
                UPDATE [item]
                SET [CartId] = [map].[KeepId]
                FROM [DbCartItems] AS [item]
                INNER JOIN [CartMap] AS [map] ON [map].[Id] = [item].[CartId];

                ;WITH [DuplicateCarts] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY [Id]) AS [RowNumber]
                    FROM [Carts]
                )
                DELETE [cart]
                FROM [Carts] AS [cart]
                INNER JOIN [DuplicateCarts] AS [duplicate] ON [duplicate].[Id] = [cart].[Id]
                WHERE [duplicate].[RowNumber] > 1;

                ;WITH [CartLineTotals] AS
                (
                    SELECT MIN([Id]) AS [KeepId],
                           SUM(CONVERT(bigint, [Quantity])) AS [TotalQuantity]
                    FROM [DbCartItems]
                    GROUP BY [CartId], [ProductId], [SelectedWeightKg], [CuttingSelected]
                )
                UPDATE [item]
                SET [Quantity] = CASE WHEN [totals].[TotalQuantity] > 100 THEN 100 ELSE [totals].[TotalQuantity] END
                FROM [DbCartItems] AS [item]
                INNER JOIN [CartLineTotals] AS [totals] ON [totals].[KeepId] = [item].[Id];

                ;WITH [DuplicateCartLines] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [CartId], [ProductId], [SelectedWeightKg], [CuttingSelected]
                               ORDER BY [Id]) AS [RowNumber]
                    FROM [DbCartItems]
                )
                DELETE [item]
                FROM [DbCartItems] AS [item]
                INNER JOIN [DuplicateCartLines] AS [duplicate] ON [duplicate].[Id] = [item].[Id]
                WHERE [duplicate].[RowNumber] > 1;

                ;WITH [DefaultAddresses] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY [Id]) AS [RowNumber]
                    FROM [Addresses]
                    WHERE [IsDefault] = 1
                )
                UPDATE [address]
                SET [IsDefault] = 0
                FROM [Addresses] AS [address]
                INNER JOIN [DefaultAddresses] AS [defaults] ON [defaults].[Id] = [address].[Id]
                WHERE [defaults].[RowNumber] > 1;

                UPDATE [Categories]
                SET [Name] = CASE WHEN LEN(LTRIM(RTRIM([Name]))) = 0
                                  THEN CONCAT(N'Category ', [Id])
                                  ELSE LEFT(LTRIM(RTRIM([Name])), 100) END,
                    [IconKey] = CASE WHEN [IconKey] IS NULL THEN NULL ELSE LEFT([IconKey], 50) END,
                    [IconClass] = CASE WHEN [IconClass] IS NULL THEN NULL ELSE LEFT([IconClass], 100) END,
                    [IconColor] = CASE WHEN [IconColor] IS NULL THEN NULL ELSE LEFT([IconColor], 20) END,
                    [IconBgColor] = CASE WHEN [IconBgColor] IS NULL THEN NULL ELSE LEFT([IconBgColor], 20) END;

                ;WITH [CategoryMap] AS
                (
                    SELECT [Id],
                           MIN([Id]) OVER (PARTITION BY [Name]) AS [KeepId]
                    FROM [Categories]
                )
                UPDATE [product]
                SET [CategoryId] = [map].[KeepId]
                FROM [Products] AS [product]
                INNER JOIN [CategoryMap] AS [map] ON [map].[Id] = [product].[CategoryId];

                ;WITH [DuplicateCategories] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (PARTITION BY [Name] ORDER BY [Id]) AS [RowNumber]
                    FROM [Categories]
                )
                DELETE [category]
                FROM [Categories] AS [category]
                INNER JOIN [DuplicateCategories] AS [duplicate] ON [duplicate].[Id] = [category].[Id]
                WHERE [duplicate].[RowNumber] > 1;

                UPDATE [AspNetUsers]
                SET [FullName] = LEFT([FullName], 100);

                UPDATE [Addresses]
                SET [Street] = LEFT([Street], 200),
                    [Notes] = LEFT([Notes], 500),
                    [City] = LEFT([City], 100),
                    [Building] = LEFT([Building], 50),
                    [Area] = LEFT([Area], 100);
                """);

            migrationBuilder.RenameColumn(
                name: "IsFavorite",
                table: "Products",
                newName: "IsFeatured");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductWeightTiers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "UserPhone",
                table: "PharmacyRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PharmacyRequests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrescriptionImagePath",
                table: "PharmacyRequests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PharmacyRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "PharmacyRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "PharmacyRequests",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionToken",
                table: "PharmacyRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MedicineName",
                table: "PharmacyRequestItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Street",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Area",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "OrderItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "OrderItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "CuttingSelected",
                table: "OrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ProductNameSnapshot",
                table: "DbCartItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrlSnapshot",
                table: "DbCartItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "CuttingSelected",
                table: "DbCartItems",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "IconKey",
                table: "Categories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconColor",
                table: "Categories",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconClass",
                table: "Categories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconBgColor",
                table: "Categories",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Categories",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Street",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Addresses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Addresses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Area",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFavorites_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsCore",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsCore",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsCore",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsCore",
                value: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsCore",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductWeightTiers_ProductId_FromKg_ToKg",
                table: "ProductWeightTiers",
                columns: new[] { "ProductId", "FromKg", "ToKg" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductWeightTiers_ValidRange",
                table: "ProductWeightTiers",
                sql: "[FromKg] > 0 AND [ToKg] > [FromKg] AND [PricePerKg] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_WeightConfiguration",
                table: "Products",
                sql: "([SellingMode] = 0 AND [AllowCutting] = 0 AND [CuttingFee] = 0) OR ([SellingMode] = 1 AND [MinKg] > 0 AND [MaxKg] > [MinKg] AND [StepKg] > 0 AND [PricePerKg] > 0 AND [CuttingFee] >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyRequests_UserId_CreatedAt",
                table: "PharmacyRequests",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyRequests_UserId_SubmissionToken",
                table: "PharmacyRequests",
                columns: new[] { "UserId", "SubmissionToken" },
                unique: true,
                filter: "[SubmissionToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsForAdmin_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "IsForAdmin", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsForAdmin_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsForAdmin", "IsRead", "CreatedAt" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DbCartItems_NormalLine",
                table: "DbCartItems",
                columns: new[] { "CartId", "ProductId", "CuttingSelected" },
                unique: true,
                filter: "[SelectedWeightKg] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DbCartItems_WeightedLine",
                table: "DbCartItems",
                columns: new[] { "CartId", "ProductId", "SelectedWeightKg", "CuttingSelected" },
                unique: true,
                filter: "[SelectedWeightKg] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DbCartItems_Quantity",
                table: "DbCartItems",
                sql: "[Quantity] >= 1 AND [Quantity] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses",
                column: "UserId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_NotificationId_UserId",
                table: "NotificationReads",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_UserId",
                table: "NotificationReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavorites_ProductId",
                table: "UserFavorites",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavorites_UserId_ProductId",
                table: "UserFavorites",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "UserFavorites");

            migrationBuilder.DropIndex(
                name: "IX_ProductWeightTiers_ProductId_FromKg_ToKg",
                table: "ProductWeightTiers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductWeightTiers_ValidRange",
                table: "ProductWeightTiers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_WeightConfiguration",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyRequests_UserId_CreatedAt",
                table: "PharmacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyRequests_UserId_SubmissionToken",
                table: "PharmacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsForAdmin_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsForAdmin_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DbCartItems_NormalLine",
                table: "DbCartItems");

            migrationBuilder.DropIndex(
                name: "IX_DbCartItems_WeightedLine",
                table: "DbCartItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DbCartItems_Quantity",
                table: "DbCartItems");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Carts_UserId",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductWeightTiers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SubmissionToken",
                table: "PharmacyRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "IsFeatured",
                table: "Products",
                newName: "IsFavorite");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "UserPhone",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrescriptionImagePath",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "PharmacyRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "MedicineName",
                table: "PharmacyRequestItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Street",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Area",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<bool>(
                name: "CuttingSelected",
                table: "OrderItems",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "ProductNameSnapshot",
                table: "DbCartItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrlSnapshot",
                table: "DbCartItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<bool>(
                name: "CuttingSelected",
                table: "DbCartItems",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "IconKey",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconColor",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconClass",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconBgColor",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Street",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Area",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsCore",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsCore",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsCore",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsCore",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsCore",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductWeightTiers_ProductId",
                table: "ProductWeightTiers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DbCartItems_CartId",
                table: "DbCartItems",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
