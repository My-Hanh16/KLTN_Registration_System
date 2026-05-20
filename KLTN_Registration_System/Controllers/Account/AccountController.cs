// ============================================================
// FILE: Controllers/Account/AccountController.cs
// THAY THẾ HOÀN TOÀN file cũ
// Bổ sung: ChangePassword (GET + POST) đầy đủ logic
// Giữ nguyên: Login, Logout, AccessDenied
// ============================================================
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KLTN_Registration_System.Controllers.Account
{
    public class AccountController : Controller
    {
        private const string GenericLoginError = "Thông tin đăng nhập không chính xác.";

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
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
    }
}
