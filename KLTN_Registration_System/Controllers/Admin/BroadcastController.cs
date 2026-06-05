using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Hubs;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class BroadcastController : Controller
    {
        private const string AdminSelectedPeriodSessionKey = "AdminSelectedPeriodName";
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<NotificationHub> _hubContext;

        public BroadcastController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetSelectedPeriodAsync(semester, year);
            var studentIds = await GetEligibleStudentIdsAsync(selectedPeriod);
            var lecturerIds = (await _userManager.GetUsersInRoleAsync("Lecturer"))
                .Select(u => u.Id)
                .ToList();
            var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(u => u.Id)
                .ToList();

            ViewBag.TotalStudents = studentIds.Count;
            ViewBag.TotalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;
            ViewBag.TotalUsers = studentIds.Concat(lecturerIds).Concat(adminIds).Distinct().Count();
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetSelectedPeriodName();

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(
            string title,
            string content,
            string target,    
            string type,   
            string? redirectUrl)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Tiêu đề và nội dung không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            target = target?.Trim().ToLowerInvariant() ?? "all";
            type = type?.Trim() ?? "System";

            var allowedTargets = new[] { "all", "student", "lecturer" };
            if (!allowedTargets.Contains(target))
            {
                TempData["Error"] = "Nhóm nhận thông báo không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var allowedTypes = new[] { "System", "SystemAlert", "Deadline" };
            if (!allowedTypes.Contains(type))
            {
                TempData["Error"] = "Loại thông báo không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            title = title.Trim();
            content = content.Trim();
            var safeRedirectUrl = NotificationService.NormalizeRedirectUrl(redirectUrl);

            List<string> userIds;
            if (target == "student")
            {
                var selectedPeriod = await GetSelectedPeriodAsync();
                userIds = await GetEligibleStudentIdsAsync(selectedPeriod);
            }
            else if (target == "lecturer")
            {
                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
                userIds = lecturers.Select(u => u.Id).ToList();
            }
            else 
            {
                var selectedPeriod = await GetSelectedPeriodAsync();
                var studentIds = await GetEligibleStudentIdsAsync(selectedPeriod);
                var lecturerIds = (await _userManager.GetUsersInRoleAsync("Lecturer"))
                    .Select(u => u.Id);
                var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                    .Select(u => u.Id);

                userIds = studentIds
                    .Concat(lecturerIds)
                    .Concat(adminIds)
                    .Distinct()
                    .ToList();
            }

            if (!userIds.Any())
            {
                TempData["Error"] = "Không tìm thấy người dùng nào phù hợp.";
                return RedirectToAction(nameof(Index));
            }

            var notifications = userIds.Select(uid => new Notification
            {
                UserId = uid,
                Title = title,
                Content = content,
                Type = type,
                RedirectUrl = safeRedirectUrl,
                IsRead = false,
                Priority = type == "SystemAlert" ? 1 : 0,
                CreatedAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            foreach (var notification in notifications)
            {
                var unreadCount = await _context.Notifications
                    .CountAsync(n => n.UserId == notification.UserId && !n.IsRead);

                await _hubContext.Clients
                    .Group(notification.UserId)
                    .SendAsync("ReceiveNotification", new
                    {
                        id = notification.Id,
                        title = notification.Title,
                        content = notification.Content,
                        type = notification.Type,
                        priority = notification.Priority,
                        redirectUrl = notification.RedirectUrl,
                        createdAt = notification.CreatedAt.ToString("HH:mm dd/MM/yyyy"),
                        unreadCount
                    });
            }

            TempData["Success"] = $"Đã gửi thông báo đến {userIds.Count} người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task<RegistrationPeriod?> GetSelectedPeriodAsync(string? semester = null, string? year = null)
        {
            if (!string.IsNullOrWhiteSpace(semester) && !string.IsNullOrWhiteSpace(year))
            {
                var periodName = $"{semester.Trim()}-{year.Trim()}";
                HttpContext.Session.SetString(AdminSelectedPeriodSessionKey, periodName);
                SetPeriodViewBag(periodName);
            }

            var selectedPeriodName = GetSelectedPeriodName();
            if (string.IsNullOrWhiteSpace(selectedPeriodName))
            {
                return await _context.RegistrationPeriods
                    .FirstOrDefaultAsync(p => p.IsActive);
            }

            return await _context.RegistrationPeriods
                .FirstOrDefaultAsync(p => p.Name == selectedPeriodName);
        }

        private string? GetSelectedPeriodName()
        {
            var selectedPeriodName = HttpContext.Session.GetString(AdminSelectedPeriodSessionKey);
            if (!string.IsNullOrWhiteSpace(selectedPeriodName))
            {
                SetPeriodViewBag(selectedPeriodName);
            }

            return selectedPeriodName;
        }

        private void SetPeriodViewBag(string periodName)
        {
            ViewBag.AdminSelectedPeriodName = periodName;
            var parts = periodName.Split('-', 2);
            if (parts.Length == 2)
            {
                ViewBag.AdminSelectedSemester = parts[0];
                ViewBag.AdminSelectedYear = parts[1];
            }
        }

        private async Task<List<string>> GetEligibleStudentIdsAsync(RegistrationPeriod? selectedPeriod)
        {
            if (selectedPeriod == null)
            {
                return new List<string>();
            }

            return await _context.PeriodStudents
                .Where(ps =>
                    ps.RegistrationPeriodId == selectedPeriod.Id &&
                    ps.IsEligible &&
                    !ps.Student.HasCompletedThesis)
                .Select(ps => ps.StudentId)
                .Distinct()
                .ToListAsync();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOld(int daysOld = 30)
        {
            daysOld = Math.Clamp(daysOld, 1, 3650);
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
