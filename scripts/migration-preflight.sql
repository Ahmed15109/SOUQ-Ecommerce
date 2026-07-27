/*
    SOUQ hardening-migration preflight.

    This script is read-only: it contains no INSERT, UPDATE, DELETE, MERGE, DDL,
    migration application, or transaction-state changes. Run it against a restored
    production copy before reviewing or applying 20260726155003_HardenCommerceAndSecurity.
*/

SET NOCOUNT ON;

SELECT
    DB_NAME() AS [DatabaseName],
    @@SERVERNAME AS [ServerName],
    SYSUTCDATETIME() AS [PreflightUtc],
    CAST(SESSIONPROPERTY('ANSI_NULLS') AS int) AS [AnsiNulls];

IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    SELECT
        N'__EFMigrationsHistory is absent' AS [MigrationHistoryStatus],
        CAST(NULL AS nvarchar(150)) AS [MigrationId],
        CAST(NULL AS nvarchar(32)) AS [ProductVersion];
END
ELSE
BEGIN
    SELECT
        N'__EFMigrationsHistory is present' AS [MigrationHistoryStatus],
        [MigrationId],
        [ProductVersion]
    FROM [__EFMigrationsHistory]
    ORDER BY [MigrationId];
END;

IF EXISTS
(
    SELECT 1
    FROM
    (
        VALUES
            (N'Products'),
            (N'ProductWeightTiers'),
            (N'PharmacyRequests'),
            (N'PharmacyRequestItems'),
            (N'Orders'),
            (N'OrderItems'),
            (N'Notifications'),
            (N'Carts'),
            (N'DbCartItems'),
            (N'Categories'),
            (N'AspNetUsers'),
            (N'Addresses')
    ) AS [required]([TableName])
    WHERE OBJECT_ID(N'[dbo].' + QUOTENAME([required].[TableName]), N'U') IS NULL
)
BEGIN
    SELECT [required].[TableName] AS [MissingRequiredTable]
    FROM
    (
        VALUES
            (N'Products'),
            (N'ProductWeightTiers'),
            (N'PharmacyRequests'),
            (N'PharmacyRequestItems'),
            (N'Orders'),
            (N'OrderItems'),
            (N'Notifications'),
            (N'Carts'),
            (N'DbCartItems'),
            (N'Categories'),
            (N'AspNetUsers'),
            (N'Addresses')
    ) AS [required]([TableName])
    WHERE OBJECT_ID(N'[dbo].' + QUOTENAME([required].[TableName]), N'U') IS NULL;

    THROW 51000, 'Preflight stopped because one or more historical schema tables are missing.', 1;
END;

/* Duplicate carts. The hardening migration keeps the lowest Cart.Id per UserId. */
SELECT
    [UserId],
    COUNT_BIG(*) AS [CartCount],
    MIN([Id]) AS [KeepCartId],
    COUNT_BIG(*) - 1 AS [CartRowsToDelete]
FROM [Carts]
GROUP BY [UserId]
HAVING COUNT_BIG(*) > 1
ORDER BY [UserId];

/* Duplicate logical cart lines after duplicate carts are mapped to the retained cart. */
;WITH [CartMap] AS
(
    SELECT
        [Id],
        MIN([Id]) OVER (PARTITION BY [UserId]) AS [KeepCartId]
    FROM [Carts]
),
[LogicalCartLines] AS
(
    SELECT
        [map].[KeepCartId],
        [item].[ProductId],
        [item].[SelectedWeightKg],
        COALESCE([item].[CuttingSelected], CAST(0 AS bit)) AS [NormalizedCuttingSelected],
        [item].[Id],
        CASE
            WHEN [item].[Quantity] < 1 THEN 1
            WHEN [item].[Quantity] > 100 THEN 100
            ELSE [item].[Quantity]
        END AS [NormalizedQuantity]
    FROM [DbCartItems] AS [item]
    INNER JOIN [CartMap] AS [map] ON [map].[Id] = [item].[CartId]
)
SELECT
    [KeepCartId],
    [ProductId],
    [SelectedWeightKg],
    [NormalizedCuttingSelected],
    COUNT_BIG(*) AS [LogicalLineCount],
    COUNT_BIG(*) - 1 AS [CartLineRowsToDelete],
    SUM(CONVERT(bigint, [NormalizedQuantity])) AS [MergedQuantityBeforeCap],
    CASE
        WHEN SUM(CONVERT(bigint, [NormalizedQuantity])) > 100 THEN 100
        ELSE SUM(CONVERT(bigint, [NormalizedQuantity]))
    END AS [MergedQuantityAfterCap]
