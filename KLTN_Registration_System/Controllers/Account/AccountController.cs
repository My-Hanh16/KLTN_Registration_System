using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace KLTN_Registration_System.Controllers.Account
{
    public class AccountController : Controller
    {
        private const string GenericLoginError = "Thông tin đăng nhập không chính xác.";

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IMemoryCache memoryCache)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailService = emailService;
            _memoryCache = memoryCache;
        }

        // ─────────────────────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────────────────────
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToRoleHome();
            }

            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string username, string password,
            bool rememberMe = false, string? returnUrl = null)
        {
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            username = username?.Trim() ?? string.Empty;

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["username"] = username;
            ViewData["RememberMe"] = rememberMe;

            if (string.IsNullOrWhiteSpace(username))
                ModelState.AddModelError("username", "Vui lòng nhập tài khoản");
            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError("password", "Vui lòng nhập mật khẩu");
            if (!ModelState.IsValid) return View();

            var user = username.Contains('@')
                ? await _userManager.FindByEmailAsync(username)
                : await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                ModelState.AddModelError("", GenericLoginError);
                return View();
            }

            HttpContext.Session.Clear();

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, password, rememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Any())
                {
                    await _signInManager.SignOutAsync();
                    HttpContext.Session.Clear();
                    ModelState.AddModelError("", "Tài khoản chưa được phân quyền. Vui lòng liên hệ quản trị viên.");
                    return View();
                }

                TempData["Success"] = "Đăng nhập thành công!";

                HttpContext.Session.SetString("Username", user.UserName ?? "");
                HttpContext.Session.SetString("Role", roles.FirstOrDefault() ?? "Student");
                HttpContext.Session.SetString("UserId", user.Id);

                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToRoleHome(roles);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Tài khoản bị khóa tạm thời! Thử lại sau 5 phút.");
                return View();
            }

            ModelState.AddModelError("", GenericLoginError);
            return View();
        }

        // ─────────────────────────────────────────────────────────────
        // LOGOUT
        // ─────────────────────────────────────────────────────────────
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            TempData["Success"] = "Đã đăng xuất thành công.";
            return RedirectToAction("Login");
        }

        // ─────────────────────────────────────────────────────────────
        // ACCESS DENIED
        // ─────────────────────────────────────────────────────────────
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ─────────────────────────────────────────────────────────────
        // QUÊN MẬT KHẨU
        // GET  /Account/ForgotPassword
        // POST /Account/ForgotPassword
        // ─────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string identifier)
        {
            identifier = identifier?.Trim() ?? string.Empty;
            ViewData["Identifier"] = identifier;

            if (string.IsNullOrWhiteSpace(identifier))
            {
                ModelState.AddModelError("identifier", "Vui lòng nhập email, tên đăng nhập hoặc mã số.");
                return View();
            }

            var user = identifier.Contains('@')
                ? await _userManager.FindByEmailAsync(identifier)
                : await _userManager.FindByNameAsync(identifier);

            user ??= await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserCode != null && u.UserCode == identifier);

            if (user?.Email == null)
            {
                TempData["Success"] = "Nếu tài khoản tồn tại, hệ thống sẽ gửi mã OTP đặt lại mật khẩu về email đã đăng ký.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var otp = GenerateOtp();
            var cacheKey = GetResetOtpCacheKey(user.Id);
            _memoryCache.Set(
                cacheKey,
                new PasswordResetOtpEntry(otp, DateTimeOffset.UtcNow.AddMinutes(10), 0),
                TimeSpan.FromMinutes(10));

            var safeName = System.Net.WebUtility.HtmlEncode(user.FullName ?? user.UserName ?? "người dùng");
            var body = $@"
                <div style='font-family:Arial,sans-serif;line-height:1.6;color:#111827'>
                    <h2 style='margin:0 0 12px;color:#2563eb'>Mã OTP đặt lại mật khẩu KLTN Portal</h2>
                    <p>Xin chào <strong>{safeName}</strong>,</p>
                    <p>Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
                    <p style='font-size:28px;font-weight:800;letter-spacing:6px;color:#2563eb;margin:18px 0'>{otp}</p>
                    <p>Mã OTP có hiệu lực trong <strong>10 phút</strong>. Nếu bạn không yêu cầu thao tác này, vui lòng bỏ qua email.</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Mã OTP đặt lại mật khẩu KLTN Portal", body);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Không gửi được mã OTP: " + ex.Message);
                return View();
            }

            TempData["Success"] = "Mã OTP đã được gửi về email đã đăng ký. Vui lòng kiểm tra hộp thư.";
            return RedirectToAction(nameof(ResetPasswordOtp), new { userId = user.Id });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ResetPasswordOtp(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Phiên đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            ViewData["UserId"] = user.Id;
            ViewData["MaskedEmail"] = MaskEmail(user.Email);
            return View();
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordOtp(
            string userId,
            string otp,
            string newPassword,
            string confirmPassword)
        {
            ViewData["UserId"] = userId;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", "Không tìm thấy tài khoản.");
                return View();
            }

            ViewData["MaskedEmail"] = MaskEmail(user.Email);

            if (string.IsNullOrWhiteSpace(otp))
            {
                ModelState.AddModelError("otp", "Vui lòng nhập mã OTP.");
                return View();
            }

            otp = new string(otp.Where(char.IsDigit).ToArray());
            if (otp.Length != 6)
            {
                ModelState.AddModelError("otp", "Mã OTP phải gồm 6 chữ số.");
                return View();
            }

            if (!IsStrongPassword(newPassword))
            {
                ModelState.AddModelError("newPassword", "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Xác nhận mật khẩu không khớp.");
                return View();
            }

            var cacheKey = GetResetOtpCacheKey(user.Id);
            if (!_memoryCache.TryGetValue(cacheKey, out PasswordResetOtpEntry? entry) || entry == null)
            {
                ModelState.AddModelError("otp", "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
                return View();
            }

            if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _memoryCache.Remove(cacheKey);
                ModelState.AddModelError("otp", "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
                return View();
            }

            if (entry.Code != otp)
            {
                var attempts = entry.Attempts + 1;
                if (attempts >= 5)
                {
                    _memoryCache.Remove(cacheKey);
                    ModelState.AddModelError("otp", "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã OTP mới.");
                    return View();
                }

                _memoryCache.Set(cacheKey, entry with { Attempts = attempts }, entry.ExpiresAt);
                ModelState.AddModelError("otp", $"Mã OTP không đúng. Còn {5 - attempts} lần thử.");
                return View();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Đặt lại mật khẩu thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return View();
            }

            _memoryCache.Remove(cacheKey);
            await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            TempData["Success"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string? userId, string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            ViewData["UserId"] = userId;
            ViewData["Token"] = token;
            return View();
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string userId,
            string token,
            string newPassword,
            string confirmPassword)
        {
            ViewData["UserId"] = userId;
            ViewData["Token"] = token;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                ModelState.AddModelError("", "Liên kết đặt lại mật khẩu không hợp lệ.");
                return View();
            }

            if (!IsStrongPassword(newPassword))
            {
                ModelState.AddModelError("newPassword", "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Xác nhận mật khẩu không khớp.");
                return View();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", "Không tìm thấy tài khoản.");
                return View();
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Đặt lại mật khẩu thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return View();
            }

            await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            TempData["Success"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        // ─────────────────────────────────────────────────────────────
        // ĐỔI MẬT KHẨU
        // GET  /Account/ChangePassword
        // POST /Account/ChangePassword
        // ─────────────────────────────────────────────────────────────
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            string currentPassword,
            string newPassword,
            string confirmPassword)
        {
            // ── Validate input ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                TempData["Error"] = "Vui lòng nhập mật khẩu hiện tại.";
                return View();
            }

            if (!IsStrongPassword(newPassword))
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Xác nhận mật khẩu không khớp.";
                return View();
            }

            if (newPassword == currentPassword)
            {
                TempData["Error"] = "Mật khẩu mới không được trùng mật khẩu cũ.";
                return View();
            }

            // ── Thực hiện đổi ─────────────────────────────────────
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                // Làm mới security stamp → kick session cũ trên thiết bị khác
                await _userManager.UpdateSecurityStampAsync(user);
                // Giữ session hiện tại vẫn đăng nhập
                await _signInManager.RefreshSignInAsync(user);

                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("ChangePassword");
            }

            // Lỗi từ Identity (sai mật khẩu hiện tại, không đủ độ phức tạp...)
            var errors = result.Errors.Select(e => e.Description).ToList();
            TempData["Error"] = errors.FirstOrDefault()
                ?? "Đổi mật khẩu thất bại. Kiểm tra lại mật khẩu hiện tại.";
            return View();
        }

        private IActionResult RedirectToRoleHome(IList<string>? roles = null)
        {
            if (roles == null)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Statistics", "Admin");
                if (User.IsInRole("Lecturer")) return RedirectToAction("Index", "Lecturer");
                if (User.IsInRole("Student")) return RedirectToAction("Home", "Student");

                return RedirectToAction("Index", "Home");
            }

            if (roles.Contains("Admin")) return RedirectToAction("Statistics", "Admin");
            if (roles.Contains("Lecturer")) return RedirectToAction("Index", "Lecturer");
            if (roles.Contains("Student")) return RedirectToAction("Home", "Student");

            return RedirectToAction("Index", "Home");
        }

        private static bool IsStrongPassword(string? password)
        {
            return !string.IsNullOrWhiteSpace(password)
                && password.Length >= 8
                && password.Any(char.IsUpper)
                && password.Any(char.IsLower)
                && password.Any(char.IsDigit)
                && password.Any(ch => !char.IsLetterOrDigit(ch));
        }

        private static string GenerateOtp()
            => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        private static string GetResetOtpCacheKey(string userId)
            => $"password-reset-otp:{userId}";

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "email đã đăng ký";

            var parts = email.Split('@', 2);
            var name = parts[0];
            var domain = parts[1];
            var visible = name.Length <= 2 ? name[..1] : name[..2];
            return $"{visible}***@{domain}";
        }

        private sealed record PasswordResetOtpEntry(string Code, DateTimeOffset ExpiresAt, int Attempts);
    }
}
