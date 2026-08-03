namespace EcommerceApp.Constants
{
    public static class CommerceLimits
    {
        public const int MaxQuantityPerLine = 100;
        public const int MaxTotalCartQuantity = 1000;
        public const int MaxAnonymousFavorites = 50;
        public const int MaxAuthenticatedFavorites = 250;
        public const int MaxPharmacyMedicines = 25;
        public const int MaxPageSize = 100;
        public const long MaxUploadRequestSizeBytes = 6 * 1024 * 1024;

        public const decimal MaxWeightSafetyCeilingKg = 1000m;
    }
}