FROM [LogicalCartLines]
GROUP BY
    [KeepCartId],
    [ProductId],
    [SelectedWeightKg],
    [NormalizedCuttingSelected]
HAVING COUNT_BIG(*) > 1
ORDER BY [KeepCartId], [ProductId];

/* Duplicate category names after the migration's trim/blank/truncate normalization. */
;WITH [NormalizedCategories] AS
(
    SELECT
        [Id],
        CASE
            WHEN LEN(LTRIM(RTRIM([Name]))) = 0 THEN CONCAT(N'Category ', [Id])
            ELSE LEFT(LTRIM(RTRIM([Name])), 100)
        END AS [NormalizedName]
    FROM [Categories]
)
SELECT
    [NormalizedName],
    COUNT_BIG(*) AS [CategoryCount],
    MIN([Id]) AS [KeepCategoryId],
    COUNT_BIG(*) - 1 AS [CategoryRowsToDelete]
FROM [NormalizedCategories]
GROUP BY [NormalizedName]
HAVING COUNT_BIG(*) > 1
ORDER BY [NormalizedName];

/* Users with multiple default addresses; the lowest Address.Id remains default. */
SELECT
    [UserId],
    COUNT_BIG(*) AS [DefaultAddressCount],
    MIN([Id]) AS [KeepDefaultAddressId],
    COUNT_BIG(*) - 1 AS [DefaultFlagsToClear]
FROM [Addresses]
WHERE [IsDefault] = 1
GROUP BY [UserId]
HAVING COUNT_BIG(*) > 1
ORDER BY [UserId];

/* Per-column counts of values that the hardening migration truncates with LEFT(...). */
SELECT N'Products.Name' AS [ColumnName], SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Name]) / 2 > 150 THEN 1 ELSE 0 END)) AS [RowsToTruncate] FROM [Products]
UNION ALL SELECT N'Products.ImageUrl', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([ImageUrl]) / 2 > 500 THEN 1 ELSE 0 END)) FROM [Products]
UNION ALL SELECT N'Products.Description', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Description]) / 2 > 2000 THEN 1 ELSE 0 END)) FROM [Products]
UNION ALL SELECT N'PharmacyRequests.UserPhone', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([UserPhone]) / 2 > 20 THEN 1 ELSE 0 END)) FROM [PharmacyRequests]
UNION ALL SELECT N'PharmacyRequests.PrescriptionImagePath', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([PrescriptionImagePath]) / 2 > 255 THEN 1 ELSE 0 END)) FROM [PharmacyRequests]
UNION ALL SELECT N'PharmacyRequests.Notes', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Notes]) / 2 > 1000 THEN 1 ELSE 0 END)) FROM [PharmacyRequests]
UNION ALL SELECT N'PharmacyRequests.FullName', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([FullName]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [PharmacyRequests]
UNION ALL SELECT N'PharmacyRequests.Address', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Address]) / 2 > 250 THEN 1 ELSE 0 END)) FROM [PharmacyRequests]
UNION ALL SELECT N'PharmacyRequestItems.MedicineName', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([MedicineName]) / 2 > 150 THEN 1 ELSE 0 END)) FROM [PharmacyRequestItems]
UNION ALL SELECT N'Orders.Street', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Street]) / 2 > 200 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.Phone', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Phone]) / 2 > 20 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.Notes', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Notes]) / 2 > 500 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.FullName', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([FullName]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.City', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([City]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.Building', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Building]) / 2 > 50 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'Orders.Area', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Area]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Orders]
UNION ALL SELECT N'OrderItems.ProductName', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([ProductName]) / 2 > 150 THEN 1 ELSE 0 END)) FROM [OrderItems]
UNION ALL SELECT N'OrderItems.ImageUrl', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([ImageUrl]) / 2 > 500 THEN 1 ELSE 0 END)) FROM [OrderItems]
UNION ALL SELECT N'Notifications.Title', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Title]) / 2 > 150 THEN 1 ELSE 0 END)) FROM [Notifications]
UNION ALL SELECT N'Notifications.Message', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Message]) / 2 > 1000 THEN 1 ELSE 0 END)) FROM [Notifications]
UNION ALL SELECT N'DbCartItems.ProductNameSnapshot', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([ProductNameSnapshot]) / 2 > 150 THEN 1 ELSE 0 END)) FROM [DbCartItems]
UNION ALL SELECT N'DbCartItems.ImageUrlSnapshot', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([ImageUrlSnapshot]) / 2 > 500 THEN 1 ELSE 0 END)) FROM [DbCartItems]
UNION ALL SELECT N'Categories.Name', SUM(CONVERT(bigint, CASE WHEN DATALENGTH(LTRIM(RTRIM([Name]))) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Categories]
UNION ALL SELECT N'Categories.IconKey', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([IconKey]) / 2 > 50 THEN 1 ELSE 0 END)) FROM [Categories]
UNION ALL SELECT N'Categories.IconClass', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([IconClass]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Categories]
UNION ALL SELECT N'Categories.IconColor', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([IconColor]) / 2 > 20 THEN 1 ELSE 0 END)) FROM [Categories]
UNION ALL SELECT N'Categories.IconBgColor', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([IconBgColor]) / 2 > 20 THEN 1 ELSE 0 END)) FROM [Categories]
UNION ALL SELECT N'AspNetUsers.FullName', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([FullName]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [AspNetUsers]
UNION ALL SELECT N'Addresses.Street', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Street]) / 2 > 200 THEN 1 ELSE 0 END)) FROM [Addresses]
UNION ALL SELECT N'Addresses.Notes', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Notes]) / 2 > 500 THEN 1 ELSE 0 END)) FROM [Addresses]
UNION ALL SELECT N'Addresses.City', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([City]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Addresses]
UNION ALL SELECT N'Addresses.Building', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Building]) / 2 > 50 THEN 1 ELSE 0 END)) FROM [Addresses]
UNION ALL SELECT N'Addresses.Area', SUM(CONVERT(bigint, CASE WHEN DATALENGTH([Area]) / 2 > 100 THEN 1 ELSE 0 END)) FROM [Addresses]
ORDER BY [ColumnName];

