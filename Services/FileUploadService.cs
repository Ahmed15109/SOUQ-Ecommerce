using System.Diagnostics;

namespace EcommerceApp.Services
{
    public interface IFileUploadService
    {
        Task<(bool IsValid, string? FilePath, string? ErrorMessage)> SaveImageAsync(
            IFormFile? file,
            string subFolder,
            CancellationToken cancellationToken = default);

        Task<(bool IsValid, string? FilePath, string? ErrorMessage)> SavePharmacyAttachmentAsync(
            IFormFile? file,
            CancellationToken cancellationToken = default);

        bool TryGetPharmacyAttachment(
            string? storedName,
            out string fullPath,
            out string contentType,
            out bool downloadAsAttachment);

        bool DeleteFile(string? relativeUrl);
    }

    public interface IFileSecurityScanner
    {
        Task<(bool IsClean, string? ErrorMessage)> ScanAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }

    public class ExternalFileSecurityScanner : IFileSecurityScanner
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ExternalFileSecurityScanner> _logger;

        public ExternalFileSecurityScanner(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<ExternalFileSecurityScanner> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task<(bool IsClean, string? ErrorMessage)> ScanAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (!_configuration.GetValue("Uploads:RequireMalwareScan", true))
            {
                return (true, null);
            }

            var command = _configuration["Uploads:MalwareScannerCommand"];
            if (string.IsNullOrWhiteSpace(command) || !File.Exists(command))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("Malware scanning is unavailable in development; upload scan was skipped.");
                    return (true, null);
                }

