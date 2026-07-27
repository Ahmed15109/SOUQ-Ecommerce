using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Options
{
    public class EmailSettings : IValidatableObject
    {
        public const string SectionName = "Email";

        public string Host { get; set; } = string.Empty;

        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string FromAddress { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FromName { get; set; } = "SOUQ";
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var hasHost = !string.IsNullOrWhiteSpace(Host);
            var hasFromAddress = !string.IsNullOrWhiteSpace(FromAddress);

            if (hasHost != hasFromAddress)
            {
                yield return new ValidationResult(
                    "Email Host and FromAddress must either both be configured or both be empty.",
                    [nameof(Host), nameof(FromAddress)]);
            }

            if (hasFromAddress && !new EmailAddressAttribute().IsValid(FromAddress))
            {
                yield return new ValidationResult(
                    "Email FromAddress must be a valid email address.",
                    [nameof(FromAddress)]);
            }

            if (!string.IsNullOrWhiteSpace(UserName) && string.IsNullOrWhiteSpace(Password))
            {
                yield return new ValidationResult(
                    "Email Password is required when UserName is configured.",
                    [nameof(Password)]);
            }
        }
    }
}
