// ============================================================
// FILE: Controllers/Lecturer/LecturerController.cs
// THAY THẾ HOÀN TOÀN file cũ
// Bổ sung so với bản gốc:
//   1. Edit (GET) → trả View đúng thay vì View("_TopicList")
//   2. ExportRegistrations → xuất Excel SV đã duyệt
//   3. Profile (GET + POST) → xem / cập nhật thông tin GV
//   4. GetTopicStats → AJAX thống kê cho dashboard
//   5. Dashboard Index → đọc schedule thực từ DB Timelines
// ============================================================
using ClosedXML.Excel;
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Security.Claims;
using KLTN_Registration_System.Models.Enums;

namespace KLTN_Registration_System.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LecturerController(AppDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ─────────────────────────────────────────────────────────────
        // DASHBOARD  →  /Lecturer/Index
        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == lid);

            var allData = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .Where(r => r.Topic.LecturerId == lid)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.TotalPending =
                allData.Count(r => r.Status == "Pending");

            ViewBag.TotalApproved =
                allData.Count(r => r.Status == "Approved");

            ViewBag.GuidingGroups =
                allData.Where(r => r.Status == "Approved").ToList();

            ViewBag.PendingRequests =
                allData.Where(r => r.Status == "Pending").ToList();

            var myTopics = await _context.Topics
                .Where(t => t.LecturerId == lid)
                .ToListAsync();

            ViewBag.TotalTopics = myTopics.Count;

            // THÊM ĐOẠN NÀY
            ViewBag.Majors = await _context.Majors
                .Where(m => m.IsActive)
                .ToListAsync();

            // Đọc schedule từ DB thay vì hardcode
            var schedules = await _context.Timelines
                .Where(t => t.IsActive && t.Date >= DateTime.Today)
                .OrderBy(t => t.Date)
                .Take(5)
                .ToListAsync();

            ViewBag.UpcomingSchedules = schedules.Select(t => new LecturerScheduleVM
            {
                Day = t.Date.ToString("ddd"),
                Date = t.Date.Day.ToString(),
                Title = t.Title,
                Time = t.Date.ToString("HH:mm"),
                Color = (t.Date - DateTime.Today).TotalDays <= 3
                    ? "red-500"
                    : "primary",

                DaysLeft = Math.Max(
                    0,
                    (t.Date - DateTime.Today).Days
                ),

                IsUrgent =
                    (t.Date - DateTime.Today).TotalDays <= 3

            }).ToList();

            ViewBag.LecturerName =
                user?.FullName
                ?? User.Identity?.Name
                ?? "Giảng viên";

            return View(allData);
        }

        // ─────────────────────────────────────────────────────────────
        // DUYỆT ĐƠN LẺ
        // ─────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();
            if (reg.Topic?.LecturerId != lid) return Forbid();

            var count = await _context.Registrations
                .CountAsync(r => r.TopicId == reg.TopicId && r.Status == "Approved");

            if (count >= reg.Topic!.MaxStudents)
            {
                TempData["Error"] = "Đề tài đã đủ sinh viên!";
                return RedirectToAction(nameof(Index));
            }

            reg.Status = "Approved";
            reg.ApprovedBy = lid;
            reg.UpdatedAt = DateTime.Now;

            // Từ chối các Pending khác của cùng SV
            var otherPending = await _context.Registrations
                .Where(r => r.StudentId == reg.StudentId && r.Id != reg.Id && r.Status == "Pending")
                .ToListAsync();
            foreach (var o in otherPending) { o.Status = "Rejected"; o.UpdatedAt = DateTime.Now; }

            if (count + 1 >= reg.Topic.MaxStudents)
            {
                reg.Topic.Status = TopicStatus.Full;
                reg.Topic.IsRegistrationOpen = false;
            }

            await _context.SaveChangesAsync();
            await Notify(reg.StudentId, "Đề tài được duyệt",
                $"Đề tài \"{reg.Topic?.Title}\" đã được phê duyệt.",
                "TopicApproved", "/Student/MyRegistration");

            TempData["Success"] = "Đã duyệt thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? feedback)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();
            if (reg.Topic?.LecturerId != lid) return Forbid();

            reg.Status = "Rejected";
            reg.Feedback = feedback;
            reg.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await Notify(reg.StudentId, "Yêu cầu bị từ chối",
                $"Đề tài \"{reg.Topic?.Title}\" đã bị từ chối." +
                (string.IsNullOrEmpty(feedback) ? "" : $" Lý do: {feedback}"),
                "TopicRejected", "/Topic/Index");

            TempData["Error"] = "Đã từ chối.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // DUYỆT THEO NHÓM  →  /Lecturer/Approval
        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Approval(string status = "Pending")
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var allRegs = await _context.Registrations
                .Include(r => r.Student)
                .Include(r => r.Topic)
                .Where(r => r.Topic.LecturerId == lid)
                .ToListAsync();

            // ===== THỐNG KÊ =====

            ViewBag.TotalPending = allRegs.Count(r => r.Status == "Pending");

            ViewBag.TotalApproved = allRegs.Count(r => r.Status == "Approved");

            ViewBag.TotalRegistrations = allRegs.Count;

            ViewBag.StatusFilter = status;

            // ===== SINH VIÊN ĐÃ CÓ NHÓM =====

            var groupedStudents = allRegs
                .Where(r => r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .Count();

            ViewBag.TotalStudentsGrouped = groupedStudents;

            // ===== TỈ LỆ =====

            var studentRoleId = await _context.Roles
    .Where(r => r.Name == "Student")
    .Select(r => r.Id)
    .FirstOrDefaultAsync();

            var totalStudents = await _context.UserRoles
                .CountAsync(ur => ur.RoleId == studentRoleId);
            ViewBag.GroupPercent = totalStudents == 0
                ? 0
                : (int)Math.Round((double)groupedStudents / totalStudents * 100);

            // ===== FILTER =====

            var regs = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .Where(r => r.Topic.LecturerId == lid && r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var grouped = regs
                .GroupBy(r => new { r.TopicId, r.Topic!.Title })
                .Select(g => new GroupedRegistration
                {
                    Topic = g.First().Topic,
                    TopicTitle = g.Key.Title ?? "",
                    TopicId = g.Key.TopicId,
                    Students = g.Select(x => x.Student).ToList(),
                    RegistrationIds = g.Select(x => x.Id).ToList(),
                    CreatedAt = g.Max(x => x.CreatedAt)
                })
                .ToList();

            return View(grouped);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGroup(List<int> ids)
        {
            if (ids == null || !ids.Any()) return RedirectToAction(nameof(Approval));
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var regs = await _context.Registrations
                .Include(r => r.Topic)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync();

            foreach (var reg in regs)
            {
                if (reg.Topic?.LecturerId != lid) continue;
                reg.Status = "Approved"; reg.ApprovedBy = lid; reg.UpdatedAt = DateTime.Now;
                await Notify(reg.StudentId, "Đề tài được duyệt",
                    $"Nhóm đã được duyệt đề tài: {reg.Topic?.Title}", "TopicApproved", "/Student/MyRegistration");
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã phê duyệt {regs.Count} đăng ký!";
            return RedirectToAction(nameof(Approval));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGroup(List<int> ids, string? feedback)
        {
            if (ids == null || !ids.Any()) return RedirectToAction(nameof(Approval));
            var regs = await _context.Registrations
                .Include(r => r.Topic)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync();

            foreach (var reg in regs)
            {
                reg.Status = "Rejected"; reg.Feedback = feedback; reg.UpdatedAt = DateTime.Now;
                await Notify(reg.StudentId, "Yêu cầu bị từ chối",
                    $"Đề tài: {reg.Topic?.Title}" + (string.IsNullOrEmpty(feedback) ? "" : $". Lý do: {feedback}"),
                    "TopicRejected", "/Topic/Index");
            }

            await _context.SaveChangesAsync();
            TempData["Error"] = "Đã từ chối nhóm.";
            return RedirectToAction(nameof(Approval));
        }

        // ─────────────────────────────────────────────────────────────
        // QUẢN LÝ ĐỀ TÀI  →  /Lecturer/ThesisManagement
        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> ThesisManagement(
            string? semester = null,
            string? status = null,
            int? majorId = null)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Topics
                .Include(t => t.Registrations)
                .Include(t => t.Major)
                .Include(t => t.Lecturer)
                .Where(t => t.LecturerId == uid)
                .AsQueryable();

            // FILTER HỌC KỲ
            if (!string.IsNullOrEmpty(semester))
            {
                query = query.Where(t => t.Semester == semester);
            }

            // FILTER TRẠNG THÁI
            if (!string.IsNullOrEmpty(status)
                && Enum.TryParse<TopicStatus>(status, out var se))
            {
                query = query.Where(t => t.Status == se);
            }

            // FILTER BỘ MÔN
            if (majorId.HasValue)
            {
                query = query.Where(t => t.MajorId == majorId.Value);
            }

            // DATA
            var topics = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var my = await _context.Topics
                .Where(t => t.LecturerId == uid)
                .ToListAsync();

            // KPI
            ViewBag.TotalTopics = my.Count;

            ViewBag.OpenTopics = my.Count(t =>
                t.IsApproved &&
                t.IsRegistrationOpen);

            ViewBag.PendingTopics = my.Count(t =>
                !t.IsApproved);

            // FIX LỖI NULL
            ViewBag.Majors = await _context.Majors.ToListAsync();

            return View("_TopicList", topics);
        }

        // ─────────────────────────────────────────────────────────────
        // TẠO ĐỀ TÀI  →  /Lecturer/Create
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Topic? topic)
        {
            try
            {
                if (topic == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không nhận được dữ liệu"
                    });
                }
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new
                    {
                        success = false,
                        message = string.Join(" | ", errors)
                    });
                }

                if (string.IsNullOrWhiteSpace(topic.Title))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Tên đề tài không được để trống"
                    });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Nếu admin tạo mà chưa chọn GV
                if (string.IsNullOrEmpty(topic.LecturerId))
                {
                    topic.LecturerId = userId;
                }

                topic.CreatedAt = DateTime.Now;

                topic.IsApproved = false;

                topic.IsRegistrationOpen = false;

                topic.Status = TopicStatus.Pending;

                if (string.IsNullOrEmpty(topic.TopicCode))
                {
                    topic.TopicCode =
                        $"TOPIC-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
                }

                if (topic.Deadline == default)
                {
                    topic.Deadline = DateTime.Now.AddMonths(3);
                }

                _context.Topics.Add(topic);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Tạo đề tài thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // SỬA ĐỀ TÀI  →  /Lecturer/Edit/{id}
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var topic = await _context.Topics
                .Include(t => t.Major)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();
            if (topic.LecturerId != lid && !User.IsInRole("Admin")) return Forbid();

            ViewBag.MajorId = new SelectList(
                await _context.Majors.Where(m => m.IsActive).ToListAsync(),
                "Id", "Name", topic.MajorId);
            return View(topic);          // ← trả đúng View "Edit", không dùng _TopicList
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Topic topic)
        {
            if (id != topic.Id) return NotFound();

            ModelState.Remove("Lecturer"); ModelState.Remove("Major");
            ModelState.Remove("Student"); ModelState.Remove("Registrations");

            if (ModelState.IsValid)
            {
                var existing = await _context.Topics.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id);
                if (existing == null) return NotFound();

                topic.LecturerId = existing.LecturerId;
                topic.CreatedAt = DateTime.Now;
                topic.IsApproved = false;          // Reset: Admin duyệt lại
                topic.IsRegistrationOpen = false;
                topic.Status = TopicStatus.Pending;

                try
                {
                    _context.Update(topic);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật xong! Chờ Admin duyệt lại.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Topics.Any(t => t.Id == topic.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(ThesisManagement));
            }

            ViewBag.MajorId = new SelectList(
                await _context.Majors.Where(m => m.IsActive).ToListAsync(),
                "Id", "Name", topic.MajorId);
            return View(topic);
        }

        // ─────────────────────────────────────────────────────────────
        // XÓA ĐỀ TÀI
        // ─────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();
            if (topic.LecturerId != lid && !User.IsInRole("Admin")) return Forbid();

            if (topic.Registrations != null && topic.Registrations.Any(r => r.Status == "Approved"))
            {
                TempData["Error"] = "Không thể xóa đề tài đã có sinh viên được duyệt!";
                return RedirectToAction(nameof(ThesisManagement));
            }

            if (topic.Registrations != null)
                _context.Registrations.RemoveRange(topic.Registrations);

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa đề tài \"{topic.Title}\".";
            return RedirectToAction(nameof(ThesisManagement));
        }

        // ─────────────────────────────────────────────────────────────
        // BẬT / TẮT ĐĂNG KÝ  (JSON)
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleRegistration(int id, bool isOpen)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var topic = await _context.Topics.Include(t => t.Registrations).FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();
            if (topic.LecturerId != lid && !User.IsInRole("Admin")) return Forbid();

            topic.IsRegistrationOpen = isOpen;
            topic.Status = isOpen
                ? (topic.Registrations != null && topic.Registrations.Count(r => r.Status == "Approved") >= topic.MaxStudents
                    ? TopicStatus.Full : TopicStatus.Available)
                : TopicStatus.Closed;

            await _context.SaveChangesAsync();
            return Json(new { success = true, isOpen, status = topic.Status.ToString() });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRegistrationStatus([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                int id = data.GetProperty("id").GetInt32();
                bool isOpen = data.GetProperty("isOpen").GetBoolean();
                var topic = await _context.Topics.Include(t => t.Registrations).FirstOrDefaultAsync(t => t.Id == id);
                if (topic == null) return Json(new { success = false, message = "Không tìm thấy" });

                var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (topic.LecturerId != lid && !User.IsInRole("Admin"))
                    return Json(new { success = false, message = "Không có quyền" });

                topic.IsRegistrationOpen = isOpen;
                topic.Status = !isOpen ? TopicStatus.Closed
                    : ((topic.Registrations?.Count(r => r.Status == "Approved") ?? 0) >= topic.MaxStudents
                        ? TopicStatus.Full : TopicStatus.Available);

                await _context.SaveChangesAsync();
                return Json(new { success = true, isOpen, currentStatus = topic.Status.ToString() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // IMPORT TOPICS TỪ EXCEL
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ImportTopics(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Chọn file Excel hợp lệ.";
                return RedirectToAction(nameof(ThesisManagement));
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int count = 0;

            try
            {
                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                using var pkg = new ExcelPackage(stream);
                var ws = pkg.Workbook.Worksheets[0];

                if (ws?.Dimension == null) { TempData["Error"] = "File trống."; return RedirectToAction(nameof(ThesisManagement)); }

                for (int row = 2; row <= ws.Dimension.Rows; row++)
                {
                    var title = ws.Cells[row, 2].Value?.ToString();
                    if (string.IsNullOrEmpty(title)) continue;

                    _context.Topics.Add(new Topic
                    {
                        TopicCode = ws.Cells[row, 1].Value?.ToString() ?? $"TOPIC-{Guid.NewGuid().ToString()[..8]}",
                        Title = title,
                        Category = ws.Cells[row, 3].Value?.ToString() ?? "Ứng dụng",
                        Description = ws.Cells[row, 4].Value?.ToString() ?? "",
                        Semester = "HK2-2025-2026",
                        LecturerId = lid,
                        CreatedAt = DateTime.Now,
                        IsApproved = false,
                        Status = TopicStatus.Pending,
                        IsRegistrationOpen = false,
                        Level = TopicLevel.Easy,
                        MaxStudents = int.TryParse(ws.Cells[row, 5].Value?.ToString(), out int m) ? m : 1,
                        Deadline = DateTime.Now.AddMonths(3)
                    });
                    count++;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã nhập {count} đề tài.";
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi: " + ex.Message; }

            return RedirectToAction(nameof(ThesisManagement));
        }

        // ─────────────────────────────────────────────────────────────
        // XUẤT EXCEL DANH SÁCH SV ĐÃ DUYỆT  →  GET /Lecturer/ExportRegistrations
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ExportRegistrations(int? topicId = null)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .Where(r => r.Topic.LecturerId == lid && r.Status == "Approved")
                .AsQueryable();

            if (topicId.HasValue) query = query.Where(r => r.TopicId == topicId.Value);

            var data = await query.OrderBy(r => r.Topic.Title).ThenBy(r => r.Student.FullName).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("SV đã duyệt");

            // Tiêu đề
            ws.Cell(1, 1).Value = "DANH SÁCH SINH VIÊN ĐÃ ĐƯỢC DUYỆT ĐỀ TÀI";
            ws.Range(1, 1, 1, 7).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Header
            var headers = new[] { "STT", "Mã đề tài", "Tên đề tài", "Mã SV", "Họ và tên", "Email", "Ngày duyệt" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 4;
            foreach (var r in data)
            {
                ws.Cell(row, 1).Value = row - 3;
                ws.Cell(row, 2).Value = r.Topic?.TopicCode ?? "";
                ws.Cell(row, 3).Value = r.Topic?.Title ?? "";
                ws.Cell(row, 4).Value = r.Student?.UserCode ?? "";
                ws.Cell(row, 5).Value = r.Student?.FullName ?? "";
                ws.Cell(row, 6).Value = r.Student?.Email ?? "";
                ws.Cell(row, 7).Value = r.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? r.CreatedAt.ToString("dd/MM/yyyy");

                bool even = row % 2 == 0;
                for (int c = 1; c <= 7; c++)
                {
                    ws.Cell(row, c).Style.Fill.BackgroundColor = even ? XLColor.FromHtml("#f0f4ff") : XLColor.White;
                    ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(row, c).Style.Border.OutsideBorderColor = XLColor.FromHtml("#e2e8f0");
                }
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(3);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"SV_DaDuyet_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────
        // THÔNG TIN CÁ NHÂN  →  GET + POST /Lecturer/Profile
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users
                .Include(u => u.Major)
                .FirstOrDefaultAsync(u => u.Id == lid);
            if (user == null) return RedirectToAction("Login", "Account");

            var myTopics = await _context.Topics
                .Include(t => t.Registrations)
                .Where(t => t.LecturerId == lid)
                .ToListAsync();

            ViewBag.TotalTopics = myTopics.Count;
            ViewBag.TotalStudents = myTopics.Sum(t => t.Registrations?.Count(r => r.Status == "Approved") ?? 0);
            ViewBag.Majors = await _context.Majors.Where(m => m.IsActive).ToListAsync();

            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            string fullName, string? faculty, string? degree,
            string? position, string? phoneNumber)
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == lid);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Error"] = "Họ tên không được để trống!";
                return RedirectToAction(nameof(Profile));
            }

            user.FullName = fullName.Trim();
            user.Faculty = faculty?.Trim();
            user.Degree = degree?.Trim();
            user.Position = position?.Trim();
            user.PhoneNumber = phoneNumber?.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // ─────────────────────────────────────────────────────────────
        // AJAX: THỐNG KÊ DASHBOARD  →  GET /Lecturer/GetTopicStats
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetTopicStats()
        {
            var lid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var topics = await _context.Topics
                .Include(t => t.Registrations)
                .Where(t => t.LecturerId == lid)
                .ToListAsync();

            return Json(new
            {
                total = topics.Count,
                approved = topics.Count(t => t.IsApproved),
                pending = topics.Count(t => !t.IsApproved),
                full = topics.Count(t => t.Status == TopicStatus.Full),
                totalStudents = topics.Sum(t => t.Registrations?.Count(r => r.Status == "Approved") ?? 0)
            });
        }

        // ─────────────────────────────────────────────────────────────
        // HELPER
        // ─────────────────────────────────────────────────────────────
        private async Task Notify(string userId, string title, string content, string type, string url = "")
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Content = content,
                Type = type,
                RedirectUrl = url,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────
        // NESTED VM  (dùng trong Approval action)
        // ─────────────────────────────────────────────────────────────
        public class GroupedRegistration
        {
            public Topic? Topic { get; set; }
            public string TopicTitle { get; set; } = "";
            public int TopicId { get; set; }
            public List<ApplicationUser?> Students { get; set; } = new();
            public List<int> RegistrationIds { get; set; } = new();
            public DateTime CreatedAt { get; set; }
        }

        public class LecturerScheduleVM
        {
            public string Day { get; set; } = "";
            public string Date { get; set; } = "";
            public string Title { get; set; } = "";
            public string Time { get; set; } = "";
            public string Color { get; set; } = "primary";
            public int DaysLeft { get; set; }
            public bool IsUrgent { get; set; }
        }

        public async Task<IActionResult> TimelineManagement()
        {
            var lecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var timelines = await _context.Timelines

                .Include(t => t.TimelineSubmissions)
                    .ThenInclude(s => s.Student)

                .Include(t => t.TimelineSubmissions)
                    .ThenInclude(s => s.Versions)

                .OrderBy(t => t.Date)

                .ToListAsync();

            return View(timelines);
        }
        [HttpPost]
        public async Task<IActionResult> ApproveSubmission(int id)
        {
            var sub = await _context.TimelineSubmissions
                .Include(x => x.Timeline)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (sub == null)
                return NotFound();

            // cập nhật trạng thái
            sub.Status = SubmissionStatus.Approved;


            sub.ReviewedAt = DateTime.Now;

            sub.ReviewedById =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            // thêm notification
            _context.Notifications.Add(new Notification
            {
                UserId = sub.StudentId,

                Title = "Tiến độ đã được duyệt",

                Content =
                    $"Mốc \"{sub.Timeline?.Title}\" đã được duyệt.",

                Type = "Timeline",

                RedirectUrl = "/StudentTimeline",

                CreatedAt = DateTime.Now
            });

            // save một lần
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã duyệt bài nộp";

            return RedirectToAction(nameof(TimelineManagement));
        }
        [HttpPost]
        public async Task<IActionResult> RejectSubmission(
     int id,
     string? comment)
        {
            var sub = await _context.TimelineSubmissions
                .Include(x => x.Timeline)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (sub == null)
                return NotFound();

            // cập nhật trạng thái
            sub.Status = SubmissionStatus.Rejected;

            // lưu nhận xét giảng viên
            sub.LecturerComment = comment;

            sub.ReviewedAt = DateTime.Now;

            sub.ReviewedById =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            // gửi notification
            _context.Notifications.Add(new Notification
            {
                UserId = sub.StudentId,

                Title = "Tiến độ bị từ chối",

                Content =
                    $"Mốc \"{sub.Timeline?.Title}\" chưa đạt yêu cầu.",

                Type = "Timeline",

                RedirectUrl = "/StudentTimeline",

                CreatedAt = DateTime.Now
            });

            // save một lần
            await _context.SaveChangesAsync();

            TempData["Error"] = "Đã từ chối bài nộp";

            return RedirectToAction(nameof(TimelineManagement));
        }
    }
}
