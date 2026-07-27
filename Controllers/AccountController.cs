using System.Net;
using System.Security.Claims;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.Services;
using EcommerceApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcommerceApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFavoritesService _favoritesService;
        private readonly IAccountEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IFavoritesService favoritesService,
            IAccountEmailService emailService,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _favoritesService = favoritesService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            await SetExternalProviderStateAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                await SetExternalProviderStateAsync();
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    await MergeSessionStateAsync(user);
                    return await RedirectAfterLoginAsync(user, returnUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction(nameof(TwoFactor), new { returnUrl, rememberMe = model.RememberMe });
                }

            }

            ModelState.AddModelError(
                string.Empty,
                "تعذر تسجيل الدخول. تحقق من البيانات وتأكيد البريد، أو حاول مرة أخرى لاحقًا.");
            await SetExternalProviderStateAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            await SetExternalProviderStateAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (_userManager.Options.SignIn.RequireConfirmedEmail && !_emailService.IsConfigured)
            {
                ModelState.AddModelError(string.Empty, "خدمة البريد الإلكتروني غير مهيأة حاليًا.");
            }

            if (!ModelState.IsValid)
            {
                await SetExternalProviderStateAsync();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim()
            };

            var creation = await _userManager.CreateAsync(user, model.Password);
            if (!creation.Succeeded)
            {
                AddIdentityErrors(creation);
                await SetExternalProviderStateAsync();
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.User);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                await SetExternalProviderStateAsync();
                return View(model);
            }

            await _favoritesService.MergeSessionFavoritesAsync(user.Id, HttpContext.Session);

            if (_userManager.Options.SignIn.RequireConfirmedEmail)
            {
                ViewData["EmailSent"] = await SendConfirmationEmailAsync(user);
                return View("RegistrationPending", user.Email);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            HttpContext.Session.SetString("SessionUserId", user.Id);
            return await RedirectAfterLoginAsync(user, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
        {
            var providers = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (!providers.Any(item => string.Equals(item.Name, provider, StringComparison.Ordinal)))
            {
                return BadRequest();
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                TempData["Error"] = "تعذر تسجيل الدخول بواسطة مزود الخدمة.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null || info.LoginProvider != "Google")
            {
                TempData["Error"] = "تعذر التحقق من تسجيل الدخول الخارجي.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: false);

            if (signInResult.Succeeded)
            {
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser == null)
                {
                    return RedirectToAction(nameof(Login));
                }

                await MergeSessionStateAsync(existingUser);
                return await RedirectAfterLoginAsync(existingUser, returnUrl);
            }

            if (signInResult.RequiresTwoFactor)
            {
                return RedirectToAction(nameof(TwoFactor), new { returnUrl });
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var emailVerified = string.Equals(
                info.Principal.FindFirstValue("email_verified"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(email) || !emailVerified)
            {
                TempData["Error"] = "لم يقدم مزود الخدمة بريدًا إلكترونيًا مؤكدًا.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                TempData["Error"] = "يوجد حساب بهذا البريد. استخدم تسجيل الدخول أو استعادة كلمة المرور بدلًا من إنشاء ارتباط تلقائي.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = "تعذر إنشاء الحساب الخارجي.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.User);
            var loginResult = roleResult.Succeeded
                ? await _userManager.AddLoginAsync(user, info)
                : roleResult;

            if (!roleResult.Succeeded || !loginResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                TempData["Error"] = "تعذر إكمال إنشاء الحساب الخارجي.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            await MergeSessionStateAsync(user);
            return await RedirectAfterLoginAsync(user, returnUrl);
        }

        [HttpGet]
        public Task<IActionResult> ConfirmEmail(string? userId, string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult<IActionResult>(BadRequest());
            }

            return ConfirmEmailCore(userId, token);
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpGet]
        public IActionResult ResendConfirmation() =>
            View(new ResendConfirmationViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
        {
            if (ModelState.IsValid && _emailService.IsConfigured)
            {
                var user = await _userManager.FindByEmailAsync(model.Email.Trim());
                if (user is { EmailConfirmed: false })
                {
                    await SendConfirmationEmailAsync(user);
                }
            }

            return View("ConfirmationRequested");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid && _emailService.IsConfigured)
            {
                var user = await _userManager.FindByEmailAsync(model.Email.Trim());
                if (user?.EmailConfirmed == true)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callback = Url.Action(
                        nameof(ResetPassword),
                        "Account",
                        new { email = user.Email, token },
                        Request.Scheme);

                    await TrySendEmailAsync(
                        user.Email!,
                        "إعادة تعيين كلمة المرور",
                        $"<p>استخدم الرابط التالي لإعادة تعيين كلمة المرور:</p><p><a href=\"{WebUtility.HtmlEncode(callback)}\">إعادة تعيين كلمة المرور</a></p>",
                        HttpContext.RequestAborted);
                }
            }

            return View("RecoveryRequested");
        }

        [HttpGet]
        public IActionResult ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return View("PasswordResetComplete");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(model);
            }

            return View("PasswordResetComplete");
        }

        [HttpGet]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> TwoFactor(string? returnUrl = null, bool rememberMe = false)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!_emailService.IsConfigured || string.IsNullOrWhiteSpace(user.Email))
            {
                TempData["Error"] = "تعذر إرسال رمز التحقق.";
                return RedirectToAction(nameof(Login));
            }

            var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
            if (!await TrySendEmailAsync(
                    user.Email,
                    "رمز التحقق",
                    $"<p>رمز التحقق الخاص بك: <strong>{WebUtility.HtmlEncode(code)}</strong></p>",
                    HttpContext.RequestAborted))
            {
                TempData["Error"] = "تعذر إرسال رمز التحقق. حاول مرة أخرى لاحقًا.";
                return RedirectToAction(nameof(Login));
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new TwoFactorViewModel { RememberMe = rememberMe });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> TwoFactor(
            TwoFactorViewModel model,
            string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider,
                model.Code.Replace(" ", string.Empty),
                model.RememberMe,
                rememberClient: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح.");
                return View(model);
            }

            await MergeSessionStateAsync(user);
            return await RedirectAfterLoginAsync(user, returnUrl);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Security()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            ViewData["TwoFactorEnabled"] = await _userManager.GetTwoFactorEnabledAsync(user);
            ViewData["CanEnableTwoFactor"] = user.EmailConfirmed && _emailService.IsConfigured;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTwoFactor(bool enabled)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (enabled && (!user.EmailConfirmed || !_emailService.IsConfigured))
            {
                TempData["Error"] = "يجب تأكيد البريد وتهيئة خدمة البريد أولاً.";
                return RedirectToAction(nameof(Security));
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, enabled);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "تم تحديث إعداد المصادقة الثنائية."
                : string.Join(" ", result.Errors.Select(error => error.Description));

            return RedirectToAction(nameof(Security));
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        private async Task<IActionResult> ConfirmEmailCore(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            ViewData["Confirmed"] = result.Succeeded;
            return View("EmailConfirmed");
        }

        private async Task<bool> SendConfirmationEmailAsync(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callback = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, token },
                Request.Scheme);

            return await TrySendEmailAsync(
                user.Email!,
                "تأكيد البريد الإلكتروني",
                $"<p>يرجى تأكيد بريدك الإلكتروني:</p><p><a href=\"{WebUtility.HtmlEncode(callback)}\">تأكيد البريد الإلكتروني</a></p>",
                HttpContext.RequestAborted);
        }

        private async Task<bool> TrySendEmailAsync(
            string recipient,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken)
        {
            try
            {
                await _emailService.SendAsync(recipient, subject, htmlBody, cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Account email delivery failed for message type {Subject}.",
                    subject);
                return false;
            }
        }

        private async Task SetExternalProviderStateAsync()
        {
            var providers = await _signInManager.GetExternalAuthenticationSchemesAsync();
            ViewData["GoogleEnabled"] = providers.Any(item => item.Name == "Google");
        }

        private async Task MergeSessionStateAsync(ApplicationUser user)
        {
            var previousUserId = HttpContext.Session.GetString("SessionUserId");
            if (!string.IsNullOrWhiteSpace(previousUserId) && previousUserId != user.Id)
            {
                HttpContext.Session.Remove("Cart");
                HttpContext.Session.Remove("Favorites");
            }

            await _favoritesService.MergeSessionFavoritesAsync(user.Id, HttpContext.Session);
            HttpContext.Session.SetString("SessionUserId", user.Id);
        }

        private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.SuperAdmin)
                ? RedirectToAction("Index", "Home", new { area = "Admin" })
                : RedirectToAction("Index", "Home");
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
