// ============================================================
// FILE: Controllers/Admin/BroadcastController.cs  — TẠO MỚI
// CHỨC NĂNG: Gửi thông báo hàng loạt đến tất cả / nhóm người dùng
// ROUTE: /Admin/Broadcast/*
// ============================================================
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class BroadcastController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BroadcastController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET /Admin/Broadcast
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            ViewBag.TotalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            ViewBag.SentToday = await _context.Notifications
    .Where(x => x.CreatedAt >= today && x.CreatedAt < tomorrow)
    .GroupBy(x => new
    {
        x.Title,
        x.Content,
        x.Type
    })
    .CountAsync();
            // Lịch sử 10 thông báo gần nhất
            var history = await _context.Notifications
    .Where(n => n.Type == "System"
             || n.Type == "SystemAlert"
             || n.Type == "Deadline")
    .GroupBy(n => new
    {
        n.Title,
        n.Content,
        n.Type
    })
    .Select(g => new
    {
        g.Key.Title,
        g.Key.Content,
        g.Key.Type,
        CreatedAt = g.Max(x => x.CreatedAt)
    })
    .OrderByDescending(x => x.CreatedAt)
    .Take(10)
    .ToListAsync();

            ViewBag.RecentBroadcasts = history;

            return View("~/Views/Admin/Broadcast/Index.cshtml");
        }

        // POST /Admin/Broadcast/Send
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(
            string title,
            string content,
            string target,      // "all" | "student" | "lecturer"
            string type,        // "System" | "SystemAlert" | "Deadline"
            string? redirectUrl)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Tiêu đề và nội dung không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            // Lấy danh sách userId theo target
            List<string> userIds;
            if (target == "student")
            {
                var students = await _userManager.GetUsersInRoleAsync("Student");
                userIds = students.Select(u => u.Id).ToList();
            }
            else if (target == "lecturer")
            {
                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
                userIds = lecturers.Select(u => u.Id).ToList();
            }
            else // "all"
            {
                userIds = await _userManager.Users.Select(u => u.Id).ToListAsync();
            }

            if (!userIds.Any())
            {
                TempData["Error"] = "Không tìm thấy người dùng nào phù hợp.";
                return RedirectToAction(nameof(Index));
            }

            // Tạo thông báo hàng loạt
            var notifications = userIds.Select(uid => new Notification
            {
                UserId = uid,
                Title = title.Trim(),
                Content = content.Trim(),
                Type = type,
                RedirectUrl = redirectUrl?.Trim(),
                IsRead = false,
                Priority = type == "SystemAlert" ? 1 : 0,
                CreatedAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã gửi thông báo đến {userIds.Count} người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Broadcast/DeleteAll  — Xóa thông báo hệ thống cũ
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOld(int daysOld = 30)
        {
            var cutoff = DateTime.Now.AddDays(-daysOld);
            var old = await _context.Notifications
                .Where(n => n.CreatedAt < cutoff && n.IsRead)
                .ToListAsync();

            _context.Notifications.RemoveRange(old);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa {old.Count} thông báo cũ hơn {daysOld} ngày.";
            return RedirectToAction(nameof(Index));
        }
    }
}
