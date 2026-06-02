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
        private const string AdminSelectedPeriodSessionKey = "AdminSelectedPeriodName";
        private const string LecturerApprovedProposalPrefix = "[LECTURER_APPROVED]";
        private const string LecturerRejectedProposalPrefix = "[LECTURER_REJECTED]";
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            SetAdminSelectedPeriod(activePeriod.Name);

            var topicQuery = FilterTopicsByActivePeriod(_context.Topics.AsQueryable(), activePeriod);
            var registrationQuery = FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod);

            var totalTopics = await topicQuery.CountAsync();
            var pendingApprovals = await topicQuery
                .CountAsync(t => t.Status == TopicStatus.Pending && !t.IsApproved);
            var totalLecturers = await topicQuery
                .Where(t => !string.IsNullOrWhiteSpace(t.LecturerId))
                .Select(t => t.LecturerId!)
                .Distinct()
                .CountAsync();
            var totalStudents = await _context.PeriodStudents
                .CountAsync(ps => ps.RegistrationPeriodId == activePeriod.Id
                    && ps.IsEligible
                    && !ps.Student.HasCompletedThesis);

            var approvedStudentIds = await registrationQuery
                .Where(r => r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .CountAsync();

            double registrationRate = totalStudents > 0
                ? Math.Round((double)approvedStudentIds / totalStudents * 100, 1)
                : 0;

            // Biểu đồ phân bổ theo Khoa
            var departmentData = await topicQuery
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

            var recentRegistrationCounts = await registrationQuery
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
            var newTopics = await topicQuery
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

            ViewBag.ActivePeriod = activePeriod;
            return View(model);
        }

        // ============================================================
        // THỐNG KÊ HỆ THỐNG  →  /Admin/Statistics
        // ============================================================
        public async Task<IActionResult> Statistics(string? semester = null, string? year = null)
        {
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            semester = string.IsNullOrWhiteSpace(semester) ? activePeriod.SemesterCode : semester.Trim();
            year = string.IsNullOrWhiteSpace(year) ? activePeriod.AcademicYear : year.Trim();
            var semesterKey = $"{semester}-{year}";
            SetAdminSelectedPeriod(semesterKey);
            var selectedPeriod = await _context.RegistrationPeriods
                .FirstOrDefaultAsync(p => p.Name == semesterKey);

            var topicQuery = _context.Topics.AsQueryable();
            var registrationQuery = _context.Registrations
                .Include(r => r.Topic)
                .AsQueryable();

            if (selectedPeriod == null)
            {
                topicQuery = topicQuery.Where(_ => false);
                registrationQuery = registrationQuery.Where(_ => false);
            }
            else
            {
                topicQuery = topicQuery.Where(t =>
                    t.RegistrationPeriodId == selectedPeriod.Id
                    || (t.RegistrationPeriodId == null && t.Semester == selectedPeriod.Name));

                registrationQuery = registrationQuery.Where(r =>
                    r.RegistrationPeriodId == selectedPeriod.Id
                    || (r.Topic != null && r.RegistrationPeriodId == null && r.Topic.Semester == selectedPeriod.Name));
            }

            var totalTopics = await topicQuery.CountAsync();
            var approvedTopics = await topicQuery.CountAsync(t => t.IsApproved);
            var pendingTopics = await topicQuery.CountAsync(t => !t.IsApproved && t.Status == TopicStatus.Pending);
            var rejectedTopics = await topicQuery.CountAsync(t => t.Status == TopicStatus.Rejected);
            var otherUnapprovedTopics = await topicQuery.CountAsync(t =>
                !t.IsApproved && t.Status != TopicStatus.Pending && t.Status != TopicStatus.Rejected);
            var totalLecturers = await topicQuery
                .Where(t => t.LecturerId != null)
                .Select(t => t.LecturerId!)
                .Distinct()
                .CountAsync();

            var registrations = await registrationQuery.ToListAsync();
            var totalStudents = selectedPeriod == null
                ? 0
                : await _context.PeriodStudents.CountAsync(ps =>
                    ps.RegistrationPeriodId == selectedPeriod.Id &&
                    ps.IsEligible &&
                    !ps.Student.HasCompletedThesis);

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
                ? Math.Min(100, Math.Round((double)registeredStudentIds / totalStudents * 100, 1))
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
            // TIẾN ĐỘ ĐĂNG KÝ THEO NGÀY
            // =========================
            var progressStart = selectedPeriod?.RegistrationOpenAt.Date
                ?? registrations.Select(r => r.CreatedAt.Date).DefaultIfEmpty(DateTime.Today).Min();
            var progressEnd = selectedPeriod?.RegistrationCloseAt.Date ?? DateTime.Today;
            var latestRegistrationDate = registrations.Select(r => r.CreatedAt.Date).DefaultIfEmpty(progressStart).Max();
            progressEnd = new[] { progressEnd, latestRegistrationDate }.Max();

            if (progressEnd < progressStart)
            {
                progressEnd = progressStart;
            }

            if ((progressEnd - progressStart).TotalDays > 45)
            {
                progressStart = progressEnd.AddDays(-45);
            }

            var activeRegistrations = registrations
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .GroupBy(r => r.StudentId)
                .Select(g => g.Min(r => r.CreatedAt.Date))
                .ToList();

            var progressDays = Enumerable.Range(0, (progressEnd - progressStart).Days + 1)
                .Select(offset => progressStart.AddDays(offset))
                .ToList();

            var today = DateTime.Today;
            var progressCounts = progressDays
                .Select(day => day > today
                    ? (int?)null
                    : activeRegistrations.Count(d => d <= day))
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
            ViewBag.RejectedTopics = rejectedTopics;
            ViewBag.OtherUnapprovedTopics = otherUnapprovedTopics;
            ViewBag.TotalRegistrations = registrations.Count;

            ViewBag.RegistrationRate = registrationRate;
            ViewBag.SelectedSemester = semester;
            ViewBag.SelectedYear = year;
            ViewBag.YearOptions = await GetAcademicYearOptions();
            ViewBag.SemesterOptions = await GetSemesterOptions();
            ViewBag.ActivePeriod = selectedPeriod;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? semesterKey;

            // Major chart
            ViewBag.MajorLabels = majorStats.Select(x => x.Faculty).ToList();
            ViewBag.MajorCounts = majorStats.Select(x => x.Count).ToList();

            ViewBag.TopicStatusLabels = new List<string> { "Đã duyệt", "Chờ duyệt", "Từ chối/khác" };
            ViewBag.TopicStatusCounts = new List<int>
            {
                approvedTopics,
                pendingTopics,
                rejectedTopics + otherUnapprovedTopics
            };

            // Registration progress chart
            ViewBag.MonthLabels = progressDays
                .Select(d => d.ToString("dd/MM"))
                .ToList();

            ViewBag.MonthCounts = progressCounts;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportStatistics(string? semester = null, string? year = null)
        {
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            semester = string.IsNullOrWhiteSpace(semester) ? activePeriod.SemesterCode : semester.Trim();
            year = string.IsNullOrWhiteSpace(year) ? activePeriod.AcademicYear : year.Trim();
            var semesterKey = $"{semester}-{year}";
            var selectedPeriod = await _context.RegistrationPeriods
                .FirstOrDefaultAsync(p => p.Name == semesterKey);

            var topicQuery = _context.Topics
                .Include(t => t.Major)
                .AsQueryable();
            var registrationQuery = _context.Registrations
                .Include(r => r.Topic)
                .AsQueryable();

            if (selectedPeriod == null)
            {
                topicQuery = topicQuery.Where(_ => false);
                registrationQuery = registrationQuery.Where(_ => false);
            }
            else
            {
                topicQuery = topicQuery.Where(t =>
                    t.RegistrationPeriodId == selectedPeriod.Id
                    || (t.RegistrationPeriodId == null && t.Semester == selectedPeriod.Name));
                registrationQuery = registrationQuery.Where(r =>
                    r.RegistrationPeriodId == selectedPeriod.Id
                    || (r.Topic != null && r.RegistrationPeriodId == null && r.Topic.Semester == selectedPeriod.Name));
            }

            var topics = await topicQuery.ToListAsync();
            var registrations = await registrationQuery.ToListAsync();

            var registeredStudents = registrations
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .Count();
            var totalStudents = selectedPeriod == null
                ? 0
                : await _context.PeriodStudents.CountAsync(ps =>
                    ps.RegistrationPeriodId == selectedPeriod.Id &&
                    ps.IsEligible &&
                    !ps.Student.HasCompletedThesis);
            var totalLecturers = topics
                .Where(t => !string.IsNullOrWhiteSpace(t.LecturerId))
                .Select(t => t.LecturerId)
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
        public async Task<IActionResult> Approval(string? status = null, string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetAdminSelectedPeriodAsync(semester, year);
            var registrationQuery = _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .AsQueryable();

            registrationQuery = selectedPeriod == null
                ? registrationQuery.Where(_ => false)
                : registrationQuery.Where(r =>
                    r.RegistrationPeriodId == selectedPeriod.Id
                    || (r.RegistrationPeriodId == null && r.Topic != null && r.Topic.Semester == selectedPeriod.Name));

            var registrations = await registrationQuery
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.ApprovedCount = registrations.Count(r => r.Status == "Approved");
            ViewBag.PendingCount = registrations.Count(r => r.Status == "Pending");
            ViewBag.RejectedCount = registrations.Count(r => r.Status == "Rejected");
            ViewBag.TotalRegistrations = registrations.Count;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();

            return View(registrations);
        }

        // POST: Duyệt 1 đăng ký — ĐÃ SỬA bug NullReference
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int id, string? semester = null, string? year = null, string? status = "Pending")
        {
            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();
            if (reg.Topic == null)
            {
                TempData["Error"] = "Đăng ký này không còn đề tài hợp lệ.";
                return RedirectToAction(nameof(Approval), new { status, semester, year });
            }

            // SV đã có đề tài approved chưa
            var alreadyApproved = await _context.Registrations
                .AnyAsync(r =>
                    r.StudentId == reg.StudentId &&
                    r.Status == "Approved" &&
                    r.Id != reg.Id);

            if (alreadyApproved)
            {
                TempData["Error"] = "Sinh viên đã có đề tài được duyệt!";
                return RedirectToAction(nameof(Approval), new { status, semester, year });
            }

            var currentApprovedCount = await _context.Registrations
                .CountAsync(r => r.TopicId == reg.TopicId && r.Status == "Approved");

            if (currentApprovedCount >= reg.Topic.MaxStudents)
            {
                reg.Topic.Status = TopicStatus.Full;
                reg.Topic.IsRegistrationOpen = false;
                await _context.SaveChangesAsync();

                TempData["Error"] = "Đề tài đã đủ sinh viên, không thể duyệt thêm.";
                return RedirectToAction(nameof(Approval), new { status, semester, year });
            }

            reg.UpdatedAt = DateTime.Now;
            reg.ApprovedBy = User.Identity?.Name;

            reg.Status = "Approved";

            if (currentApprovedCount + 1 >= reg.Topic.MaxStudents)
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
            return RedirectToAction(nameof(Approval), new { status = status ?? "Pending", semester, year });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAllRegistrations(string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetAdminSelectedPeriodAsync(semester, year);
            if (selectedPeriod == null)
            {
                TempData["Error"] = "Không tìm thấy đợt đăng ký cần duyệt.";
                return RedirectToAction(nameof(Approval), new { status = "Pending", semester, year });
            }

            var pendingRegs = await _context.Registrations
                .Include(r => r.Topic)
                .Where(r => r.Status == "Pending"
                    && (r.RegistrationPeriodId == selectedPeriod.Id
                        || (r.RegistrationPeriodId == null && r.Topic != null && r.Topic.Semester == selectedPeriod.Name)))
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            var existingApprovedStudentIds = await _context.Registrations
                .Where(r => r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .ToListAsync();
            var approvedStudents = existingApprovedStudentIds.ToHashSet();

            var topicApprovedCounts = await _context.Registrations
                .Where(r => r.Status == "Approved")
                .GroupBy(r => r.TopicId)
                .Select(g => new { TopicId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TopicId, x => x.Count);

            var approvedRegs = new List<Registration>();
            int skipped = 0;

            foreach (var reg in pendingRegs)
            {
                if (reg.Topic == null || approvedStudents.Contains(reg.StudentId))
                {
                    skipped++;
                    continue;
                }

                var currentApprovedCount = topicApprovedCounts.TryGetValue(reg.TopicId, out var count)
                    ? count
                    : 0;

                if (currentApprovedCount >= reg.Topic.MaxStudents)
                {
                    reg.Topic.Status = TopicStatus.Full;
                    reg.Topic.IsRegistrationOpen = false;
                    skipped++;
                    continue;
                }

                reg.Status = "Approved";
                reg.UpdatedAt = DateTime.Now;
                reg.ApprovedBy = User.Identity?.Name;
                approvedRegs.Add(reg);
                approvedStudents.Add(reg.StudentId);
                topicApprovedCounts[reg.TopicId] = currentApprovedCount + 1;

                if (currentApprovedCount + 1 >= reg.Topic.MaxStudents)
                {
                    reg.Topic.Status = TopicStatus.Full;
                    reg.Topic.IsRegistrationOpen = false;
                }
            }

            await _context.SaveChangesAsync();

            foreach (var reg in approvedRegs)
            {
                await _notificationService.SendDualNotification(
                    reg.StudentId,
                    "🔔 Đăng ký được duyệt",
                    $"Yêu cầu đăng ký đề tài \"{reg.Topic?.Title}\" của bạn đã được Admin phê duyệt.",
                    "TopicApproved",
                    reg.TopicId);
            }

            TempData["Success"] = $"Đã duyệt {approvedRegs.Count} đăng ký. Bỏ qua {skipped} đăng ký không hợp lệ hoặc đề tài đã đủ sinh viên.";
            return RedirectToAction(nameof(Approval), new { status = "Pending", semester, year });
        }

        // POST: Từ chối 1 đăng ký
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id, string? semester = null, string? year = null, string? status = "Pending")
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
            return RedirectToAction(nameof(Approval), new { status = status ?? "Pending", semester, year });
        } // Thêm dấu đóng ngoặc này để fix lỗi CS1513

        // ============================================================
        // QUẢN LÝ KHO ĐỀ TÀI  →  /Admin/ThesisManagement
        // ============================================================
        public async Task<IActionResult> ThesisManagement(
            string? search = null, string? status = null,
            int? majorId = null, int page = 1,
            string? semester = null, string? year = null)
        {
            int pageSize = 10;
            var selectedPeriod = await GetAdminSelectedPeriodAsync(semester, year);

            var query = _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Include(t => t.Registrations)
                .Where(t => !t.IsStudentProposed)
                .AsQueryable();
            query = selectedPeriod == null
                ? query.Where(_ => false)
                : FilterTopicsByActivePeriod(query, selectedPeriod);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.Lecturer != null &&
t.Lecturer.FullName != null &&
t.Lecturer.FullName.Contains(search)));

            if (!string.IsNullOrEmpty(status))
            {
                if (status == nameof(TopicStatus.Full))
                {
                    query = query.Where(t => t.Status == TopicStatus.Full
                        || (t.IsApproved
                            && !t.IsRegistrationOpen
                            && !string.IsNullOrWhiteSpace(t.CreatedByStudentId)));
                }
                else if (status == nameof(TopicStatus.Closed))
                {
                    query = query.Where(t => t.Status == TopicStatus.Closed
                        && string.IsNullOrWhiteSpace(t.CreatedByStudentId));
                }
                else if (Enum.TryParse<TopicStatus>(status, out var parsedStatus))
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

            var periodTopics = selectedPeriod == null
                ? _context.Topics.Where(_ => false)
                : FilterTopicsByActivePeriod(_context.Topics.Where(t => !t.IsStudentProposed), selectedPeriod);
            ViewBag.TotalTopics = await periodTopics.CountAsync();
            ViewBag.OpenTopics = await periodTopics.CountAsync(t => t.IsApproved && t.IsRegistrationOpen);
            ViewBag.ReadyTopics = await periodTopics.CountAsync(t => t.IsApproved && !t.IsRegistrationOpen && t.Status == TopicStatus.Available);
            ViewBag.FullTopics = await periodTopics.CountAsync(t => t.Status == TopicStatus.Full
                || (t.IsApproved
                    && !t.IsRegistrationOpen
                    && !string.IsNullOrWhiteSpace(t.CreatedByStudentId)));
            ViewBag.ClosedTopics = await periodTopics.CountAsync(t => t.Status == TopicStatus.Closed
                && string.IsNullOrWhiteSpace(t.CreatedByStudentId));
            ViewBag.PendingTopics = await periodTopics.CountAsync(t => !t.IsApproved);
            ViewBag.ActivePeriod = selectedPeriod;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
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
            int? majorId = null, int page = 1,
            string? semester = null, string? year = null)
            => ThesisManagement(search, status, majorId, page, semester, year);

        // Duyệt 1 đề tài
        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound();
            if (topic.IsStudentProposed)
            {
                TempData["Error"] = "Đề xuất sinh viên phải đi qua mục Đề xuất từ sinh viên, không duyệt trực tiếp trong kho đề tài.";
                return RedirectToAction(nameof(ThesisManagement));
            }

            topic.IsApproved = true;
            topic.IsRegistrationOpen = false;
            topic.Status = TopicStatus.Available;

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.LecturerId))
                await _notificationService.SendDualNotification(topic.LecturerId,
                    "Đề tài được phê duyệt",
                    $"Đề tài \"{topic.Title}\" đã được Admin phê duyệt. Admin sẽ mở đăng ký theo lịch hệ thống.",
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
            topic.IsRegistrationOpen = false;
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

            if (topic.Registrations != null && topic.Registrations.Any())
            {
                _context.Registrations.RemoveRange(topic.Registrations);
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
                t.IsRegistrationOpen = false;
                t.Status = TopicStatus.Available;

                if (!string.IsNullOrEmpty(t.LecturerId))
                {
                    await _notificationService.SendDualNotification(
                        t.LecturerId,
                        "Đề tài được phê duyệt",
                        $"Đề tài \"{t.Title}\" đã được Admin phê duyệt. Admin sẽ mở đăng ký theo lịch hệ thống.",
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
                topic.IsRegistrationOpen = false;
                topic.Status = TopicStatus.Available;

                if (!string.IsNullOrEmpty(topic.LecturerId))
                {
                    await _notificationService.SendDualNotification(
                        topic.LecturerId,
                        "Đề tài được phê duyệt",
                        $"Đề tài \"{topic.Title}\" đã được Admin phê duyệt. Admin sẽ mở đăng ký theo lịch hệ thống.",
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
                topic.IsRegistrationOpen = false;
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
            string? search = null, string? role = null, int page = 1,
            string? semester = null, string? year = null,
            string? faculty = null, int? majorId = null)
        {
            int pageSize = 10;
            RegistrationPeriod? selectedStudentPeriod = null;

            IList<ApplicationUser> usersInRole = string.IsNullOrEmpty(role)
                ? await _userManager.Users.ToListAsync()
                : await _userManager.GetUsersInRoleAsync(role);

            if (role == "Student")
            {
                selectedStudentPeriod = await GetAdminSelectedPeriodAsync(semester, year);
                if (selectedStudentPeriod == null)
                {
                    usersInRole = new List<ApplicationUser>();
                }
                else
                {
                    var eligibleStudentIds = await _context.PeriodStudents
                        .Where(ps =>
                            ps.RegistrationPeriodId == selectedStudentPeriod.Id &&
                            (ps.IsEligible || ps.Student.HasCompletedThesis))
                        .Select(ps => ps.StudentId)
                        .ToListAsync();
                    var eligibleSet = eligibleStudentIds.ToHashSet();
                    usersInRole = usersInRole
                        .Where(u => eligibleSet.Contains(u.Id))
                        .ToList();
                }
            }

            var query = usersInRole.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.UserCode != null && u.UserCode.Contains(search, StringComparison.OrdinalIgnoreCase)));

            var filteredUsers = query.ToList();

            if (!string.IsNullOrWhiteSpace(faculty) || majorId.HasValue)
            {
                var filteredUserIds = filteredUsers.Select(u => u.Id).ToList();
                var userMajorRows = await _context.UserMajors
                    .Include(um => um.Major)
                    .Where(um => filteredUserIds.Contains(um.UserId))
                    .ToListAsync();

                var matchedUserIds = userMajorRows
                    .Where(um =>
                        (!majorId.HasValue || um.MajorId == majorId.Value) &&
                        (string.IsNullOrWhiteSpace(faculty)
                            || string.Equals(um.Major.FacultyName, faculty.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .Select(um => um.UserId)
                    .ToHashSet();

                filteredUsers = filteredUsers
                    .Where(u => matchedUserIds.Contains(u.Id)
                        || (!majorId.HasValue
                            && !string.IsNullOrWhiteSpace(faculty)
                            && string.Equals(u.Faculty, faculty.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var totalItems = filteredUsers.Count;
            var users = filteredUsers
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
            ViewBag.FacultyFilter = faculty;
            ViewBag.MajorFilter = majorId;
            ViewBag.SelectedPeriodName = selectedStudentPeriod?.Name ?? GetAdminSelectedPeriodName();
            var majors = await _context.Majors
                .Where(m => m.IsActive)
                .OrderBy(m => m.FacultyName)
                .ThenBy(m => m.Name)
                .ToListAsync();
            ViewBag.Majors = majors;
            ViewBag.Faculties = majors
                .Select(m => m.FacultyName)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f)
                .ToList();

            var pageUserIds = users.Select(u => u.Id).ToList();
            ViewBag.UserMajorNames = await _context.UserMajors
                .Include(um => um.Major)
                .Where(um => pageUserIds.Contains(um.UserId))
                .GroupBy(um => um.UserId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => string.Join(", ", g.Select(um => um.Major.Name).Distinct()));

            // Thống kê theo đúng tập đang lọc để số liệu trên trang không lệch với bảng.
            var statsUsers = filteredUsers.ToList();
            ViewBag.NewUsers = statsUsers.Count(u => !u.EmailConfirmed);
            var lecturerList = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.VerifiedLecturers = role == "Lecturer"
                ? statsUsers.Count(u => u.EmailConfirmed)
                : lecturerList.Count(u => u.EmailConfirmed);
            ViewBag.CompletedStudents = role == "Student"
                ? statsUsers.Count(u => u.HasCompletedThesis)
                : 0;
            ViewBag.LockedAccounts = statsUsers.Count(u =>
                u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now);

            return View("UserManagement", users);
        }

        // Khóa / Mở khóa tài khoản
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLockUser(
            string userId, string? role,
            string? search = null, int page = 1,
            string? faculty = null, int? majorId = null)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["Error"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(UserManagement), new { role, search, page, faculty, majorId });
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng!";
                return RedirectToAction(nameof(UserManagement), new { role, search, page, faculty, majorId });
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

            return RedirectToAction(nameof(UserManagement), new { role, search, page, faculty, majorId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleThesisCompletion(
            string userId, string? role,
            string? search = null, int page = 1,
            string? faculty = null, int? majorId = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy sinh viên.";
                return RedirectToAction(nameof(UserManagement), new { role = role ?? "Student", search, page, faculty, majorId });
            }

            if (!await _userManager.IsInRoleAsync(user, "Student"))
            {
                TempData["Error"] = "Chỉ sinh viên mới có trạng thái hoàn thành KLTN.";
                return RedirectToAction(nameof(UserManagement), new { role, search, page, faculty, majorId });
            }

            user.HasCompletedThesis = !user.HasCompletedThesis;
            user.ThesisCompletedAt = user.HasCompletedThesis ? DateTime.Now : null;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded && user.HasCompletedThesis)
            {
                var periodStudents = await _context.PeriodStudents
                    .Where(ps => ps.StudentId == user.Id && ps.IsEligible)
                    .ToListAsync();

                foreach (var periodStudent in periodStudents)
                {
                    periodStudent.IsEligible = false;
                }

                await _context.SaveChangesAsync();
            }

            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? user.HasCompletedThesis
                    ? $"Đã đánh dấu {user.FullName ?? user.UserName} hoàn thành KLTN. Sinh viên này đã bị loại khỏi danh sách đủ điều kiện và không được tham gia các đợt sau."
                    : $"Đã mở lại quyền tham gia KLTN cho {user.FullName ?? user.UserName}."
                : "Không thể cập nhật trạng thái hoàn thành KLTN.";

            return RedirectToAction(nameof(UserManagement), new { role = role ?? "Student", search, page, faculty, majorId });
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
        public async Task<IActionResult> StudentManagement(string? search, int page = 1, string? semester = null, string? year = null, string? faculty = null, int? majorId = null)
            => await UserManagement(search, "Student", page, semester, year, faculty, majorId);

        // Quản lý Giảng viên
        public async Task<IActionResult> LecturerManagement(string? search, int page = 1, string? semester = null, string? year = null, string? faculty = null, int? majorId = null)
            => await UserManagement(search, "Lecturer", page, semester, year, faculty, majorId);

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

            RegistrationPeriod? targetStudentPeriod = null;
            if (role == "Student")
            {
                targetStudentPeriod = await GetAdminSelectedPeriodAsync();
                if (targetStudentPeriod == null)
                {
                    TempData["Error"] = "Chưa có đợt đăng ký tương ứng. Vui lòng tạo/chọn đợt ở mục Cài đặt trước khi thêm sinh viên.";
                    return RedirectToAction(nameof(StudentManagement));
                }
            }

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
                if (role == "Student")
                {
                    await AddStudentToPeriodAsync(user.Id, targetStudentPeriod!.Id);
                }
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
            var selectedPeriod = await GetAdminSelectedPeriodAsync();
            if (selectedPeriod == null)
            {
                TempData["Error"] = "Chưa có đợt đăng ký tương ứng. Vui lòng tạo/chọn đợt ở mục Cài đặt trước khi import đề tài.";
                return RedirectToAction(nameof(ThesisManagement));
            }

            if (!IsValidExcelUpload(file, out var topicImportError))
            {
                TempData["Error"] = topicImportError;
                return RedirectToAction(nameof(ThesisManagement));
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            int success = 0, skipped = 0;
            var skippedReasons = new List<string>();

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
                    colLecturerEmail = -1, colFaculty = -1,
                    colMajorCode = -1, colMajorName = -1, colMaxStudents = -1,
                    colLevel = -1, colCategory = -1;

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var colName = header[j]?.ToString()?.Trim().ToLower();
                    if (string.IsNullOrEmpty(colName)) continue;

                    if (colName.Contains("majorcode") || colName.Contains("mã ngành") || colName.Contains("ma nganh")) colMajorCode = j;
                    else if (colName.Contains("majorname") || colName.Contains("chuyên ngành") || colName.Contains("chuyen nganh")) colMajorName = j;
                    else if (colName.Contains("topiccode") || colName.Contains("mã đề tài") || colName.Contains("ma de tai")) colCode = j;
                    else if (colName.Contains("title") || colName.Contains("tên đề tài") || colName.Contains("ten de tai")) colTitle = j;
                    else if (colName.Contains("description") || colName.Contains("mô")) colDesc = j;
                    else if (colName.Contains("email")) colLecturerEmail = j;
                    else if (colName.Contains("maxstudents") || colName.Contains("số sv") || colName.Contains("so sv")) colMaxStudents = j;
                    else if (colName.Contains("level") || colName.Contains("độ khó") || colName.Contains("do kho")) colLevel = j;
                    else if (colName.Contains("category") || colName.Contains("loại") || colName.Contains("loai")) colCategory = j;
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
                    var lecturerEmail = colLecturerEmail != -1 ? row[colLecturerEmail]?.ToString()?.Trim() : "";
                    var faculty = colFaculty != -1 ? row[colFaculty]?.ToString()?.Trim() : "";
                    var majorCode = colMajorCode != -1 ? row[colMajorCode]?.ToString()?.Trim() : "";
                    var majorName = colMajorName != -1 ? row[colMajorName]?.ToString()?.Trim() : "";
                    var category = colCategory != -1 ? row[colCategory]?.ToString()?.Trim() : "";
                    var levelText = colLevel != -1 ? row[colLevel]?.ToString()?.Trim() : "";

                    if (string.IsNullOrEmpty(title))
                    {
                        skipped++;
                        skippedReasons.Add($"Dòng {i + 1}: thiếu Title.");
                        continue;
                    }

                    var exists = await _context.Topics.AnyAsync(t => t.TopicCode == code);
                    if (exists)
                    {
                        skipped++;
                        skippedReasons.Add($"Dòng {i + 1} ({code}): mã đề tài đã tồn tại.");
                        continue;
                    }

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
                        var canCreateMajor = !string.IsNullOrWhiteSpace(majorCode)
                            || !string.IsNullOrWhiteSpace(majorName)
                            || !string.IsNullOrWhiteSpace(faculty);

                        if (!canCreateMajor)
                        {
                            skipped++;
                            skippedReasons.Add($"Dòng {i + 1} ({code}): thiếu thông tin ngành/khoa.");
                            continue;
                        }

                        var normalizedMajorCode = string.IsNullOrWhiteSpace(majorCode)
                            ? null
                            : majorCode.Trim().ToUpperInvariant();
                        var importMajorName = !string.IsNullOrWhiteSpace(majorName)
                            ? majorName.Trim()
                            : (!string.IsNullOrWhiteSpace(faculty) ? faculty.Trim() : normalizedMajorCode!);

                        major = new Major
                        {
                            MajorCode = normalizedMajorCode,
                            Name = importMajorName,
                            FacultyName = !string.IsNullOrWhiteSpace(faculty)
                                ? faculty.Trim()
                                : (!string.IsNullOrWhiteSpace(majorName) ? majorName.Trim() : null),
                            IsActive = true
                        };

                        _context.Majors.Add(major);
                        await _context.SaveChangesAsync();
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

                    var topic = new Topic
                    {
                        Title = title,
                        TopicCode = code,
                        Description = desc ?? "",
                        Semester = selectedPeriod.Name,
                        RegistrationPeriodId = selectedPeriod.Id,
                        Faculty = major.FacultyName ?? faculty,
                        MajorId = major.Id,
                        DepartmentName = major.Name,
                        Category = string.IsNullOrWhiteSpace(category) ? (maxStudents > 1 ? "Nhóm" : "Cá nhân") : category,
                        Level = level,
                        Deadline = selectedPeriod.RegistrationCloseAt,
                        CreatedAt = DateTime.Now,
                        IsApproved = true,
                        IsRegistrationOpen = false,
                        Status = TopicStatus.Available,
                        MaxStudents = maxStudents
                    };

                    if (!string.IsNullOrEmpty(lecturerEmail))
                    {
                        var lecturer = await _userManager.FindByEmailAsync(lecturerEmail);
                        if (lecturer != null)
                            topic.LecturerId = lecturer.Id;
                        else
                        {
                            skipped++;
                            skippedReasons.Add($"Dòng {i + 1} ({code}): không tìm thấy giảng viên email '{lecturerEmail}'.");
                            continue;
                        }
                    }

                    _context.Topics.Add(topic);
                    success++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Import thành công: {success}, Bỏ qua: {skipped}";
            if (skippedReasons.Any())
            {
                TempData["Error"] = string.Join(" | ", skippedReasons.Take(5))
                    + (skippedReasons.Count > 5 ? $" | ... và {skippedReasons.Count - 5} dòng khác." : "");
            }
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
            ViewBag.SelectedPeriodName = GetAdminSelectedPeriodName();
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

            RegistrationPeriod? targetStudentPeriod = null;
            if (role == "Student")
            {
                targetStudentPeriod = await GetAdminSelectedPeriodAsync();
                if (targetStudentPeriod == null)
                {
                    TempData["Error"] = "Chưa có đợt đăng ký tương ứng. Vui lòng tạo/chọn đợt ở mục Cài đặt trước khi import sinh viên.";
                    return RedirectToAction(nameof(StudentManagement));
                }
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

                    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrEmpty(email) || !email.Contains("@"))
                    {
                        skipped++;
                        continue;
                    }

                    var existingUser = await _userManager.FindByEmailAsync(email);
                    if (existingUser != null)
                    {
                        if (role == "Student" && await _userManager.IsInRoleAsync(existingUser, "Student"))
                        {
                            if (await AddStudentToPeriodAsync(existingUser.Id, targetStudentPeriod!.Id))
                            {
                                success++;
                            }
                            else
                            {
                                skipped++;
                            }
                        }
                        else
                        {
                            skipped++;
                        }

                        continue;
                    }

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
                        if (role == "Student")
                        {
                            await AddStudentToPeriodAsync(user.Id, targetStudentPeriod!.Id);
                        }
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

            if (role == "Student")
            {
                var selectedPeriod = await GetAdminSelectedPeriodAsync();
                if (selectedPeriod == null)
                {
                    users = new List<ApplicationUser>();
                }
                else
                {
                    var eligibleStudentIds = await _context.PeriodStudents
                        .Where(ps =>
                            ps.RegistrationPeriodId == selectedPeriod.Id &&
                            ps.IsEligible &&
                            !ps.Student.HasCompletedThesis)
                        .Select(ps => ps.StudentId)
                        .ToListAsync();
                    var eligibleSet = eligibleStudentIds.ToHashSet();
                    users = users.Where(u => eligibleSet.Contains(u.Id)).ToList();
                }
            }

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

        private void SetAdminSelectedPeriod(string periodName)
        {
            HttpContext.Session.SetString(AdminSelectedPeriodSessionKey, periodName);
            ViewBag.AdminSelectedPeriodName = periodName;

            var parts = periodName.Split('-', 2);
            if (parts.Length == 2)
            {
                ViewBag.AdminSelectedSemester = parts[0];
                ViewBag.AdminSelectedYear = parts[1];
            }
        }

        private string? GetAdminSelectedPeriodName()
        {
            return HttpContext.Session.GetString(AdminSelectedPeriodSessionKey);
        }

        private async Task<RegistrationPeriod?> GetAdminSelectedPeriodAsync(string? semester = null, string? year = null)
        {
            if (!string.IsNullOrWhiteSpace(semester) && !string.IsNullOrWhiteSpace(year))
            {
                SetAdminSelectedPeriod($"{semester.Trim()}-{year.Trim()}");
            }

            var selectedPeriodName = GetAdminSelectedPeriodName();
            if (!string.IsNullOrWhiteSpace(selectedPeriodName))
            {
                return await _context.RegistrationPeriods
                    .FirstOrDefaultAsync(p => p.Name == selectedPeriodName);
            }

            return await GetOrCreateActiveRegistrationPeriodAsync();
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
            var selectedPeriod = await GetAdminSelectedPeriodAsync();
            if (selectedPeriod == null)
            {
                return Json(new { success = false, message = "Chưa có đợt đăng ký tương ứng. Vui lòng tạo/chọn đợt ở mục Cài đặt trước khi tạo đề tài." });
            }

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
            topic.Semester = selectedPeriod.Name;
            topic.RegistrationPeriodId = selectedPeriod.Id;
            topic.IsStudentProposed = false;
            topic.IsApproved = true;
            topic.IsRegistrationOpen = false;
            topic.Status = TopicStatus.Available;

            if (topic.MajorId.HasValue)
            {
                var major = await _context.Majors.FirstOrDefaultAsync(m => m.Id == topic.MajorId.Value && m.IsActive);
                if (major == null)
                {
                    return Json(new { success = false, message = "Chuyên ngành không hợp lệ." });
                }

                topic.Faculty = major.FacultyName;
                topic.DepartmentName = major.Name;
            }
            else
            {
                return Json(new { success = false, message = "Vui lòng chọn chuyên ngành cho đề tài." });
            }

            if (!string.IsNullOrWhiteSpace(topic.LecturerId))
            {
                var lecturer = await _userManager.FindByIdAsync(topic.LecturerId);
                if (lecturer == null || !await _userManager.IsInRoleAsync(lecturer, "Lecturer"))
                {
                    return Json(new { success = false, message = "Giảng viên hướng dẫn không hợp lệ." });
                }
            }

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
            var years = await _context.RegistrationPeriods
                .Select(p => p.AcademicYear)
                .ToListAsync();

            var parsedYears = years
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

        private async Task<List<string>> GetSemesterOptions()
        {
            var semesters = await _context.RegistrationPeriods
                .Select(p => p.SemesterCode)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            if (!semesters.Contains("HK1")) semesters.Insert(0, "HK1");
            if (!semesters.Contains("HK2")) semesters.Add("HK2");

            return semesters
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
        }

        private new static string GetCurrentAcademicYear()
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

        private async Task DeactivateAllRegistrationPeriodsAsync()
        {
            var activePeriods = await _context.RegistrationPeriods
                .Where(p => p.IsActive)
                .ToListAsync();

            foreach (var period in activePeriods)
            {
                period.IsActive = false;
            }
        }

        private async Task SyncSettingsFromPeriodAsync(RegistrationPeriod period)
        {
            var values = new Dictionary<string, string>
            {
                ["Semester_Start"] = period.SemesterStart.ToString("yyyy-MM-dd"),
                ["Semester_End"] = period.SemesterEnd.ToString("yyyy-MM-dd"),
                ["Registration_Start"] = period.RegistrationOpenAt.ToString("yyyy-MM-ddTHH:mm"),
                ["Registration_End"] = period.RegistrationCloseAt.ToString("yyyy-MM-ddTHH:mm")
            };

            foreach (var (name, value) in values)
            {
                var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Name == name);
                if (setting == null)
                {
                    _context.Settings.Add(new Setting { Name = name, Value = value });
                }
                else
                {
                    setting.Value = value;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<bool> AddStudentToPeriodAsync(string studentId, int registrationPeriodId)
        {
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null || student.HasCompletedThesis)
            {
                return false;
            }

            var existing = await _context.PeriodStudents.FirstOrDefaultAsync(ps =>
                ps.StudentId == studentId &&
                ps.RegistrationPeriodId == registrationPeriodId);

            if (existing != null)
            {
                existing.IsEligible = true;
                existing.ImportedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }

            _context.PeriodStudents.Add(new PeriodStudent
            {
                StudentId = studentId,
                RegistrationPeriodId = registrationPeriodId,
                ImportedAt = DateTime.Now,
                IsEligible = true
            });

            await _context.SaveChangesAsync();
            return true;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Settings()
        {
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            var settings = await _context.Settings.ToListAsync();
            var periodTopics = FilterTopicsByActivePeriod(_context.Topics.AsQueryable(), activePeriod);
            ViewBag.ActivePeriod = activePeriod;
            ViewBag.EligibleStudentCount = await _context.PeriodStudents
                .CountAsync(ps => ps.RegistrationPeriodId == activePeriod.Id
                    && ps.IsEligible
                    && !ps.Student.HasCompletedThesis);
            ViewBag.RegistrationPeriods = await _context.RegistrationPeriods
                .OrderByDescending(p => p.AcademicYear)
                .ThenByDescending(p => p.Id)
                .ToListAsync();
            ViewBag.TopicTotal = await periodTopics.CountAsync(t => t.IsApproved);
            ViewBag.TopicRegistrationOpen = await periodTopics.CountAsync(t => t.IsApproved && t.IsRegistrationOpen);
            ViewBag.TopicRegistrationReady = await periodTopics.CountAsync(t => t.IsApproved && !t.IsRegistrationOpen && t.Status == TopicStatus.Available);
            ViewBag.Majors = await _context.Majors
                .OrderBy(m => m.FacultyName)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View(settings);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRegistrationPeriod(
            string academicYear,
            string semesterCode,
            DateTime semesterStart,
            DateTime semesterEnd,
            DateTime registrationOpenAt,
            DateTime registrationCloseAt,
            bool setActive = true)
        {
            academicYear = academicYear?.Trim() ?? string.Empty;
            semesterCode = string.IsNullOrWhiteSpace(semesterCode) ? "HK2" : semesterCode.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(academicYear))
            {
                TempData["Error"] = "Năm học không được để trống.";
                return RedirectToAction(nameof(Settings));
            }

            if (semesterStart > semesterEnd)
            {
                TempData["Error"] = "Ngày bắt đầu học kỳ không được sau ngày kết thúc học kỳ.";
                return RedirectToAction(nameof(Settings));
            }

            if (registrationOpenAt > registrationCloseAt)
            {
                TempData["Error"] = "Thời gian mở đăng ký không được sau thời gian đóng đăng ký.";
                return RedirectToAction(nameof(Settings));
            }

            var name = $"{semesterCode}-{academicYear}";
            if (await _context.RegistrationPeriods.AnyAsync(p => p.Name == name))
            {
                TempData["Error"] = $"Đợt đăng ký {name} đã tồn tại.";
                return RedirectToAction(nameof(Settings));
            }

            if (setActive)
            {
                await DeactivateAllRegistrationPeriodsAsync();
            }

            var period = new RegistrationPeriod
            {
                Name = name,
                AcademicYear = academicYear,
                SemesterCode = semesterCode,
                SemesterStart = semesterStart,
                SemesterEnd = semesterEnd,
                RegistrationOpenAt = registrationOpenAt,
                RegistrationCloseAt = registrationCloseAt,
                IsActive = setActive
            };

            _context.RegistrationPeriods.Add(period);
            await _context.SaveChangesAsync();

            if (setActive)
            {
                await SyncSettingsFromPeriodAsync(period);
                SetAdminSelectedPeriod(period.Name);
            }

            TempData["Success"] = $"Đã tạo đợt đăng ký {name}.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActiveRegistrationPeriod(int id)
        {
            var period = await _context.RegistrationPeriods.FindAsync(id);
            if (period == null) return NotFound();

            await DeactivateAllRegistrationPeriodsAsync();
            period.IsActive = true;
            await _context.SaveChangesAsync();

            await SyncSettingsFromPeriodAsync(period);
            SetAdminSelectedPeriod(period.Name);

            TempData["Success"] = $"Đã chuyển sang đợt đăng ký {period.Name}.";
            return RedirectToAction(nameof(Settings));
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
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

            var oldTopicRegistrationValue = await _context.Settings
                .Where(s => s.Name == "IsTopicRegistrationOpen")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

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

            if (postedSettings.TryGetValue("Semester_Start", out semesterStartValue)
                && DateTime.TryParse(semesterStartValue, out var parsedSemesterStart))
            {
                activePeriod.SemesterStart = parsedSemesterStart;
            }

            if (postedSettings.TryGetValue("Semester_End", out semesterEndValue)
                && DateTime.TryParse(semesterEndValue, out var parsedSemesterEnd))
            {
                activePeriod.SemesterEnd = parsedSemesterEnd;
            }

            if (postedSettings.TryGetValue("Registration_Start", out registrationStartValue)
                && DateTime.TryParse(registrationStartValue, out var parsedRegistrationStart))
            {
                activePeriod.RegistrationOpenAt = parsedRegistrationStart;
            }

            if (postedSettings.TryGetValue("Registration_End", out registrationEndValue)
                && DateTime.TryParse(registrationEndValue, out var parsedRegistrationEnd))
            {
                activePeriod.RegistrationCloseAt = parsedRegistrationEnd;
            }

            int? updatedTopicCount = null;
            if (postedSettings.TryGetValue("IsTopicRegistrationOpen", out var isTopicRegistrationOpenValue)
                && bool.TryParse(isTopicRegistrationOpenValue, out var isTopicRegistrationOpen))
            {
                var previousTopicRegistrationOpen = bool.TryParse(oldTopicRegistrationValue, out var oldValue) && oldValue;
                if (previousTopicRegistrationOpen != isTopicRegistrationOpen)
                {
                    updatedTopicCount = await ApplyTopicRegistrationStateAsync(isTopicRegistrationOpen, activePeriod.Id);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = updatedTopicCount.HasValue
                ? $"Cấu hình hệ thống đã được cập nhật! Đã {(postedSettings["IsTopicRegistrationOpen"] == "true" ? "mở" : "đóng")} đăng ký cho {updatedTopicCount.Value} đề tài."
                : "Cấu hình hệ thống đã được cập nhật!";
            return RedirectToAction(nameof(Settings));
        }

        private async Task<int> ApplyTopicRegistrationStateAsync(bool isOpen, int? registrationPeriodId = null)
        {
            var topics = await _context.Topics
                .Include(t => t.Registrations)
                .Where(t => t.IsApproved
                    && t.Status != TopicStatus.Rejected
                    && (!registrationPeriodId.HasValue || t.RegistrationPeriodId == registrationPeriodId.Value))
                .ToListAsync();

            var updated = 0;
            foreach (var topic in topics)
            {
                var reservedCount = topic.Registrations?.Count(r => r.Status == "Pending" || r.Status == "Approved") ?? 0;
                if (reservedCount >= topic.MaxStudents)
                {
                    if (topic.IsRegistrationOpen || topic.Status != TopicStatus.Full)
                    {
                        topic.IsRegistrationOpen = false;
                        topic.Status = TopicStatus.Full;
                        updated++;
                    }

                    continue;
                }

                if (isOpen)
                {
                    if (!topic.IsRegistrationOpen || topic.Status != TopicStatus.Available)
                    {
                        topic.IsRegistrationOpen = true;
                        topic.Status = TopicStatus.Available;
                        updated++;
                    }
                }
                else if (topic.IsRegistrationOpen || topic.Status == TopicStatus.Available)
                {
                    topic.IsRegistrationOpen = false;
                    topic.Status = TopicStatus.Closed;
                    updated++;
                }
            }

            return updated;
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
                .Where(t => (t.IsStudentProposed || t.CreatedByStudentId != null)
                    && (t.IsApproved
                        || t.Status == TopicStatus.Rejected
                        || t.LecturerId == null
                        || t.Note!.StartsWith(LecturerApprovedProposalPrefix)
                        || t.Note!.StartsWith(LecturerRejectedProposalPrefix)))
                .AsQueryable();

            if (status == "pending") query = query.Where(t => t.Status == TopicStatus.Pending && !t.IsApproved);
            else if (status == "approved") query = query.Where(t => t.IsApproved);
            else if (status == "rejected") query = query.Where(t => t.Status == TopicStatus.Rejected);

            var topics = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.PendingCount = await _context.Topics.CountAsync(t => (t.IsStudentProposed || t.CreatedByStudentId != null)
                && t.Status == TopicStatus.Pending
                && !t.IsApproved
                && (t.LecturerId == null
                    || t.Note!.StartsWith(LecturerApprovedProposalPrefix)
                    || t.Note!.StartsWith(LecturerRejectedProposalPrefix)));
            ViewBag.ApprovedCount = await _context.Topics.CountAsync(t => (t.IsStudentProposed || t.CreatedByStudentId != null) && t.IsApproved);
            ViewBag.RejectedCount = await _context.Topics.CountAsync(t => (t.IsStudentProposed || t.CreatedByStudentId != null) && t.Status == TopicStatus.Rejected);
            ViewBag.StatusFilter = status;

            // Danh sách giảng viên để phân công, kèm chuyên ngành/khoa để lọc theo đề xuất.
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            var lecturerIds = lecturers.Select(l => l.Id).ToList();
            ViewBag.Lecturers = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .Where(u => lecturerIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(topics);
        }

        // POST: Duyệt đề xuất sinh viên (và phân công GV)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStudentProposal(int topicId, string? assignLecturerId, string? status = null)
        {
            var topic = await _context.Topics
                .Include(t => t.Student)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            bool lecturerApproved = !string.IsNullOrWhiteSpace(topic.Note)
                && topic.Note.StartsWith(LecturerApprovedProposalPrefix);
            if (!lecturerApproved)
            {
                TempData["Error"] = "Admin chỉ thêm vào hệ thống sau khi giảng viên đồng ý hướng dẫn. Nếu chưa có hoặc giảng viên từ chối, hãy phân công giảng viên khác.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            if (!string.IsNullOrEmpty(assignLecturerId))
            {
                var lecturer = await _userManager.FindByIdAsync(assignLecturerId);
                if (lecturer == null || !await _userManager.IsInRoleAsync(lecturer, "Lecturer"))
                {
                    TempData["Error"] = "Giảng viên phân công không hợp lệ.";
                    return RedirectToAction(nameof(StudentProposals), new { status });
                }

                topic.LecturerId = assignLecturerId;
            }

            if (string.IsNullOrWhiteSpace(topic.LecturerId))
            {
                TempData["Error"] = "Vui lòng phân công giảng viên trước khi thêm đề xuất vào hệ thống.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            topic.IsApproved = true;
            topic.IsRegistrationOpen = false;
            topic.Status = TopicStatus.Full;
            topic.IsStudentProposed = false;
            topic.MaxStudents = 1;
            topic.Note = "Admin đã thêm đề xuất vào hệ thống.";

            if (!string.IsNullOrWhiteSpace(topic.CreatedByStudentId)
                && !await _context.Registrations.AnyAsync(r =>
                    r.TopicId == topic.Id &&
                    r.StudentId == topic.CreatedByStudentId &&
                    r.Status == "Approved"))
            {
                _context.Registrations.Add(new Registration
                {
                    StudentId = topic.CreatedByStudentId,
                    TopicId = topic.Id,
                    RegistrationPeriodId = topic.RegistrationPeriodId,
                    Status = "Approved",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Priority = 1,
                    ApprovedBy = _userManager.GetUserId(User)
                });
            }

            await _context.SaveChangesAsync();

            // Thông báo cho sinh viên đề xuất
            if (!string.IsNullOrEmpty(topic.CreatedByStudentId))
                await _notificationService.SendDualNotification(
                    topic.CreatedByStudentId,
                    "Đề xuất đề tài được duyệt! 🎉",
                    $"Đề tài \"{topic.Title}\" của bạn đã được Admin phê duyệt và công khai.",
                    "TopicApproved",
                    relatedId: topic.Id,
                    redirectUrl: "/Student/MyRegistration");

            // Thông báo cho GV được phân công
            if (!string.IsNullOrEmpty(assignLecturerId))
                await _notificationService.SendDualNotification(
                    assignLecturerId,
                    "Được phân công hướng dẫn đề tài đề xuất",
                    $"Admin đã phân công bạn hướng dẫn đề tài đề xuất \"{topic.Title}\".",
                    "TopicApproved",
                    relatedId: topic.Id,
                    redirectUrl: "/Lecturer/Topics");

            TempData["Success"] = $"Đã duyệt đề xuất \"{topic.Title}\"!";
            return RedirectToAction(nameof(StudentProposals), new { status = "approved" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReassignStudentProposal(int topicId, string assignLecturerId, string? status = null)
        {
            var topic = await _context.Topics
                .Include(t => t.Student)
                .Include(t => t.Major)
                .FirstOrDefaultAsync(t => t.Id == topicId && t.IsStudentProposed);

            if (topic == null) return NotFound();

            if (string.IsNullOrWhiteSpace(assignLecturerId))
            {
                TempData["Error"] = "Vui lòng chọn giảng viên cần phân công.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            if (topic.LecturerId == assignLecturerId)
            {
                TempData["Error"] = "Vui lòng chọn giảng viên khác với giảng viên hiện tại.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            var lecturer = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .FirstOrDefaultAsync(u => u.Id == assignLecturerId);
            if (lecturer == null || !await _userManager.IsInRoleAsync(lecturer, "Lecturer"))
            {
                TempData["Error"] = "Giảng viên phân công không hợp lệ.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            if (!IsLecturerMatchedTopicMajor(lecturer, topic))
            {
                TempData["Error"] = "Giảng viên được phân công phải cùng chuyên ngành hoặc cùng khoa với đề xuất.";
                return RedirectToAction(nameof(StudentProposals), new { status });
            }

            topic.LecturerId = assignLecturerId;
            topic.Note = null;
            topic.IsApproved = false;
            topic.IsRegistrationOpen = false;
            topic.Status = TopicStatus.Pending;
            await _context.SaveChangesAsync();

            await _notificationService.SendDualNotification(
                assignLecturerId,
                "Đề xuất cần bạn xem xét hướng dẫn",
                $"Admin đã phân công bạn xem xét đề tài sinh viên đề xuất: \"{topic.Title}\".",
                "NewTopic",
                relatedId: topic.Id,
                redirectUrl: "/Lecturer/Approval?tab=proposals");

            if (!string.IsNullOrEmpty(topic.CreatedByStudentId))
            {
                await _notificationService.SendDualNotification(
                    topic.CreatedByStudentId,
                    "Admin đã phân công giảng viên khác",
                    $"Đề tài \"{topic.Title}\" đã được chuyển cho giảng viên khác xem xét.",
                    "NewTopic",
                    relatedId: topic.Id,
                    redirectUrl: "/Student/MyRegistration");
            }

            TempData["Success"] = $"Đã chuyển đề xuất \"{topic.Title}\" cho giảng viên mới xem xét.";
            return RedirectToAction(nameof(StudentProposals), new { status = "pending" });
        }

        // POST: Từ chối đề xuất sinh viên
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStudentProposal(int topicId, string? reason, string? status = null)
        {
            var topic = await _context.Topics
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            topic.IsApproved = false;
            topic.IsRegistrationOpen = false;
            topic.Status = TopicStatus.Rejected;
            topic.Note = string.IsNullOrWhiteSpace(reason)
                ? "Admin đã từ chối đề xuất."
                : reason.Trim();
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.CreatedByStudentId))
                await _notificationService.SendDualNotification(
                    topic.CreatedByStudentId,
                    "Đề xuất đề tài bị từ chối",
                    $"Đề tài \"{topic.Title}\" của bạn đã bị từ chối." + (string.IsNullOrEmpty(reason) ? "" : $" Lý do: {reason}"),
                    "TopicRejected",
                    relatedId: topic.Id,
                    redirectUrl: "/Student/ProposeTopic");

            TempData["Error"] = $"Đã từ chối đề xuất \"{topic.Title}\".";
            return RedirectToAction(nameof(StudentProposals), new { status = "rejected" });
        }

        // ============================================================
        // SỬA ĐỀ TÀI (Admin)  →  GET + POST /Admin/EditTopic/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> EditTopic(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.Major)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = new SelectList(
                lecturers.OrderBy(l => l.FullName),
                "Id", "FullName", topic.LecturerId);
            ViewBag.Majors = new SelectList(
                await _context.Majors.Where(m => m.IsActive).ToListAsync(),
                "Id", "Name", topic.MajorId);
            ViewBag.ApprovedCount = topic.Registrations?.Count(r => r.Status == "Approved") ?? 0;
            ViewBag.PendingCount = topic.Registrations?.Count(r => r.Status == "Pending") ?? 0;

            return View(topic);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("EditTopic")]
        public async Task<IActionResult> EditTopicPost(Topic model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == model.Id);

            if (topic == null)
                return isAjax
                    ? Json(new { success = false, message = "Không tìm thấy đề tài." })
                    : NotFound();

            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");
            ModelState.Remove("Registrations");

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    var message = string.Join(" | ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .Where(e => !string.IsNullOrWhiteSpace(e)));

                    return Json(new
                    {
                        success = false,
                        message = string.IsNullOrWhiteSpace(message) ? "Dữ liệu không hợp lệ." : message
                    });
                }

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
                ViewBag.ApprovedCount = await _context.Registrations.CountAsync(r => r.TopicId == model.Id && r.Status == "Approved");
                ViewBag.PendingCount = await _context.Registrations.CountAsync(r => r.TopicId == model.Id && r.Status == "Pending");

                return View(model);
            }

            topic.Title = model.Title.Trim();
            topic.Description = model.Description?.Trim() ?? "";
            topic.LecturerId = model.LecturerId;
            topic.MajorId = model.MajorId;
            topic.Level = model.Level;
            var activeCount = await _context.Registrations
                .CountAsync(r => r.TopicId == topic.Id && (r.Status == "Pending" || r.Status == "Approved"));

            if (model.MaxStudents < activeCount)
            {
                TempData["Error"] = $"Số sinh viên tối đa không thể nhỏ hơn số đăng ký/chờ duyệt hiện tại ({activeCount}).";
                if (isAjax)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Số sinh viên tối đa không thể nhỏ hơn số đăng ký/chờ duyệt hiện tại ({activeCount})."
                    });
                }

                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
                ViewBag.Lecturers = new SelectList(lecturers.OrderBy(l => l.FullName), "Id", "FullName", model.LecturerId);
                ViewBag.Majors = new SelectList(await _context.Majors.Where(m => m.IsActive).ToListAsync(), "Id", "Name", model.MajorId);
                ViewBag.ApprovedCount = await _context.Registrations.CountAsync(r => r.TopicId == model.Id && r.Status == "Approved");
                ViewBag.PendingCount = await _context.Registrations.CountAsync(r => r.TopicId == model.Id && r.Status == "Pending");
                return View(model);
            }

            topic.MaxStudents = Math.Clamp(model.MaxStudents, 1, 10);
            topic.IsApproved = model.IsApproved;
            var selectedPeriod = await GetAdminSelectedPeriodAsync();
            topic.Semester = selectedPeriod?.Name ?? topic.Semester;
            topic.RegistrationPeriodId = selectedPeriod?.Id ?? topic.RegistrationPeriodId;
            topic.Deadline = model.Deadline;
            topic.Category = model.Category?.Trim();
            topic.Note = model.Note?.Trim();

            topic.IsRegistrationOpen = model.IsRegistrationOpen && topic.IsApproved && activeCount < topic.MaxStudents;
            topic.Status = activeCount >= topic.MaxStudents
                ? TopicStatus.Full
                : topic.IsRegistrationOpen || (topic.IsApproved && model.Status == TopicStatus.Available)
                    ? TopicStatus.Available
                    : model.Status;

            if (topic.MajorId.HasValue)
            {
                var major = await _context.Majors.FirstOrDefaultAsync(m => m.Id == topic.MajorId.Value);
                topic.Faculty = major?.FacultyName;
                topic.DepartmentName = major?.Name;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật đề tài thành công!";
            if (isAjax)
            {
                return Json(new { success = true, message = "Đã cập nhật đề tài thành công!" });
            }

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
                topic.IsRegistrationOpen = false;
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

        private static bool IsLecturerMatchedTopicMajor(ApplicationUser lecturer, Topic topic)
        {
            if (topic.MajorId.HasValue)
            {
                if (lecturer.MajorId == topic.MajorId.Value)
                    return true;

                if (lecturer.UserMajors.Any(um => um.MajorId == topic.MajorId.Value))
                    return true;
            }

            var topicFaculty = topic.Major?.FacultyName ?? topic.Faculty;
            if (string.IsNullOrWhiteSpace(topicFaculty))
                return !topic.MajorId.HasValue;

            return string.Equals(lecturer.Faculty, topicFaculty, StringComparison.OrdinalIgnoreCase)
                || string.Equals(lecturer.Major?.FacultyName, topicFaculty, StringComparison.OrdinalIgnoreCase)
                || lecturer.UserMajors.Any(um =>
                    string.Equals(um.Major?.FacultyName, topicFaculty, StringComparison.OrdinalIgnoreCase));
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
