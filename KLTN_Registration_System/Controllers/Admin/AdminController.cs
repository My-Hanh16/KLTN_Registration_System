using ClosedXML.Excel;
using ExcelDataReader;
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.ViewModels;
using KLTN_Registration_System.Models.ViewModels.Admin;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;

namespace KLTN_Registration_System.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public AdminController(AppDbContext context,UserManager<ApplicationUser> userManager,NotificationService notificationService)
            : base(context, userManager)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ============================================================
        // DASHBOARD CHÍNH  →  /Admin/Index
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var totalTopics = await _context.Topics.CountAsync();
            var pendingApprovals = await _context.Topics
                .CountAsync(t => t.Status == TopicStatus.Pending && !t.IsApproved);
            var totalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;
            var totalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;

            var approvedStudentIds = await _context.Registrations
                .Where(r => r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .CountAsync();

            double registrationRate = totalStudents > 0
                ? Math.Round((double)approvedStudentIds / totalStudents * 100, 1)
                : 0;

            // Biểu đồ phân bổ theo Khoa
            var departmentData = await _context.Topics
                .Include(t => t.Major)
                .Where(t => t.Major != null)
                .GroupBy(t => t.Major!.FacultyName ?? "Chưa phân khoa")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            List<DepartmentStatVM> stats;
            if (departmentData.Any())
            {
                int maxCount = departmentData.Max(d => d.Count);
                stats = departmentData.Select(d => new DepartmentStatVM
                {
                    Name = AbbreviateName(d.Name),
                    Value = maxCount > 0 ? d.Count * 100 / maxCount : 0,
                    Count = d.Count
                }).ToList();
            }
            else
            {
                stats = new List<DepartmentStatVM>();
            }

            // Hoạt động gần đây
            var recentNotifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            var recentActivities = recentNotifications.Select(n => new ActivityLogVM
            {
                Message = n.Content ?? "Hoạt động hệ thống",
                TimeAgo = GetTimeAgo(n.CreatedAt),
                Icon = MapNotificationIcon(n.Type),
                ColorClass = MapNotificationColor(n.Type)
            }).ToList();

            if (!recentActivities.Any())
                recentActivities.Add(new ActivityLogVM
                {
                    Message = "Hệ thống khởi động thành công",
                    TimeAgo = "Vừa xong",
                    Icon = "check_circle",
                    ColorClass = "bg-green-100 text-green-600"
                });

            var last7Days = Enumerable.Range(0, 7)
                .Select(offset => DateTime.Today.AddDays(offset - 6))
                .ToList();

            var recentRegistrationCounts = await _context.Registrations
                .Where(r => r.CreatedAt.Date >= last7Days.First())
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.RegistrationLineLabels = last7Days
                .Select(d => d.ToString("dd/MM"))
                .ToList();
            ViewBag.RegistrationLineCounts = last7Days
                .Select(d => recentRegistrationCounts.FirstOrDefault(x => x.Date == d)?.Count ?? 0)
                .ToList();

            // Đề tài mới cần duyệt (top 5)
            var newTopics = await _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Where(t => t.Status == TopicStatus.Pending && !t.IsApproved)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new TopicItemVM
                {
                    Id = t.Id.ToString(),
                    Name = t.Title,
                    LecturerName = t.Lecturer != null ? (t.Lecturer.FullName ?? t.Lecturer.UserName ?? "—") : "Chưa phân công",
                    Faculty = t.Major != null ? t.Major.Name : "N/A"
                })
                .ToListAsync();

            var model = new AdminDashboard
            {
                TotalTopics = totalTopics,
                PendingApprovals = pendingApprovals,
                TotalLecturers = totalLecturers,
                RegistrationRate = registrationRate,
                DepartmentStats = stats,
                RecentActivities = recentActivities,
                NewTopics = newTopics
            };

            return View(model);
        }

        // ============================================================
        // THỐNG KÊ HỆ THỐNG  →  /Admin/Statistics
        // ============================================================
        public async Task<IActionResult> Statistics(string? semester = null, string? year = null)
        {
            var totalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            var totalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;

            semester = string.IsNullOrWhiteSpace(semester) ? "HK2" : semester.Trim();
            year = string.IsNullOrWhiteSpace(year) ? GetCurrentAcademicYear() : year.Trim();
            var semesterKey = $"{semester}-{year}";

            var topicQuery = _context.Topics.AsQueryable();
            topicQuery = topicQuery.Where(t => t.Semester == semesterKey || t.Semester == null);

            var registrationQuery = _context.Registrations
                .Include(r => r.Topic)
                .AsQueryable();
            registrationQuery = registrationQuery.Where(r =>
                r.Topic.Semester == semesterKey || r.Topic.Semester == null);

            var totalTopics = await topicQuery.CountAsync();
            var approvedTopics = await topicQuery.CountAsync(t => t.IsApproved);
            var pendingTopics = await topicQuery.CountAsync(t => t.Status == TopicStatus.Pending || !t.IsApproved);

            var registrations = await registrationQuery.ToListAsync();

            var approvedRegs = registrations.Count(r => r.Status == "Approved");
            var pendingRegs = registrations.Count(r => r.Status == "Pending");
            var rejectedRegs = registrations.Count(r => r.Status == "Rejected");
            var registeredStudentIds = registrations
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .Count();
            var approvedStudentIds = registrations
                .Where(r => r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .Count();

            double registrationRate = totalStudents > 0
                ? Math.Round((double)registeredStudentIds / totalStudents * 100, 1)
                : 0;

            // =========================
            // BIỂU ĐỒ THEO KHOA
            // =========================
            var majorStats = await topicQuery
                .Include(t => t.Major)
                .Where(t => t.Major != null)
                .GroupBy(t => t.Major!.FacultyName ?? "Chưa phân khoa")
                .Select(g => new
                {
                    Faculty = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // =========================
            // BIỂU ĐỒ THEO THÁNG
            // =========================
            var monthlyRegs = registrations
                .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();

            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalLecturers = totalLecturers;
            ViewBag.TotalTopics = totalTopics;

            ViewBag.ApprovedCount = approvedRegs;
            ViewBag.PendingCount = pendingRegs;
            ViewBag.RejectedCount = rejectedRegs;
            ViewBag.RegisteredStudents = registeredStudentIds;
            ViewBag.ApprovedStudents = approvedStudentIds;
            ViewBag.UnregisteredStudents = Math.Max(0, totalStudents - registeredStudentIds);

            ViewBag.ApprovedTopics = approvedTopics;
            ViewBag.PendingTopics = pendingTopics;
            ViewBag.TotalRegistrations = registrations.Count;

            ViewBag.RegistrationRate = registrationRate;
            ViewBag.SelectedSemester = semester;
            ViewBag.SelectedYear = year;
            ViewBag.YearOptions = await GetAcademicYearOptions();

            // Major chart
            ViewBag.MajorLabels = majorStats.Select(x => x.Faculty).ToList();
            ViewBag.MajorCounts = majorStats.Select(x => x.Count).ToList();

            // Monthly chart
            ViewBag.MonthLabels = monthlyRegs
                .Select(x => $"{x.Month:00}/{x.Year}")
                .ToList();

            ViewBag.MonthCounts = monthlyRegs
                .Select(x => x.Count)
                .ToList();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportStatistics(string? semester = null, string? year = null)
        {
            semester = string.IsNullOrWhiteSpace(semester) ? "HK2" : semester.Trim();
            year = string.IsNullOrWhiteSpace(year) ? GetCurrentAcademicYear() : year.Trim();
            var semesterKey = $"{semester}-{year}";

            var totalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            var totalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;

            var topics = await _context.Topics
                .Include(t => t.Major)
                .Where(t => t.Semester == semesterKey || t.Semester == null)
                .ToListAsync();

            var registrations = await _context.Registrations
                .Include(r => r.Topic)
                .Where(r => r.Topic.Semester == semesterKey || r.Topic.Semester == null)
                .ToListAsync();

            var registeredStudents = registrations
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .Count();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Thong ke");

            ws.Cell(1, 1).Value = "BÁO CÁO THỐNG KÊ HỆ THỐNG";
            ws.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;
            ws.Cell(2, 1).Value = "Học kỳ";
            ws.Cell(2, 2).Value = semesterKey;
            ws.Cell(3, 1).Value = "Ngày xuất";
            ws.Cell(3, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            ws.Cell(5, 1).Value = "Chỉ số";
            ws.Cell(5, 2).Value = "Giá trị";
            ws.Range(5, 1, 5, 2).Style.Font.Bold = true;

            var rows = new (string Label, object Value)[]
            {
                ("Tổng sinh viên", totalStudents),
                ("Tổng giảng viên", totalLecturers),
                ("Tổng đề tài", topics.Count),
                ("Đề tài đã duyệt", topics.Count(t => t.IsApproved)),
                ("Đề tài chờ duyệt", topics.Count(t => t.Status == TopicStatus.Pending || !t.IsApproved)),
                ("Tổng đăng ký", registrations.Count),
                ("Đăng ký đã duyệt", registrations.Count(r => r.Status == "Approved")),
                ("Đăng ký chờ duyệt", registrations.Count(r => r.Status == "Pending")),
                ("Đăng ký bị từ chối", registrations.Count(r => r.Status == "Rejected")),
                ("Sinh viên đã đăng ký", registeredStudents),
                ("Tỷ lệ đăng ký", totalStudents > 0 ? $"{Math.Round((double)registeredStudents / totalStudents * 100, 1)}%" : "0%")
            };

            for (int i = 0; i < rows.Length; i++)
            {
                ws.Cell(i + 6, 1).Value = rows[i].Label;
                ws.Cell(i + 6, 2).Value = rows[i].Value.ToString();
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"Thong-ke-{semesterKey}-{DateTime.Now:yyyyMMddHHmm}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ============================================================
        // DUYỆT ĐĂNG KÝ  →  /Admin/Approval
        // ============================================================
        public async Task<IActionResult> Approval()
        {
            var registrations = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.ApprovedCount = registrations.Count(r => r.Status == "Approved");
            ViewBag.PendingCount = registrations.Count(r => r.Status == "Pending");
            ViewBag.RejectedCount = registrations.Count(r => r.Status == "Rejected");
            ViewBag.TotalRegistrations = registrations.Count;

            return View(registrations);
        }

        // POST: Duyệt 1 đăng ký — ĐÃ SỬA bug NullReference
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int id)
        {
            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();

            reg.Status = "Approved";
            // SV đã có đề tài approved chưa
            var alreadyApproved = await _context.Registrations
                .AnyAsync(r =>
                    r.StudentId == reg.StudentId &&
                    r.Status == "Approved" &&
                    r.Id != reg.Id);

            if (alreadyApproved)
            {
                TempData["Error"] = "Sinh viên đã có đề tài được duyệt!";
                return RedirectToAction(nameof(Approval));
            }
            reg.UpdatedAt = DateTime.Now;
            reg.ApprovedBy = User.Identity?.Name;


            // ✅ FIX: Query riêng thay vì dùng navigation property chưa Include
            reg.Status = "Approved";
            await _context.SaveChangesAsync();

            var approvedCount = await _context.Registrations
                .CountAsync(r =>
                    r.TopicId == reg.TopicId &&
                    r.Status == "Approved");

            if (approvedCount >= reg.Topic.MaxStudents)
            {
                reg.Topic.Status = TopicStatus.Full;
                reg.Topic.IsRegistrationOpen = false;
            }

            await _context.SaveChangesAsync();

            await _notificationService.SendDualNotification(
    reg.StudentId,
    "🔔 Đăng ký được duyệt",
    $"Yêu cầu đăng ký đề tài \"{reg.Topic?.Title}\" của bạn đã được Admin phê duyệt.",
    "TopicApproved",
    reg.TopicId // Truyền ID đề tài vào tham số relatedId (kiểu int)
);

            TempData["Success"] = "Đã phê duyệt đăng ký thành công!";
            return RedirectToAction(nameof(Approval));
        }

        // POST: Từ chối 1 đăng ký
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id)
        {
            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();

            reg.Status = "Rejected";
            reg.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.SendDualNotification(
        reg.StudentId,
        "❌ Đăng ký bị từ chối",
        $"Yêu cầu đăng ký đề tài \"{reg.Topic?.Title}\" của bạn đã bị từ chối.",
        "TopicRejected",
        reg.TopicId);

            TempData["Error"] = "Đã từ chối yêu cầu đăng ký.";
            return RedirectToAction(nameof(Approval)); // Thêm dòng này để fix lỗi CS0161
        } // Thêm dấu đóng ngoặc này để fix lỗi CS1513

        // ============================================================
        // QUẢN LÝ KHO ĐỀ TÀI  →  /Admin/ThesisManagement
        // ============================================================
        public async Task<IActionResult> ThesisManagement(
            string? search = null, string? status = null,
            int? majorId = null, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Include(t => t.Registrations)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.Lecturer != null &&
t.Lecturer.FullName != null &&
t.Lecturer.FullName.Contains(search)));

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<TopicStatus>(status, out var parsedStatus))
                {
                    query = query.Where(t => t.Status == parsedStatus);
                }
            }

            if (majorId.HasValue)
                query = query.Where(t => t.MajorId == majorId);

            var totalItems = await query.CountAsync();
            var topics = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalTopics = await _context.Topics.CountAsync();
            ViewBag.OpenTopics = await _context.Topics.CountAsync(t => t.IsApproved && t.IsRegistrationOpen);
            ViewBag.PendingTopics = await _context.Topics.CountAsync(t => !t.IsApproved);
            ViewBag.Majors = await _context.Majors.Where(m => m.IsActive).ToListAsync();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.StatusFilter = status;
            ViewBag.MajorFilter = majorId;
            ViewBag.Lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");

            return View("_TopicList", topics);
        }

        // Alias sidebar dùng route ManageTopics
        public Task<IActionResult> ManageTopics(
            string? search = null, string? status = null,
            int? majorId = null, int page = 1)
            => ThesisManagement(search, status, majorId, page);

        // Duyệt 1 đề tài
        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound();

            topic.IsApproved = true;
            topic.IsRegistrationOpen = true;
            topic.Status = TopicStatus.Available;

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.LecturerId))
                await _notificationService.SendDualNotification(topic.LecturerId,
                    "Đề tài được phê duyệt",
                    $"Đề tài \"{topic.Title}\" đã được Admin phê duyệt và công khai.",
                    "TopicApproved");

            TempData["Success"] = $"Đã phê duyệt đề tài \"{topic.Title}\"!";
            return RedirectToAction(nameof(ThesisManagement));
        }

        // Từ chối 1 đề tài
        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTopic(int id, string? reason = null)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound();

            topic.IsApproved = false;
            topic.Status = TopicStatus.Rejected;

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.LecturerId))
                await _notificationService.SendDualNotification(topic.LecturerId,
                    "Đề tài bị từ chối",
                    $"Đề tài \"{topic.Title}\" đã bị từ chối." +
                    (string.IsNullOrEmpty(reason) ? "" : $" Lý do: {reason}"),
                    "TopicRejected");

            TempData["Error"] = $"Đã từ chối đề tài \"{topic.Title}\".";
            return RedirectToAction(nameof(ThesisManagement));
        }

        // Xóa đề tài
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            if (topic.Registrations != null && topic.Registrations.Any(r => r.Status == "Approved"))
            {
                TempData["Error"] = "Không thể xóa đề tài đã có sinh viên đăng ký thành công!";
                return RedirectToAction(nameof(ThesisManagement));
            }

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            TempData["Error"] = $"Đã xóa đề tài \"{topic.Title}\".";
            return RedirectToAction(nameof(ThesisManagement));
        }

        // Duyệt TẤT CẢ đề tài đang chờ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAllPending()
        {
            var pendingTopics = await _context.Topics
                .Where(t => !t.IsApproved)
                .ToListAsync();

            foreach (var t in pendingTopics)
            {
                t.IsApproved = true;
                t.IsRegistrationOpen = true;
                t.Status = TopicStatus.Available;

                if (!string.IsNullOrEmpty(t.LecturerId))
                {
                    await _notificationService.SendDualNotification(
                        t.LecturerId,
                        "Đề tài được phê duyệt",
                        $"Đề tài \"{t.Title}\" đã được Admin phê duyệt.",
                        "TopicApproved");
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Đã phê duyệt {pendingTopics.Count} đề tài!";

            return RedirectToAction(nameof(ThesisManagement));
        }
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> ApproveMultiple(
    [FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Chưa chọn đề tài"
                });
            }

            var topics = await _context.Topics
                .Where(t =>
                    ids.Contains(t.Id)
                    && t.Status == TopicStatus.Pending)
                .ToListAsync();

            foreach (var topic in topics)
            {
                topic.IsApproved = true;
                topic.IsRegistrationOpen = true;
                topic.Status = TopicStatus.Available;

                if (!string.IsNullOrEmpty(topic.LecturerId))
                {
                    await _notificationService.SendDualNotification(
                        topic.LecturerId,
                        "Đề tài được phê duyệt",
                        $"Đề tài \"{topic.Title}\" đã được Admin phê duyệt.",
                        "TopicApproved");
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Đã duyệt {topics.Count} đề tài"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectMultiple(
    [FromBody] List<int> ids)
        {
            var topics = await _context.Topics
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            foreach (var topic in topics)
            {
                topic.IsApproved = false;
                topic.Status = TopicStatus.Rejected;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Đã từ chối {topics.Count} đề tài"
            });
        }

        // ============================================================
        // PHÂN CÔNG GIẢNG VIÊN  →  POST /Admin/AssignLecturer
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignLecturer(int topicId, string lecturerId)
        {
            var topic = await _context.Topics.FindAsync(topicId);
            if (topic == null) return NotFound();

            var lecturer = await _userManager.FindByIdAsync(lecturerId);
            if (lecturer == null) return NotFound();

            topic.LecturerId = lecturerId;
            await _context.SaveChangesAsync();

            await _notificationService.SendDualNotification(
        lecturerId,
        "Bạn được phân công hướng dẫn đề tài",
        $"Admin đã phân công bạn hướng dẫn đề tài \"{topic.Title}\".",
        "TopicApproved",
        topicId);

            TempData["Success"] = $"Đã phân công giảng viên cho đề tài.";
            return RedirectToAction(nameof(ThesisManagement)); // Đảm bảo có dòng return này
        } // Thêm dấu đóng ngoặc nhọn kết thúc hàm tại đây

        // ============================================================
        // QUẢN LÝ NGƯỜI DÙNG  →  /Admin/UserManagement
        // ============================================================
        public async Task<IActionResult> UserManagement(
            string? search = null, string? role = null, int page = 1)
        {
            int pageSize = 10;

            IList<ApplicationUser> usersInRole = string.IsNullOrEmpty(role)
                ? await _userManager.Users.ToListAsync()
                : await _userManager.GetUsersInRoleAsync(role);

            var query = usersInRole.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.UserCode != null && u.UserCode.Contains(search, StringComparison.OrdinalIgnoreCase)));

            var totalItems = query.Count();
            var users = query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userRoles = new Dictionary<string, string>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userRoles[u.Id] = roles.FirstOrDefault() ?? "Student";
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.TotalUsers = totalItems;
            ViewBag.Search = search;
            ViewBag.RoleFilter = role;
            ViewBag.Majors = await _context.Majors
                .Where(m => m.IsActive)
                .OrderBy(m => m.FacultyName)
                .ThenBy(m => m.Name)
                .ToListAsync();

            // ✅ FIX: Set đủ 3 ViewBag cho thẻ thống kê cuối trang
            var allUsers = await _userManager.Users.ToListAsync();
            ViewBag.NewUsers = allUsers.Count(u => !u.EmailConfirmed);
            var lecturerList = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.VerifiedLecturers = lecturerList.Count(u => u.EmailConfirmed);
            ViewBag.LockedAccounts = allUsers.Count(u =>
                u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now);

            return View("UserManagement", users);
        }

        // Khóa / Mở khóa tài khoản
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLockUser(string userId, string? role)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng!";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            bool isLocked = await _userManager.IsLockedOutAsync(user);

            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);

                TempData["Success"] =
                    $"Đã mở khóa tài khoản {user.FullName}";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

                TempData["Success"] =
                    $"Đã khóa tài khoản {user.FullName}";
            }

            return RedirectToAction(nameof(UserManagement), new { role });
        }

        // Đổi Role người dùng
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserRole(string userId, string newRole, string? role)
        {
            var allowed = new[] { "Admin", "Lecturer", "Student" };
            if (!allowed.Contains(newRole)) return BadRequest("Role không hợp lệ.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentUserId = _userManager.GetUserId(User);

            if (user.Id == currentUserId && currentRoles.Contains("Admin") && newRole != "Admin")
            {
                TempData["Error"] = "Bạn không thể tự hạ quyền Admin của tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            if (currentRoles.Contains("Admin") && newRole != "Admin")
            {
                var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
                if (adminCount <= 1)
                {
                    TempData["Error"] = "Không thể hạ quyền Admin cuối cùng của hệ thống.";
                    return RedirectToAction(nameof(UserManagement), new { role });
                }
            }

            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);

            TempData["Success"] = $"Đã đổi quyền {user.FullName} thành {newRole}.";
            return RedirectToAction(nameof(UserManagement), new { role = role ?? newRole });
        }

        // Reset mật khẩu
        // ─── GET: Hiển thị trang reset mật khẩu ─────────────────────
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            ViewBag.User = user;
            ViewBag.Roles = await _userManager.GetRolesAsync(user);
            return View();
        }

        // ─── POST: Thực hiện reset ───────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("ResetPassword")]
        public async Task<IActionResult> ResetPasswordPost(
            string userId, string? newPassword, bool useRandomPassword = false)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            string finalPassword;
            if (useRandomPassword || string.IsNullOrWhiteSpace(newPassword))
            {
                finalPassword = GenerateStrongPassword();
            }
            else
            {
                finalPassword = newPassword.Trim();
                // Kiểm tra độ dài tối thiểu
                if (!finalPassword.Any(char.IsUpper) ||
    !finalPassword.Any(char.IsLower) ||
    !finalPassword.Any(char.IsDigit) ||
    finalPassword.Length < 8 ||
    !finalPassword.Any(ch => !char.IsLetterOrDigit(ch)))
                {
                    TempData["Error"] =
                        "Mật khẩu phải có chữ hoa, chữ thường, số, ký tự đặc biệt và tối thiểu 8 ký tự.";

                    return RedirectToAction(nameof(ResetPassword), new { userId });
                }
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, finalPassword);

            if (result.Succeeded)
            {
                // Kick session cũ
                await _userManager.UpdateSecurityStampAsync(user);

                // Ghi log thông báo cho user
                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Title = "Mật khẩu đã được đặt lại",
                    Content = "Quản trị viên đã đặt lại mật khẩu tài khoản của bạn. Vui lòng đăng nhập lại và đổi mật khẩu ngay.",
                    Type = "System",
                    RedirectUrl = "/Account/ChangePassword",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["TempPassword"] = finalPassword;
            }
            else
            {
                TempData["Error"] = "Reset thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(UserManagement));
        }

        // Xóa người dùng
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId, string role)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "Bạn không thể tự xóa tài khoản đang đăng nhập.";
                return RedirectToAction("UserManagement", new { role });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng!";
                return RedirectToAction("UserManagement");
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var adminCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count;
                if (adminCount <= 1)
                {
                    TempData["Error"] = "Không thể xóa Admin cuối cùng của hệ thống.";
                    return RedirectToAction("UserManagement", new { role });
                }
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
                TempData["Success"] = "Đã xóa người dùng thành công!";
            else
                TempData["Error"] = "Lỗi: Không thể xóa người dùng này (có thể do ràng buộc dữ liệu).";

            return RedirectToAction("UserManagement", new { role });
        }

        // Quản lý Sinh viên
        public async Task<IActionResult> StudentManagement(string? search, int page = 1)
            => await UserManagement(search, "Student", page);

        // Quản lý Giảng viên
        public async Task<IActionResult> LecturerManagement(string? search, int page = 1)
            => await UserManagement(search, "Lecturer", page);

        // Tạo user thủ công
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            string fullName, string email, string userCode, string role,
            string? Faculty, string? degree, string? position, List<int>? majorIds)
        {
            var allowedRoles = new[] { "Admin", "Lecturer", "Student" };
            if (!allowedRoles.Contains(role))
            {
                TempData["Error"] = "Role không hợp lệ.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            email = email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                TempData["Error"] = "Email không hợp lệ.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                TempData["Error"] = "Email đã tồn tại trong hệ thống.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            var selectedMajorIds = (majorIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName?.Trim() ?? email,
                UserCode = userCode?.Trim() ?? string.Empty,
                EmailConfirmed = true,
                Faculty = Faculty?.Trim(),
                MajorId = role == "Student" ? selectedMajorIds.Cast<int?>().FirstOrDefault() : null,
                Degree = degree?.Trim(),
                Position = position?.Trim()
            };

            var initialPassword = GenerateStrongPassword();
            var result = await _userManager.CreateAsync(user, initialPassword);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                await SyncUserMajorsAsync(user.Id, selectedMajorIds);
                TempData["Success"] = $"Đã tạo tài khoản {role} thành công!";
                TempData["TempPassword"] = initialPassword;
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(role switch
            {
                "Lecturer" => nameof(LecturerManagement),
                "Student" => nameof(StudentManagement),
                _ => nameof(UserManagement)
            }, new { role = role == "Admin" ? "Admin" : null });
        }

        // ============================================================
        // IMPORT TOPICS TỪ EXCEL
        // ============================================================
        [HttpGet]
        public IActionResult ImportTopics() => View();

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> ImportTopics(IFormFile file)
        {
            if (!IsValidExcelUpload(file, out var topicImportError))
            {
                TempData["Error"] = topicImportError;
                return RedirectToAction(nameof(ThesisManagement));
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            int success = 0, skipped = 0;

            using (var stream = file.OpenReadStream())
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                if (table.Rows.Count < 2)
                {
                    TempData["Error"] = "File không có dữ liệu!";
                    return RedirectToAction(nameof(ThesisManagement));
                }

                var header = table.Rows[0];
                int colCode = -1, colTitle = -1, colDesc = -1,
                    colSemester = -1, colLecturerEmail = -1, colFaculty = -1,
                    colMajorCode = -1, colMajorName = -1, colMaxStudents = -1,
                    colLevel = -1, colCategory = -1, colDeadline = -1;

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var colName = header[j]?.ToString()?.Trim().ToLower();
                    if (string.IsNullOrEmpty(colName)) continue;

                    if (colName.Contains("topiccode") || colName.Contains("mã")) colCode = j;
                    else if (colName.Contains("title") || colName.Contains("tên")) colTitle = j;
                    else if (colName.Contains("description") || colName.Contains("mô")) colDesc = j;
                    else if (colName.Contains("semester") || colName.Contains("kỳ")) colSemester = j;
                    else if (colName.Contains("email")) colLecturerEmail = j;
                    else if (colName.Contains("majorcode") || colName.Contains("mã ngành") || colName.Contains("ma nganh")) colMajorCode = j;
                    else if (colName.Contains("majorname") || colName.Contains("chuyên ngành") || colName.Contains("chuyen nganh")) colMajorName = j;
                    else if (colName.Contains("maxstudents") || colName.Contains("số sv") || colName.Contains("so sv")) colMaxStudents = j;
                    else if (colName.Contains("level") || colName.Contains("độ khó") || colName.Contains("do kho")) colLevel = j;
                    else if (colName.Contains("category") || colName.Contains("loại") || colName.Contains("loai")) colCategory = j;
                    else if (colName.Contains("deadline") || colName.Contains("hạn") || colName.Contains("han")) colDeadline = j;
                    else if (colName.Contains("faculty") || colName.Contains("khoa")) colFaculty = j;
                }

                if (colTitle == -1)
                {
                    TempData["Error"] = "Thiếu cột Title!";
                    return RedirectToAction(nameof(ThesisManagement));
                }

                for (int i = 1; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    var title = row[colTitle]?.ToString()?.Trim();
                    // ✅ FIX: Tự sinh mã nếu không có cột code
                    var code = colCode != -1 ? row[colCode]?.ToString()?.Trim() : "";
                    if (string.IsNullOrEmpty(code))
                        code = $"AUTO-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                    var desc = colDesc != -1 ? row[colDesc]?.ToString()?.Trim() : "";
                    var semester = colSemester != -1 ? row[colSemester]?.ToString()?.Trim() : "";
                    var lecturerEmail = colLecturerEmail != -1 ? row[colLecturerEmail]?.ToString()?.Trim() : "";
                    var faculty = colFaculty != -1 ? row[colFaculty]?.ToString()?.Trim() : "";
                    var majorCode = colMajorCode != -1 ? row[colMajorCode]?.ToString()?.Trim() : "";
                    var majorName = colMajorName != -1 ? row[colMajorName]?.ToString()?.Trim() : "";
                    var category = colCategory != -1 ? row[colCategory]?.ToString()?.Trim() : "";
                    var levelText = colLevel != -1 ? row[colLevel]?.ToString()?.Trim() : "";
                    var deadlineText = colDeadline != -1 ? row[colDeadline]?.ToString()?.Trim() : "";

                    if (string.IsNullOrEmpty(title)) { skipped++; continue; }

                    var exists = await _context.Topics.AnyAsync(t => t.TopicCode == code);
                    if (exists) { skipped++; continue; }

                    var majorQuery = _context.Majors.Where(m => m.IsActive);
                    Major? major = null;

                    if (!string.IsNullOrWhiteSpace(majorCode))
                    {
                        var normalizedCode = majorCode.Trim().ToUpperInvariant();
                        major = await majorQuery.FirstOrDefaultAsync(m =>
                            m.MajorCode != null && m.MajorCode.ToUpper() == normalizedCode);
                    }

                    if (major == null && !string.IsNullOrWhiteSpace(majorName))
                    {
                        var normalizedName = majorName.Trim().ToUpperInvariant();
                        major = await majorQuery.FirstOrDefaultAsync(m => m.Name.ToUpper() == normalizedName);
                    }

                    if (major == null && !string.IsNullOrWhiteSpace(faculty))
                    {
                        var normalizedFaculty = faculty.Trim().ToUpperInvariant();
                        major = await majorQuery
                            .OrderBy(m => m.Name)
                            .FirstOrDefaultAsync(m => m.FacultyName != null && m.FacultyName.ToUpper() == normalizedFaculty);
                    }

                    if (major == null)
                    {
                        skipped++;
                        continue;
                    }

                    int maxStudents = 1;
                    if (colMaxStudents != -1 && int.TryParse(row[colMaxStudents]?.ToString(), out var parsedMax))
                    {
                        maxStudents = Math.Clamp(parsedMax, 1, 10);
                    }

                    if (!Enum.TryParse<TopicLevel>(levelText, true, out var level))
                    {
                        level = TopicLevel.Medium;
                    }

                    var deadline = DateTime.Now.AddMonths(3);
                    if (colDeadline != -1 && row[colDeadline] is DateTime deadlineValue)
                    {
                        deadline = deadlineValue;
                    }
                    else if (!string.IsNullOrWhiteSpace(deadlineText) && DateTime.TryParse(deadlineText, out var parsedDeadline))
                    {
                        deadline = parsedDeadline;
                    }

                    var topic = new Topic
                    {
                        Title = title,
                        TopicCode = code,
                        Description = desc ?? "",
                        Semester = string.IsNullOrWhiteSpace(semester) ? "HK2-2025-2026" : semester,
                        Faculty = major.FacultyName ?? faculty,
                        MajorId = major.Id,
                        DepartmentName = major.Name,
                        Category = string.IsNullOrWhiteSpace(category) ? (maxStudents > 1 ? "Nhóm" : "Cá nhân") : category,
                        Level = level,
                        Deadline = deadline,
                        CreatedAt = DateTime.Now,
                        IsApproved = true,
                        IsRegistrationOpen = true,
                        Status = TopicStatus.Available,
                        MaxStudents = maxStudents
                    };

                    if (!string.IsNullOrEmpty(lecturerEmail))
                    {
                        var lecturer = await _userManager.FindByEmailAsync(lecturerEmail);
                        if (lecturer != null)
                            topic.LecturerId = lecturer.Id;
                        else
                        { skipped++; continue; }
                    }

                    _context.Topics.Add(topic);
                    success++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Import thành công: {success}, Bỏ qua: {skipped}";
            return RedirectToAction(nameof(ThesisManagement));
        }

        // ============================================================
        // IMPORT USERS TỪ EXCEL
        // ============================================================
        [HttpGet]
        public IActionResult ImportUsers(string? role)
        {
            var allowedRoles = new[] { "Admin", "Lecturer", "Student" };
            if (!string.IsNullOrWhiteSpace(role) && !allowedRoles.Contains(role))
            {
                TempData["Error"] = "Role import không hợp lệ.";
                return RedirectToAction(nameof(UserManagement));
            }

            ViewBag.Role = role;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> ImportUsers(IFormFile file, string role)
        {
            var allowedRoles = new[] { "Admin", "Lecturer", "Student" };
            if (!allowedRoles.Contains(role))
            {
                TempData["Error"] = "Role import không hợp lệ.";
                return RedirectToAction(nameof(UserManagement));
            }

            if (!IsValidExcelUpload(file, out var userImportError))
            {
                TempData["Error"] = userImportError;
                return RedirectToAction("UserManagement", new { role });
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = file.OpenReadStream())
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                if (table.Rows.Count < 2)
                {
                    TempData["Error"] = "File Excel không có dữ liệu!";
                    return RedirectToAction("UserManagement", new { role });
                }

                var header = table.Rows[0];
                int colFullName = -1, colEmail = -1, colUserCode = -1, colFaculty = -1,
                    colMajorCode = -1, colMajorName = -1;

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var colName = header[j]?.ToString()?.Trim().ToLower();
                    if (string.IsNullOrEmpty(colName)) continue;

                    if (colName.Contains("majorcode") || colName.Contains("mã ngành") || colName.Contains("ma nganh"))
                        colMajorCode = j;
                    else if (colName.Contains("majorname") || colName.Contains("chuyên ngành") || colName.Contains("chuyen nganh"))
                        colMajorName = j;
                    else if (colName.Contains("fullname") || colName.Contains("họ") || colName.Contains("ho ten") || colName.Contains("tên") || colName.Contains("ten"))
                        colFullName = j;
                    else if (colName.Contains("email"))
                        colEmail = j;
                    else if (colName.Contains("usercode") || colName.Contains("mssv") || colName.Contains("msgv") || colName.Contains("mã số") || colName.Contains("ma so"))
                        colUserCode = j;
                    else if (colName.Contains("khoa") || colName.Contains("faculty"))
                        colFaculty = j;
                }

                if (colEmail == -1 || colFullName == -1)
                {
                    if (table.Columns.Count >= 2)
                    {
                        colFullName = colFullName == -1 ? 0 : colFullName;
                        colEmail = colEmail == -1 ? 1 : colEmail;
                        colUserCode = colUserCode == -1 && table.Columns.Count >= 3 ? 2 : colUserCode;
                        colFaculty = colFaculty == -1 && table.Columns.Count >= 4 ? 3 : colFaculty;
                        colMajorCode = colMajorCode == -1 && table.Columns.Count >= 5 ? 4 : colMajorCode;
                        colMajorName = colMajorName == -1 && table.Columns.Count >= 6 ? 5 : colMajorName;
                    }
                    else
                    {
                        TempData["Error"] = "File Excel thiếu cột Email hoặc Họ tên!";
                        return RedirectToAction("UserManagement", new { role });
                    }
                }

                int success = 0, skipped = 0;

                for (int i = 1; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    var email = row[colEmail]?.ToString()?.Trim();
                    var fullName = row[colFullName]?.ToString()?.Trim();
                    var userCode = colUserCode != -1 ? row[colUserCode]?.ToString()?.Trim() : "";
                    var faculty = colFaculty != -1 ? row[colFaculty]?.ToString()?.Trim() : "";
                    var majorCode = colMajorCode != -1 ? row[colMajorCode]?.ToString()?.Trim() : "";
                    var majorName = colMajorName != -1 ? row[colMajorName]?.ToString()?.Trim() : "";

                    if (string.IsNullOrEmpty(email) || !email.Contains("@")) { skipped++; continue; }

                    var existingUser = await _userManager.FindByEmailAsync(email);
                    if (existingUser != null) { skipped++; continue; }

                    var majorIds = await ResolveImportMajorIdsAsync(majorCode, majorName, faculty);

                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = fullName,
                        UserCode = userCode,
                        Faculty = faculty,
                        MajorId = role == "Student" ? majorIds.Cast<int?>().FirstOrDefault() : null,
                        EmailConfirmed = true
                    };

                    var createResult = await _userManager.CreateAsync(user, GenerateStrongPassword());
                    if (createResult.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, role);
                        await SyncUserMajorsAsync(user.Id, majorIds);
                        success++;
                    }
                    else skipped++;
                }

                TempData["Success"] = $"Import thành công: {success} | Bỏ qua: {skipped}";
            }

            return RedirectToAction("UserManagement", new { role });
        }

        // ============================================================
        // XUẤT EXCEL  →  /Admin/ExportUsers  &  /Admin/ExportTopics
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ExportUsers(string? role = null)
        {
            IList<ApplicationUser> users = string.IsNullOrEmpty(role)
                ? await _userManager.Users.ToListAsync()
                : await _userManager.GetUsersInRoleAsync(role);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Users");

            // Header
            ws.Cell(1, 1).Value = "Họ tên";
            ws.Cell(1, 2).Value = "Email";
            ws.Cell(1, 3).Value = "Mã số";
            ws.Cell(1, 4).Value = "Khoa";
            ws.Cell(1, 5).Value = "Vai trò";
            ws.Row(1).Style.Font.Bold = true;

            int row = 2;
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                ws.Cell(row, 1).Value = u.FullName ?? "";
                ws.Cell(row, 2).Value = u.Email ?? "";
                ws.Cell(row, 3).Value = u.UserCode ?? "";
                ws.Cell(row, 4).Value = u.Faculty ?? "";
                ws.Cell(row, 5).Value = string.Join(", ", roles);
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSach_{role ?? "TatCa"}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportRegistrations(string? status = "Approved", string? semester = null)
        {
            var query = _context.Registrations
                .Include(r => r.Student).ThenInclude(u => u.Major)
                .Include(r => r.Topic).ThenInclude(t => t.Lecturer)
                .Include(r => r.Topic).ThenInclude(t => t.Major)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrEmpty(semester))
                query = query.Where(r => r.Topic != null && r.Topic.Semester == semester);

            var data = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Danh sách đăng ký");

            // ── TIÊU ĐỀ ──────────────────────────────────────────────
            ws.Cell(1, 1).Value = "DANH SÁCH ĐĂNG KÝ ĐỀ TÀI KHÓA LUẬN TỐT NGHIỆP";
            ws.Range(1, 1, 1, 9).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}  |  Trạng thái: {status ?? "Tất cả"}";
            ws.Range(2, 1, 2, 9).Merge();
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ── HEADER BẢNG ──────────────────────────────────────────
            var headers = new[] { "STT", "MSSV", "Họ và tên SV", "Tên đề tài", "Giảng viên HD",
                           "Chuyên ngành", "Trạng thái", "Ngày đăng ký", "Phản hồi GV" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.White;
            }

            // ── DỮ LIỆU ──────────────────────────────────────────────
            int row = 5;
            foreach (var reg in data)
            {
                bool isEven = (row % 2 == 0);
                var fillColor = isEven ? XLColor.FromHtml("#f0f4ff") : XLColor.White;

                ws.Cell(row, 1).Value = row - 4;
                ws.Cell(row, 2).Value = reg.Student?.UserCode ?? "";
                ws.Cell(row, 3).Value = reg.Student?.FullName ?? "";
                ws.Cell(row, 4).Value = reg.Topic?.Title ?? "";
                ws.Cell(row, 5).Value = reg.Topic?.Lecturer?.FullName ?? "";
                ws.Cell(row, 6).Value = reg.Topic?.Major?.Name ?? reg.Topic?.DepartmentName ?? "";

                // Trạng thái có màu
                var statusCell = ws.Cell(row, 7);
                statusCell.Value = reg.Status switch
                {
                    "Approved" => "Đã duyệt",
                    "Pending" => "Chờ duyệt",
                    "Rejected" => "Từ chối",
                    _ => reg.Status
                };
                statusCell.Style.Font.FontColor = reg.Status switch
                {
                    "Approved" => XLColor.FromHtml("#15803d"),
                    "Pending" => XLColor.FromHtml("#b45309"),
                    "Rejected" => XLColor.FromHtml("#dc2626"),
                    _ => XLColor.Black
                };
                statusCell.Style.Font.Bold = true;
                statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 8).Value = reg.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 9).Value = reg.Feedback ?? "";
                ws.Cell(row, 9).Style.Alignment.WrapText = true;

                // Tô nền xen kẽ và viền
                for (int col = 1; col <= 9; col++)
                {
                    ws.Cell(row, col).Style.Fill.BackgroundColor = fillColor;
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(row, col).Style.Border.OutsideBorderColor = XLColor.FromHtml("#e2e8f0");
                    ws.Cell(row, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                row++;
            }

            // ── DÒNG TỔNG KẾT ────────────────────────────────────────
            ws.Cell(row + 1, 1).Value = $"Tổng cộng: {data.Count} bản ghi";
            ws.Range(row + 1, 1, row + 1, 9).Merge();
            ws.Cell(row + 1, 1).Style.Font.Bold = true;
            ws.Cell(row + 1, 1).Style.Font.Italic = true;
            ws.Cell(row + 1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");

            // ── ĐỊNH DẠNG ─────────────────────────────────────────────
            ws.Column(1).Width = 5;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 38;
            ws.Column(5).Width = 22;
            ws.Column(6).Width = 20;
            ws.Column(7).Width = 13;
            ws.Column(8).Width = 18;
            ws.Column(9).Width = 30;

            ws.Row(1).Height = 28;
            ws.Row(4).Height = 22;
            for (int r = 5; r < row; r++) ws.Row(r).Height = 20;

            // Cố định dòng header khi scroll
            ws.SheetView.FreezeRows(4);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            string fileName = $"DangKyDeTai_{status ?? "TatCa"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }


        // ============================================================
        // ĐĂNG KÝ ĐỀ TÀI CỦA SINH VIÊN (Alias giữ lại)
        // ============================================================
        public async Task<IActionResult> RegisterTopic()
        {
            await Task.CompletedTask;
            return RedirectToAction("Index", "Topic");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] Topic? topic)
        {
            if (topic == null)
                return Json(new { success = false, message = "Không nhận được dữ liệu." });

            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");
            ModelState.Remove("Registrations");
            ModelState.Remove("Comments");

            if (string.IsNullOrWhiteSpace(topic.Title))
                return Json(new { success = false, message = "Tên đề tài không được để trống." });

            topic.Title = topic.Title.Trim();
            topic.Description = topic.Description?.Trim() ?? "";
            topic.TopicCode = string.IsNullOrWhiteSpace(topic.TopicCode)
                ? $"TOPIC-{Guid.NewGuid().ToString()[..8].ToUpper()}"
                : topic.TopicCode.Trim();
            topic.CreatedAt = DateTime.Now;
            topic.Deadline = topic.Deadline == default ? DateTime.Now.AddMonths(3) : topic.Deadline;
            topic.MaxStudents = Math.Clamp(topic.MaxStudents <= 0 ? 1 : topic.MaxStudents, 1, 10);
            topic.Category = topic.MaxStudents > 1 ? "Nhóm" : (topic.Category ?? "Ứng dụng");
            topic.Semester = string.IsNullOrWhiteSpace(topic.Semester) ? "HK2-2025-2026" : topic.Semester.Trim();
            topic.IsStudentProposed = false;
            topic.IsApproved = true;
            topic.IsRegistrationOpen = topic.MaxStudents > 0;
            topic.Status = TopicStatus.Available;

            if (!ModelState.IsValid)
            {
                var message = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return Json(new { success = false, message = string.IsNullOrWhiteSpace(message) ? "Dữ liệu không hợp lệ." : message });
            }

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tạo đề tài thành công." });
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================
        

        private async Task<List<string>> GetAcademicYearOptions()
        {
            var years = await _context.Topics
                .Where(t => t.Semester != null && t.Semester.Contains("-"))
                .Select(t => t.Semester!)
                .ToListAsync();

            var parsedYears = years
                .Select(ExtractAcademicYear)
                .Where(y => !string.IsNullOrWhiteSpace(y))
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            var currentYear = GetCurrentAcademicYear();
            if (!parsedYears.Contains(currentYear))
            {
                parsedYears.Insert(0, currentYear);
            }

            return parsedYears;
        }

        private static string GetCurrentAcademicYear()
        {
            var now = DateTime.Now;
            int startYear = now.Month >= 8 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static string ExtractAcademicYear(string semester)
        {
            var parts = semester.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                return $"{parts[^2]}-{parts[^1]}";
            }

            return string.Empty;
        }

        private static string GetTimeAgo(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "Vừa xong";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} phút trước";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} giờ trước";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} ngày trước";
            return dt.ToString("dd/MM/yyyy");
        }

        private static string AbbreviateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "N/A";
            if (name.Length <= 10) return name;
            return string.Concat(name.Split(' ')
                .Where(s => s.Length > 0)
                .Select(s => s[0]))
                .ToUpper();
        }

        private static string MapNotificationIcon(string? type) => type switch
        {
            "TopicApproved" => "check_circle",
            "TopicRejected" => "cancel",
            "NewRegistration" => "person_add",
            "NewTopic" => "description",
            "SystemAlert" => "warning",
            _ => "notifications"
        };

        private static string MapNotificationColor(string? type) => type switch
        {
            "TopicApproved" => "bg-green-100 text-green-600",
            "TopicRejected" => "bg-red-100 text-red-600",
            "NewRegistration" => "bg-blue-100 text-blue-600",
            "NewTopic" => "bg-purple-100 text-purple-600",
            "SystemAlert" => "bg-orange-100 text-orange-600",
            _ => "bg-slate-100 text-slate-600"
        };
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.Settings.ToListAsync();
            ViewBag.Majors = await _context.Majors
                .OrderBy(m => m.FacultyName)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View(settings);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMajor(
            string name,
            string? majorCode,
            string? facultyName,
            string? description,
            bool isActive = true)
        {
            name = name?.Trim() ?? string.Empty;
            majorCode = majorCode?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên chuyên ngành không được để trống.";
                return RedirectToAction(nameof(Settings));
            }

            if (await IsDuplicateMajorAsync(name, majorCode, null))
            {
                TempData["Error"] = "Tên hoặc mã chuyên ngành đã tồn tại.";
                return RedirectToAction(nameof(Settings));
            }

            _context.Majors.Add(new Major
            {
                Name = name,
                MajorCode = string.IsNullOrWhiteSpace(majorCode) ? null : majorCode.ToUpperInvariant(),
                FacultyName = facultyName?.Trim(),
                Description = description?.Trim(),
                IsActive = isActive
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm chuyên ngành mới.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMajor(
            int id,
            string name,
            string? majorCode,
            string? facultyName,
            string? description,
            bool isActive = false)
        {
            var major = await _context.Majors.FindAsync(id);
            if (major == null)
            {
                TempData["Error"] = "Không tìm thấy chuyên ngành cần cập nhật.";
                return RedirectToAction(nameof(Settings));
            }

            name = name?.Trim() ?? string.Empty;
            majorCode = majorCode?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên chuyên ngành không được để trống.";
                return RedirectToAction(nameof(Settings));
            }

            if (await IsDuplicateMajorAsync(name, majorCode, id))
            {
                TempData["Error"] = "Tên hoặc mã chuyên ngành đã tồn tại.";
                return RedirectToAction(nameof(Settings));
            }

            major.Name = name;
            major.MajorCode = string.IsNullOrWhiteSpace(majorCode) ? null : majorCode.ToUpperInvariant();
            major.FacultyName = facultyName?.Trim();
            major.Description = description?.Trim();
            major.IsActive = isActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật chuyên ngành.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMajor(int id)
        {
            var major = await _context.Majors.FindAsync(id);
            if (major == null)
            {
                TempData["Error"] = "Không tìm thấy chuyên ngành.";
                return RedirectToAction(nameof(Settings));
            }

            major.IsActive = !major.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = major.IsActive
                ? "Đã bật chuyên ngành."
                : "Đã ẩn chuyên ngành khỏi các màn đăng ký.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMajor(int id)
        {
            var major = await _context.Majors
                .Include(m => m.Topics)
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (major == null)
            {
                TempData["Error"] = "Không tìm thấy chuyên ngành cần xóa.";
                return RedirectToAction(nameof(Settings));
            }

            var hasReferences = (major.Topics?.Any() ?? false) || (major.Users?.Any() ?? false);
            if (hasReferences)
            {
                major.IsActive = false;
                TempData["Success"] = "Chuyên ngành đang có dữ liệu liên quan nên đã được ẩn thay vì xóa.";
            }
            else
            {
                _context.Majors.Remove(major);
                TempData["Success"] = "Đã xóa chuyên ngành.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Settings));
        }

        private async Task<bool> IsDuplicateMajorAsync(string name, string? majorCode, int? ignoreId)
        {
            var normalizedName = name.Trim().ToUpper();
            var normalizedCode = string.IsNullOrWhiteSpace(majorCode)
                ? null
                : majorCode.Trim().ToUpper();

            return await _context.Majors.AnyAsync(m =>
                (!ignoreId.HasValue || m.Id != ignoreId.Value)
                && (m.Name.ToUpper() == normalizedName
                    || (normalizedCode != null && m.MajorCode != null && m.MajorCode.ToUpper() == normalizedCode)));
        }

        private async Task SyncUserMajorsAsync(string userId, IEnumerable<int>? majorIds)
        {
            var requestedIds = (majorIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var validMajorIds = await _context.Majors
                .Where(m => m.IsActive && requestedIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();

            List<UserMajor> existing;
            try
            {
                existing = await _context.UserMajors
                    .Where(um => um.UserId == userId)
                    .ToListAsync();
            }
            catch (DbException ex) when (ex.Message.Contains("UserMajors", StringComparison.OrdinalIgnoreCase))
            {
                await SyncUserPrimaryMajorOnlyAsync(userId, validMajorIds);
                return;
            }

            _context.UserMajors.RemoveRange(existing.Where(um => !validMajorIds.Contains(um.MajorId)));

            var existingIds = existing.Select(um => um.MajorId).ToHashSet();
            foreach (var majorId in validMajorIds.Where(id => !existingIds.Contains(id)))
            {
                _context.UserMajors.Add(new UserMajor
                {
                    UserId = userId,
                    MajorId = majorId
                });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user != null && validMajorIds.Any())
            {
                user.MajorId ??= validMajorIds.First();
                var faculty = await _context.Majors
                    .Where(m => m.Id == validMajorIds.First())
                    .Select(m => m.FacultyName)
                    .FirstOrDefaultAsync();
                user.Faculty = faculty ?? user.Faculty;
            }

            await _context.SaveChangesAsync();
        }

        private async Task SyncUserPrimaryMajorOnlyAsync(string userId, List<int> validMajorIds)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null && validMajorIds.Any())
            {
                user.MajorId ??= validMajorIds.First();
                var faculty = await _context.Majors
                    .Where(m => m.Id == validMajorIds.First())
                    .Select(m => m.FacultyName)
                    .FirstOrDefaultAsync();
                user.Faculty = faculty ?? user.Faculty;
                await _context.SaveChangesAsync();
            }
        }

        private async Task<List<int>> ResolveImportMajorIdsAsync(string? majorCode, string? majorName, string? faculty)
        {
            var majorQuery = _context.Majors.Where(m => m.IsActive);
            var majorIds = new List<int>();

            if (!string.IsNullOrWhiteSpace(majorCode))
            {
                var requestedCodes = majorCode
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(code => code.Trim().ToUpperInvariant())
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .ToList();

                if (requestedCodes.Any())
                {
                    majorIds.AddRange(await majorQuery
                        .Where(m => m.MajorCode != null && requestedCodes.Contains(m.MajorCode.ToUpper()))
                        .Select(m => m.Id)
                        .ToListAsync());
                }
            }

            if (!majorIds.Any() && !string.IsNullOrWhiteSpace(majorName))
            {
                var requestedNames = majorName
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim().ToUpperInvariant())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (requestedNames.Any())
                {
                    majorIds.AddRange(await majorQuery
                        .Where(m => requestedNames.Contains(m.Name.ToUpper()))
                        .Select(m => m.Id)
                        .ToListAsync());
                }
            }

            if (!majorIds.Any() && !string.IsNullOrWhiteSpace(faculty))
            {
                var normalizedFaculty = faculty.Trim().ToUpperInvariant();
                majorIds.AddRange(await majorQuery
                    .Where(m => m.FacultyName != null && m.FacultyName.ToUpper() == normalizedFaculty)
                    .Select(m => m.Id)
                    .ToListAsync());
            }

            return majorIds.Distinct().ToList();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(List<string> names, IFormCollection form)
        {
            if (names == null || names.Count == 0)
            {
                TempData["Error"] = "Không có cấu hình nào được gửi lên.";
                return RedirectToAction(nameof(Settings));
            }

            var settingNames = names
                .Select(n => n?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct();

            var postedSettings = settingNames.ToDictionary(
                name => name,
                name =>
                {
                    var postedValues = form["values_" + name];
                    return postedValues.Count > 0
                        ? postedValues[postedValues.Count - 1]?.Trim() ?? string.Empty
                        : string.Empty;
                });

            if (postedSettings.TryGetValue("Registration_Start", out var registrationStartValue)
                && postedSettings.TryGetValue("Registration_End", out var registrationEndValue)
                && DateTime.TryParse(registrationStartValue, out var registrationStart)
                && DateTime.TryParse(registrationEndValue, out var registrationEnd)
                && registrationStart > registrationEnd)
            {
                TempData["Error"] = "Thời gian mở đăng ký không được sau thời gian đóng đăng ký.";
                return RedirectToAction(nameof(Settings));
            }

            if (postedSettings.TryGetValue("Semester_Start", out var semesterStartValue)
                && postedSettings.TryGetValue("Semester_End", out var semesterEndValue)
                && DateTime.TryParse(semesterStartValue, out var semesterStart)
                && DateTime.TryParse(semesterEndValue, out var semesterEnd)
                && semesterStart > semesterEnd)
            {
                TempData["Error"] = "Ngày bắt đầu học kỳ không được sau ngày kết thúc học kỳ.";
                return RedirectToAction(nameof(Settings));
            }

            if (postedSettings.TryGetValue("Max_Student_Per_Topic", out var maxStudentValue)
                && (!int.TryParse(maxStudentValue, out var maxStudent) || maxStudent < 1 || maxStudent > 5))
            {
                TempData["Error"] = "Số sinh viên tối đa mỗi đề tài phải nằm trong khoảng 1 đến 5.";
                return RedirectToAction(nameof(Settings));
            }

            if (postedSettings.TryGetValue("Min_GPA", out var minGpaValue)
                && (!double.TryParse(minGpaValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var minGpa)
                    || minGpa < 0 || minGpa > 10))
            {
                TempData["Error"] = "GPA tối thiểu phải nằm trong khoảng 0 đến 10.";
                return RedirectToAction(nameof(Settings));
            }

            foreach (var (name, value) in postedSettings)
            {
                var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Name == name);
                if (setting != null)
                {
                    setting.Value = value;
                }
                else
                {
                    _context.Settings.Add(new Setting { Name = name, Value = value });
                }
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cấu hình hệ thống đã được cập nhật!";
            return RedirectToAction(nameof(Settings));
        }
        // ============================================================
        // FILE: Controllers/Admin/AdminController.cs  — BỔ SUNG THÊM
        // Các action cần THÊM VÀO cuối class AdminController hiện tại
        // (trước dấu đóng ngoặc cuối cùng của class)
        //
        // Bổ sung:
        //   1. ApproveStudentProposal → duyệt đề tài SV đề xuất
        //   2. RejectStudentProposal  → từ chối đề xuất của SV
        //   3. StudentProposals       → trang quản lý đề xuất
        //   4. EditTopic (GET)        → sửa đề tài trực tiếp từ Admin
        //   5. EditTopic (POST)       → lưu sửa đề tài
        // ============================================================

        // ── THÊM VÀO AdminController ─────────────────────────────────

        // ============================================================
        // QUẢN LÝ ĐỀ XUẤT CỦA SINH VIÊN  →  /Admin/StudentProposals
        // ============================================================
        public async Task<IActionResult> StudentProposals(string? status = null)
        {
            var query = _context.Topics
                .Include(t => t.Student)
                .Include(t => t.Major)
                .Include(t => t.Lecturer)
                .Where(t => t.IsStudentProposed)
                .AsQueryable();

            if (status == "pending") query = query.Where(t => !t.IsApproved);
            else if (status == "approved") query = query.Where(t => t.IsApproved);

            var topics = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.PendingCount = await _context.Topics.CountAsync(t => t.IsStudentProposed && !t.IsApproved);
            ViewBag.ApprovedCount = await _context.Topics.CountAsync(t => t.IsStudentProposed && t.IsApproved);
            ViewBag.StatusFilter = status;

            // Danh sách giảng viên để phân công
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers.OrderBy(l => l.FullName).ToList();

            return View(topics);
        }

        // POST: Duyệt đề xuất sinh viên (và phân công GV)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStudentProposal(int topicId, string? assignLecturerId)
        {
            var topic = await _context.Topics
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            topic.IsApproved = true;
            topic.IsRegistrationOpen = true;
            topic.Status = TopicStatus.Available;

            if (!string.IsNullOrEmpty(assignLecturerId))
                topic.LecturerId = assignLecturerId;

            await _context.SaveChangesAsync();

            // Thông báo cho sinh viên đề xuất
            if (!string.IsNullOrEmpty(topic.CreatedByStudentId))
                await _notificationService.SendDualNotification(
                    topic.CreatedByStudentId,
                    "Đề xuất đề tài được duyệt! 🎉",
                    $"Đề tài \"{topic.Title}\" của bạn đã được Admin phê duyệt và công khai.",
                    "TopicApproved",
                    topic.Id); // Truyền topic.Id vào tham số relatedId

            // Thông báo cho GV được phân công
            if (!string.IsNullOrEmpty(assignLecturerId))
                await _notificationService.SendDualNotification(
                    assignLecturerId,
                    "Được phân công hướng dẫn đề tài đề xuất",
                    $"Admin đã phân công bạn hướng dẫn đề tài đề xuất \"{topic.Title}\".",
                    "TopicApproved",
                    topic.Id);

            TempData["Success"] = $"Đã duyệt đề xuất \"{topic.Title}\"!";
            return RedirectToAction(nameof(StudentProposals));
        }

        // POST: Từ chối đề xuất sinh viên
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStudentProposal(int topicId, string? reason)
        {
            var topic = await _context.Topics
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            topic.IsApproved = false;
            topic.Status = TopicStatus.Closed;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.CreatedByStudentId))
                await _notificationService.SendDualNotification(
                    topic.CreatedByStudentId,
                    "Đề xuất đề tài bị từ chối",
                    $"Đề tài \"{topic.Title}\" của bạn đã bị từ chối." + (string.IsNullOrEmpty(reason) ? "" : $" Lý do: {reason}"),
                    "TopicRejected",
                    topic.Id);

            TempData["Error"] = $"Đã từ chối đề xuất \"{topic.Title}\".";
            return RedirectToAction(nameof(StudentProposals));
        }

        // ============================================================
        // SỬA ĐỀ TÀI (Admin)  →  GET + POST /Admin/EditTopic/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> EditTopic(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.Major)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = new SelectList(
                lecturers.OrderBy(l => l.FullName),
                "Id", "FullName", topic.LecturerId);
            ViewBag.Majors = new SelectList(
                await _context.Majors.Where(m => m.IsActive).ToListAsync(),
                "Id", "Name", topic.MajorId);

            return View(topic);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("EditTopic")]
        public async Task<IActionResult> EditTopicPost(Topic model)
        {
            var topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == model.Id);

            if (topic == null)
                return NotFound();

            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");
            ModelState.Remove("Registrations");

            if (!ModelState.IsValid)
            {
                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");

                ViewBag.Lecturers = new SelectList(
                    lecturers.OrderBy(l => l.FullName),
                    "Id",
                    "FullName",
                    model.LecturerId
                );

                ViewBag.Majors = new SelectList(
                    await _context.Majors
                        .Where(m => m.IsActive)
                        .ToListAsync(),
                    "Id",
                    "Name",
                    model.MajorId
                );

                return View(model);
            }

            topic.Title = model.Title;
            topic.Description = model.Description;
            topic.LecturerId = model.LecturerId;
            topic.MajorId = model.MajorId;
            topic.Level = model.Level;
            topic.MaxStudents = model.MaxStudents;
            topic.Status = model.Status;
            topic.IsApproved = model.IsApproved;
            topic.Semester = model.Semester;
            topic.Deadline = model.Deadline;
            topic.Category = model.Category;
            topic.Note = model.Note;

            topic.IsRegistrationOpen = model.IsRegistrationOpen;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật đề tài thành công!";

            return RedirectToAction(nameof(ManageTopics));
        }

        // ============================================================
        // CẬP NHẬT THÔNG TIN CÁ NHÂN (Admin profile)  →  POST
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAdminProfile(
            string fullName, string? phoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = fullName?.Trim() ?? user.FullName;
            user.PhoneNumber = phoneNumber?.Trim();
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Đã cập nhật thông tin!";
            return RedirectToAction(nameof(Settings));
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRegistrationStatus([FromBody] UpdateRegVM model)
        {
            if (model == null)
                return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == model.Id);

            if (topic == null)
                return Json(new { success = false, message = "Không tìm thấy đề tài." });

            var reservedCount = topic.Registrations?.Count(r => r.Status == "Pending" || r.Status == "Approved") ?? 0;

            if (model.IsOpen && !topic.IsApproved)
                return Json(new { success = false, message = "Đề tài chưa được duyệt nên chưa thể mở đăng ký." });

            if (model.IsOpen && reservedCount >= topic.MaxStudents)
                return Json(new { success = false, message = "Đề tài đã đủ số lượng sinh viên đăng ký/chờ duyệt." });

            topic.IsRegistrationOpen = model.IsOpen;

            if (!model.IsOpen)
            {
                topic.Status = TopicStatus.Closed;
            }
            else
            {
                topic.Status = TopicStatus.Available;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        public async Task<IActionResult> TopicRegistrations(int id)
        {
            var registrations = await _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.Topic)
                .Where(r => r.TopicId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == id);

            return View(registrations);
        }
        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApproveTopics([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Json(new { success = false, message = "Vui lòng chọn đề tài." });

            var topics = await _context.Topics
                .Include(t => t.Registrations)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            foreach (var topic in topics)
            {
                var reservedCount = topic.Registrations?.Count(r => r.Status == "Pending" || r.Status == "Approved") ?? 0;
                topic.IsApproved = true;
                topic.IsRegistrationOpen = reservedCount < topic.MaxStudents;
                topic.Status = reservedCount >= topic.MaxStudents ? TopicStatus.Full : TopicStatus.Available;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkRejectTopics([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Json(new { success = false, message = "Vui lòng chọn đề tài." });

            var topics = await _context.Topics
                .Include(t => t.Registrations)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            var blocked = topics
                .Where(t => (t.Registrations ?? new List<Registration>())
                    .Any(r => r.Status == "Pending" || r.Status == "Approved"))
                .ToList();

            if (blocked.Any())
            {
                return Json(new
                {
                    success = false,
                    message = $"Không thể từ chối {blocked.Count} đề tài đã có sinh viên đăng ký/chờ duyệt."
                });
            }

            foreach (var topic in topics)
            {
                topic.IsApproved = false;
                topic.IsRegistrationOpen = false;
                topic.Status = TopicStatus.Rejected;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private static bool IsValidExcelUpload(IFormFile? file, out string error)
        {
            error = string.Empty;

            if (file == null || file.Length == 0)
            {
                error = "Vui lòng chọn file Excel.";
                return false;
            }

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                error = "File Excel không được vượt quá 5MB.";
                return false;
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".xls",
                ".xlsx"
            };

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                error = "Chỉ hỗ trợ file Excel .xls hoặc .xlsx.";
                return false;
            }

            return true;
        }

        private static string GenerateStrongPassword()
        {
            const string upperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijkmnopqrstuvwxyz";
            const string nums = "23456789";
            const string special = "@#!$%";
            const string allChars = upperChars + lowerChars + nums + special;

            var passwordChars = new List<char>
            {
                upperChars[RandomNumberGenerator.GetInt32(upperChars.Length)],
                upperChars[RandomNumberGenerator.GetInt32(upperChars.Length)],
                lowerChars[RandomNumberGenerator.GetInt32(lowerChars.Length)],
                lowerChars[RandomNumberGenerator.GetInt32(lowerChars.Length)],
                nums[RandomNumberGenerator.GetInt32(nums.Length)],
                nums[RandomNumberGenerator.GetInt32(nums.Length)],
                special[RandomNumberGenerator.GetInt32(special.Length)]
            };

            passwordChars.AddRange(Enumerable.Range(0, 3)
                .Select(_ => allChars[RandomNumberGenerator.GetInt32(allChars.Length)]));

            for (int i = passwordChars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]);
            }

            return new string(passwordChars.ToArray());
        }
    }
}
