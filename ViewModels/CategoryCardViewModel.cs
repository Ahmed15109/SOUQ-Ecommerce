namespace EcommerceApp.ViewModels;

public sealed class CategoryCardViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string IconClass { get; init; } = "bi bi-tag";
    public string ThemeClass { get; init; } = "category-default";
    public string? IconColor { get; init; }
    public string? IconBackgroundColor { get; init; }
    public bool LinksToPharmacyRequests { get; init; }
}