                return (false, "تعذر فحص الملف أمنيًا. حاول مرة أخرى لاحقًا.");
            }

            var configuredArguments =
                _configuration.GetSection("Uploads:MalwareScannerArguments").Get<string[]>() ?? ["{file}"];

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            foreach (var argument in configuredArguments)
            {
                process.StartInfo.ArgumentList.Add(argument.Replace("{file}", filePath, StringComparison.Ordinal));
            }

            try
            {
                process.Start();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(standardOutput, standardError);

                if (process.ExitCode == 0)
                {
                    return (true, null);
                }

                _logger.LogWarning(
                    "Malware scanner rejected an upload with exit code {ExitCode}.",
                    process.ExitCode);
                return (false, "تم رفض الملف بعد الفحص الأمني.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Malware scanner execution failed.");
                return (false, "تعذر فحص الملف أمنيًا. حاول مرة أخرى لاحقًا.");
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                return (false, "انتهت مهلة فحص الملف.");
            }
        }
    }

    public class FileUploadService : IFileUploadService
    {
        private const long MaxFileSize = 5 * 1024 * 1024;
        private readonly IWebHostEnvironment _environment;
        private readonly IFileSecurityScanner _scanner;
        private readonly ILogger<FileUploadService> _logger;

        private static readonly HashSet<string> AllowedPublicFolders =
            new(StringComparer.OrdinalIgnoreCase) { "products" };

        private static readonly Dictionary<string, byte[]> AllowedImageTypes =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = [0xFF, 0xD8, 0xFF],
                [".jpeg"] = [0xFF, 0xD8, 0xFF],
                [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
                [".webp"] = [0x52, 0x49, 0x46, 0x46]
            };

        private static readonly IReadOnlyDictionary<string, byte[]> AllowedPharmacyTypes =
            new Dictionary<string, byte[]>(AllowedImageTypes, StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = [0x25, 0x50, 0x44, 0x46]
            };

        public FileUploadService(
            IWebHostEnvironment environment,
            IFileSecurityScanner scanner,
            ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _scanner = scanner;
            _logger = logger;
        }

        public Task<(bool IsValid, string? FilePath, string? ErrorMessage)> SaveImageAsync(
            IFormFile? file,
            string subFolder,
            CancellationToken cancellationToken = default)
        {
            if (!AllowedPublicFolders.Contains(subFolder))
            {
                return Task.FromResult<(bool, string?, string?)>(
                    (false, null, "مسار رفع الملف غير مسموح."));
            }

            return ValidateAndSaveFileAsync(
                file,
                subFolder,
                AllowedImageTypes,
                isSecure: false,
                cancellationToken);
        }

        public Task<(bool IsValid, string? FilePath, string? ErrorMessage)> SavePharmacyAttachmentAsync(
            IFormFile? file,
            CancellationToken cancellationToken = default) =>
            ValidateAndSaveFileAsync(
                file,
                "pharmacy",
                AllowedPharmacyTypes,
                isSecure: true,
                cancellationToken);

        public bool TryGetPharmacyAttachment(
            string? storedName,
            out string fullPath,
            out string contentType,
            out bool downloadAsAttachment)
        {
            fullPath = string.Empty;
            contentType = "application/octet-stream";
            downloadAsAttachment = true;

            if (string.IsNullOrWhiteSpace(storedName))
            {
                return false;
            }

            if (storedName.StartsWith("/uploads/pharmacy/", StringComparison.OrdinalIgnoreCase))
            {
                storedName = Path.GetFileName(storedName);
            }

            if (Path.GetFileName(storedName) != storedName)
            {
                return false;
            }

            var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "SecureUploads", "pharmacy"));
            var candidate = Path.GetFullPath(Path.Combine(root, storedName));
            if (!IsWithinRoot(root, candidate) || !File.Exists(candidate))
            {
                return false;
            }

            var extension = Path.GetExtension(candidate).ToLowerInvariant();
            contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            downloadAsAttachment = extension == ".pdf";
            fullPath = candidate;
            return true;
        }

        public bool DeleteFile(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl))
            {
                return false;
            }

            try
            {
                string root;
                string candidate;

                if (relativeUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
                {
                    root = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "products"));
                    candidate = Path.GetFullPath(Path.Combine(
                        _environment.WebRootPath,
                        relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                }
                else if (Path.GetFileName(relativeUrl) == relativeUrl)
                {
                    root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "SecureUploads", "pharmacy"));
                    candidate = Path.GetFullPath(Path.Combine(root, relativeUrl));
                }
                else
                {
                    return false;
                }

                if (!IsWithinRoot(root, candidate) || !File.Exists(candidate))
                {
                    return false;
                }

                File.Delete(candidate);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Uploaded file {RelativeUrl} could not be deleted.", relativeUrl);
                return false;
            }
        }

        private async Task<(bool IsValid, string? FilePath, string? ErrorMessage)> ValidateAndSaveFileAsync(
            IFormFile? file,
            string subFolder,
            IReadOnlyDictionary<string, byte[]> allowedTypes,
            bool isSecure,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return (false, null, "الملف غير موجود أو فارغ.");
            }

            if (file.Length > MaxFileSize)
            {
                return (false, null, "حجم الملف يتجاوز 5 ميجابايت.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedTypes.TryGetValue(extension, out var signature))
            {
                return (false, null, "نوع الملف غير مدعوم.");
            }

            await using (var source = file.OpenReadStream())
            {
                var header = new byte[12];
                var bytesRead = await source.ReadAsync(header, cancellationToken);
                if (bytesRead < signature.Length ||
                    !signature.SequenceEqual(header.Take(signature.Length)))
                {
                    return (false, null, "محتوى الملف لا يطابق امتداده.");
                }

                if (extension == ".webp" &&
                    (bytesRead < 12 || !header.AsSpan(8, 4).SequenceEqual("WEBP"u8)))
                {
                    return (false, null, "ملف WebP غير صالح.");
                }
            }

            var root = isSecure
                ? Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "SecureUploads"))
                : Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));
            var targetFolder = Path.GetFullPath(Path.Combine(root, subFolder));
            if (!IsWithinRoot(root, targetFolder))
            {
                return (false, null, "مسار حفظ الملف غير آمن.");
            }

            Directory.CreateDirectory(targetFolder);
            var safeFileName = $"{Guid.NewGuid():N}{extension}";
            var finalPath = Path.Combine(targetFolder, safeFileName);
            var quarantinePath = finalPath + ".uploading";

            try
            {
                await using (var destination = new FileStream(
                                 quarantinePath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await file.CopyToAsync(destination, cancellationToken);
                }

                if (extension == ".pdf" && !await HasPdfTrailerAsync(quarantinePath, cancellationToken))
                {
                    return (false, null, "ملف PDF غير مكتمل أو غير صالح.");
                }

                var scan = await _scanner.ScanAsync(quarantinePath, cancellationToken);
                if (!scan.IsClean)
                {
                    return (false, null, scan.ErrorMessage);
                }

                File.Move(quarantinePath, finalPath);
                return (
                    true,
                    isSecure ? safeFileName : $"/uploads/{subFolder}/{safeFileName}",
                    null);
            }
            finally
            {
                if (File.Exists(quarantinePath))
                {
                    try
                    {
                        File.Delete(quarantinePath);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Temporary upload file {QuarantinePath} could not be removed.",
                            quarantinePath);
                    }
                }
            }
        }

        private static bool IsWithinRoot(string root, string candidate)
        {
            var relative = Path.GetRelativePath(root, candidate);
            return relative != ".." &&
                   !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                   !Path.IsPathRooted(relative);
        }

        private static async Task<bool> HasPdfTrailerAsync(string path, CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var tailLength = (int)Math.Min(2048, stream.Length);
            stream.Seek(-tailLength, SeekOrigin.End);
            var tail = new byte[tailLength];
            await stream.ReadExactlyAsync(tail, cancellationToken);
            return System.Text.Encoding.ASCII.GetString(tail).Contains("%%EOF", StringComparison.Ordinal);
        }
    }
}