/* Products whose weight-related values are changed by the migration's normalization. */
SELECT
    [Id],
    [SellingMode],
    [MinKg],
    [MaxKg],
    [StepKg],
    [PricePerKg],
    [AllowCutting],
    [CuttingFee]
FROM [Products]
WHERE
    [SellingMode] NOT IN (0, 1)
    OR
    (
        [SellingMode] <> 1
        AND
        (
            [MinKg] IS NOT NULL
            OR [MaxKg] IS NOT NULL
            OR [StepKg] IS NOT NULL
            OR [AllowCutting] <> 0
            OR [CuttingFee] <> 0
        )
    )
    OR
    (
        [SellingMode] = 1
        AND
        (
            [MinKg] IS NULL
            OR [MinKg] <= 0
            OR [MaxKg] IS NULL
            OR [MaxKg] <= CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
            OR [StepKg] IS NULL
            OR [StepKg] <= 0
            OR [StepKg] >
                (
                    CASE
                        WHEN [MaxKg] > CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
                            THEN [MaxKg]
                        ELSE CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END + 1
                    END
                    - CASE WHEN [MinKg] > 0 THEN [MinKg] ELSE 0.10 END
                )
            OR [PricePerKg] <= 0
            OR [CuttingFee] < 0
        )
    )
ORDER BY [Id];

/* Tiers changed by normalization, followed by duplicate tiers after normalization. */
SELECT
    [Id],
    [ProductId],
    [FromKg],
    [ToKg],
    [PricePerKg]
FROM [ProductWeightTiers]
WHERE
    [FromKg] <= 0
    OR [ToKg] <= CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END
    OR [PricePerKg] <= 0
ORDER BY [ProductId], [Id];

;WITH [NormalizedTiers] AS
(
    SELECT
        [Id],
        [ProductId],
        CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END AS [NormalizedFromKg],
        CASE
            WHEN [ToKg] > CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END THEN [ToKg]
            ELSE CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END + 0.01
        END AS [NormalizedToKg]
    FROM [ProductWeightTiers]
)
SELECT
    [ProductId],
    [NormalizedFromKg],
    [NormalizedToKg],
    COUNT_BIG(*) AS [TierCount],
    MIN([Id]) AS [KeepTierId],
    COUNT_BIG(*) - 1 AS [TierRowsToDelete]
FROM [NormalizedTiers]
GROUP BY [ProductId], [NormalizedFromKg], [NormalizedToKg]
HAVING COUNT_BIG(*) > 1
ORDER BY [ProductId], [NormalizedFromKg], [NormalizedToKg];

