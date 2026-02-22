
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EcommerceApp.ViewModels;
using EcommerceApp.Models;

namespace EcommerceApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        var previousUserId = HttpContext.Session.GetString("SessionUserId");
                        if (!string.IsNullOrEmpty(previousUserId) && previousUserId != user.Id)
                        {
                            HttpContext.Session.Remove("Cart");
                            HttpContext.Session.Remove("Favorites");
                        }
                        HttpContext.Session.SetString("SessionUserId", user.Id);

                        var claims = new List<System.Security.Claims.Claim>
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.FullName ?? "")
                        };

                        await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);

                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains("Admin") || roles.Contains("SuperAdmin"))
                        {
                            return RedirectToAction("Index", "Home", new { area = "Admin" });
                        }

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.FullName, PhoneNumber = model.PhoneNumber };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");

                    var claims = new List<System.Security.Claims.Claim>
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.FullName ?? "")
                    };

                    await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);
                    
                    HttpContext.Session.SetString("SessionUserId", user.Id);
                    
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Google external login
       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"خطأ من مزوّد الخدمة: {remoteError}");
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                return await RedirectAfterLogin(existingUser!, returnUrl);
            }

           
            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? info.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "لم يتم الحصول على بيانات كافية من Google. يرجى المحاولة مرة أخرى.");
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;
                user = new ApplicationUser
                {
                    UserName = email,
                    Email    = email,
                    FullName = name
                };
                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء الحساب.");
                    return RedirectToAction(nameof(Login));
                }
                await _userManager.AddToRoleAsync(user, "User");
            }

            await _userManager.AddLoginAsync(user, info);

            return await RedirectAfterLogin(user, returnUrl);
        }

        private async Task<IActionResult> RedirectAfterLogin(ApplicationUser user, string? returnUrl)
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.FullName ?? "")
            };
            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);
            HttpContext.Session.SetString("SessionUserId", user.Id);

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin") || roles.Contains("SuperAdmin"))
                return RedirectToAction("Index", "Home", new { area = "Admin" });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