/* Counts of rows deleted, merged, repointed, reset, or capped by the hardening migration. */
SELECT N'DuplicateCartRowsDeleted' AS [Operation], COALESCE(SUM([grouped].[RowCount] - 1), 0) AS [AffectedCount]
FROM (SELECT COUNT_BIG(*) AS [RowCount] FROM [Carts] GROUP BY [UserId] HAVING COUNT_BIG(*) > 1) AS [grouped]
UNION ALL
SELECT N'CartItemsRepointedToRetainedCart', COUNT_BIG(*)
FROM [DbCartItems] AS [item]
INNER JOIN
(
    SELECT [Id], MIN([Id]) OVER (PARTITION BY [UserId]) AS [KeepCartId]
    FROM [Carts]
) AS [map] ON [map].[Id] = [item].[CartId]
WHERE [item].[CartId] <> [map].[KeepCartId]
UNION ALL
SELECT N'DuplicateLogicalCartLineRowsDeleted', COALESCE(SUM([grouped].[RowCount] - 1), 0)
FROM
(
    SELECT COUNT_BIG(*) AS [RowCount]
    FROM [DbCartItems] AS [item]
    INNER JOIN
    (
        SELECT [Id], MIN([Id]) OVER (PARTITION BY [UserId]) AS [KeepCartId]
        FROM [Carts]
    ) AS [map] ON [map].[Id] = [item].[CartId]
    GROUP BY
        [map].[KeepCartId],
        [item].[ProductId],
        [item].[SelectedWeightKg],
        COALESCE([item].[CuttingSelected], CAST(0 AS bit))
    HAVING COUNT_BIG(*) > 1
) AS [grouped]
UNION ALL
SELECT N'DuplicateCategoryRowsDeleted', COALESCE(SUM([grouped].[RowCount] - 1), 0)
FROM
(
    SELECT COUNT_BIG(*) AS [RowCount]
    FROM [Categories]
    GROUP BY
        CASE
            WHEN LEN(LTRIM(RTRIM([Name]))) = 0 THEN CONCAT(N'Category ', [Id])
            ELSE LEFT(LTRIM(RTRIM([Name])), 100)
        END
    HAVING COUNT_BIG(*) > 1
) AS [grouped]
UNION ALL
SELECT N'ProductsRepointedToRetainedCategory', COUNT_BIG(*)
FROM [Products] AS [product]
INNER JOIN
(
    SELECT
        [Id],
        MIN([Id]) OVER
        (
            PARTITION BY
                CASE
                    WHEN LEN(LTRIM(RTRIM([Name]))) = 0 THEN CONCAT(N'Category ', [Id])
                    ELSE LEFT(LTRIM(RTRIM([Name])), 100)
                END
        ) AS [KeepCategoryId]
    FROM [Categories]
) AS [map] ON [map].[Id] = [product].[CategoryId]
WHERE [product].[CategoryId] <> [map].[KeepCategoryId]
UNION ALL
SELECT N'DuplicateTierRowsDeleted', COALESCE(SUM([grouped].[RowCount] - 1), 0)
FROM
(
    SELECT COUNT_BIG(*) AS [RowCount]
    FROM [ProductWeightTiers]
    GROUP BY
        [ProductId],
        CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END,
        CASE
            WHEN [ToKg] > CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END THEN [ToKg]
            ELSE CASE WHEN [FromKg] > 0 THEN [FromKg] ELSE 0.01 END + 0.01
        END
    HAVING COUNT_BIG(*) > 1
) AS [grouped]
UNION ALL
SELECT N'DefaultAddressFlagsCleared', COALESCE(SUM([grouped].[RowCount] - 1), 0)
FROM
(
    SELECT COUNT_BIG(*) AS [RowCount]
    FROM [Addresses]
    WHERE [IsDefault] = 1
    GROUP BY [UserId]
    HAVING COUNT_BIG(*) > 1
) AS [grouped]
UNION ALL
SELECT N'CartRowsWithQuantityNormalized', COUNT_BIG(*)
FROM [DbCartItems]
WHERE [Quantity] < 1 OR [Quantity] > 100
UNION ALL
SELECT N'OrderItemsWithNullCuttingFlagNormalized', COUNT_BIG(*)
FROM [OrderItems]
WHERE [CuttingSelected] IS NULL
UNION ALL
SELECT N'CartItemsWithNullCuttingFlagNormalized', COUNT_BIG(*)
FROM [DbCartItems]
WHERE [CuttingSelected] IS NULL
UNION ALL
SELECT N'LegacyPlaceholderProductUrlsChanged', COUNT_BIG(*)
FROM [Products]
WHERE [ImageUrl] LIKE N'http://via.placeholder.com/%'
   OR [ImageUrl] LIKE N'https://via.placeholder.com/%'
ORDER BY [Operation];
