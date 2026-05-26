using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace KLTN_Registration_System.Controllers
{
    [Authorize]
public class TimelineController : BaseController
{
    private readonly AppDbContext _context;

        public TimelineController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
        }

        public class DefenseEligibilityVM
        {
            public string StudentId { get; set; } = "";
            public string StudentName { get; set; } = "";
            public string StudentCode { get; set; } = "";
            public string TopicTitle { get; set; } = "";
            public string LecturerName { get; set; } = "";
            public int RequiredCount { get; set; }
            public int ApprovedCount { get; set; }
            public int PendingCount { get; set; }
            public int RejectedCount { get; set; }
            public int MissingCount { get; set; }
            public bool IsEligible { get; set; }
            public bool HasCompletedThesis { get; set; }
            public DateTime? ThesisCompletedAt { get; set; }
        }

        // ══════════════════════════════════════
        // VIEW TIMELINE (Admin + Lecturer)
        // ══════════════════════════════════════
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Index(string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            var timelineQuery = selectedPeriod == null
                ? _context.Timelines.Where(_ => false)
                : FilterTimelinesByActivePeriod(_context.Timelines, selectedPeriod);

            var timelines = await timelineQuery
                .Include(t => t.TimelineSubmissions)
                    .ThenInclude(s => s.Student)
                .OrderBy(t => t.Date)
                .ToListAsync();

            ViewBag.ActivePeriod = selectedPeriod;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            return View(timelines);
        }

        // ══════════════════════════════════════
        // ADMIN: CRUD TIMELINE
        // ══════════════════════════════════════

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string title,
            string? description,
            DateTime date,
            string? type,
            DateTime? submissionDeadline,
            DateTime? reviewDeadline,
            string? submissionType,
            bool isActive,
            bool allowSubmission)
        {
            if (submissionDeadline.HasValue)
            {
                allowSubmission = true;
            }

            if (!allowSubmission)
            {
                submissionDeadline = null;
                submissionType = null;
            }

            var validationError = ValidateTimelineInput(
                title,
                date,
                submissionDeadline,
                reviewDeadline,
                allowSubmission);
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction(nameof(Index));
            }

            var activePeriod = await GetSelectedPeriodForTimelineAsync();
            if (activePeriod == null)
            {
                TempData["Error"] = "Chưa có đợt đăng ký tương ứng. Vui lòng tạo/chọn đợt ở mục Cài đặt trước khi thêm timeline.";
                return RedirectToAction(nameof(Index));
            }

            _context.Timelines.Add(new Timeline
            {
                RegistrationPeriodId = activePeriod.Id,
                Title = title.Trim(),
                Description = description?.Trim(),
                Date = date,
                Type = type?.Trim(),
                SubmissionDeadline = submissionDeadline,
                ReviewDeadline = reviewDeadline,
                SubmissionType = NormalizeSubmissionType(submissionType),
                IsActive = isActive,
                AllowSubmission = allowSubmission
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm mốc thời gian.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string title,
            string? description,
            DateTime date,
            string? type,
            DateTime? submissionDeadline,
            DateTime? reviewDeadline,
            string? submissionType,
            bool isActive,
            bool allowSubmission)
        {
            var tl = await _context.Timelines.FindAsync(id);
            if (tl == null) return NotFound();

            if (submissionDeadline.HasValue)
            {
                allowSubmission = true;
            }

            if (!allowSubmission)
            {
                submissionDeadline = null;
                submissionType = null;
            }

            var validationError = ValidateTimelineInput(
                title,
                date,
                submissionDeadline,
                reviewDeadline,
                allowSubmission);
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction(nameof(Index));
            }

            tl.Title = title.Trim();
            tl.Description = description?.Trim();
            tl.Date = date;
            tl.Type = type?.Trim();
            tl.SubmissionDeadline = submissionDeadline;
            tl.ReviewDeadline = reviewDeadline;
            tl.SubmissionType = NormalizeSubmissionType(submissionType);
            tl.IsActive = isActive;
            tl.AllowSubmission = allowSubmission;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật mốc thời gian.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var tl = await _context.Timelines
                .Include(t => t.TimelineSubmissions)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tl == null) return NotFound();

            if (tl.TimelineSubmissions.Any())
            {
                TempData["Error"] = "Không thể xóa mốc thời gian đã có bài nộp. Hãy tắt mốc thay vì xóa để giữ dữ liệu.";
                return RedirectToAction(nameof(Index));
            }

            _context.Timelines.Remove(tl);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa mốc thời gian.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var tl = await _context.Timelines.FindAsync(id);
            if (tl == null) return NotFound();

            tl.IsActive = !tl.IsActive;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ══════════════════════════════════════
        // ADMIN: TỔNG HỢP KẾT QUẢ
        // Chỉ xem + export, KHÔNG duyệt/từ chối
        // ══════════════════════════════════════

        /// <summary>
        /// Trang tổng hợp tất cả submission đã được Lecturer duyệt (Approved).
        /// Admin dùng để xem kết quả cuối và export danh sách.
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovedSubmissions(
            string? keyword,
            string? timelineId,
            int page = 1,
            string? semester = null,
            string? year = null)
        {
            const int pageSize = 15;

            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            keyword = keyword?.Trim();
            page = Math.Max(page, 1);
            int? selectedTimelineIdFromRequest = ResolveTimelineId(timelineId);

            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Include(s => s.ReviewedBy)
                .Where(s => s.Status == SubmissionStatus.Approved)
                .AsQueryable();

            var timelineQuery = selectedPeriod == null
                ? _context.Timelines.Where(_ => false)
                : FilterTimelinesByActivePeriod(_context.Timelines, selectedPeriod);

            var timelines = await timelineQuery
                .OrderBy(t => t.Date)
                .ToListAsync();

            int requestedTimelineId = selectedTimelineIdFromRequest.GetValueOrDefault();
            var selectedTimeline = requestedTimelineId > 0
                ? timelines.FirstOrDefault(t => t.Id == requestedTimelineId)
                : null;
            int? selectedTimelineId = selectedTimeline?.Id;

            if (selectedTimelineIdFromRequest.HasValue && selectedTimeline == null)
            {
                TempData["Error"] = "Mốc thời gian được chọn không tồn tại.";
            }

            if (selectedTimelineId.HasValue)
            {
                int timelineFilterId = selectedTimelineId.Value;
                query = query.Where(s => s.TimelineId == timelineFilterId);
            }
            else
            {
                query = query.Where(_ => false);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(s =>
                    (s.Student != null && (s.Student.FullName ?? "").Contains(keyword)) ||
                    (s.Student != null && (s.Student.UserCode ?? "").Contains(keyword)) ||
                    (s.Timeline != null && (s.Timeline.Title ?? "").Contains(keyword)));
            }

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var todayApproved = await query.CountAsync(s =>
                s.ReviewedAt.HasValue &&
                s.ReviewedAt.Value >= today &&
                s.ReviewedAt.Value < tomorrow);

            var submissions = await query
                .OrderBy(s => s.Student != null ? s.Student.UserCode : "")
                .ThenBy(s => s.Student != null ? s.Student.FullName : "")
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Timelines = timelines;

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;
            ViewBag.TimelineId = selectedTimelineId;
            ViewBag.TotalApproved = total;
            ViewBag.TodayApproved = todayApproved;
            ViewBag.SelectedTimeline = selectedTimeline;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            ViewBag.Semester = selectedPeriod?.SemesterCode;
            ViewBag.Year = selectedPeriod?.AcademicYear;

            return View(submissions);
        }

        private int? ResolveTimelineId(string? timelineId)
        {
            if (int.TryParse(timelineId, out int parsedFromParameter) && parsedFromParameter > 0)
            {
                return parsedFromParameter;
            }

            if (Request.Query.TryGetValue("timelineId", out var rawTimelineId)
                && int.TryParse(rawTimelineId.FirstOrDefault(), out int parsedTimelineId)
                && parsedTimelineId > 0)
            {
                return parsedTimelineId;
            }

            return null;
        }

        /// <summary>
        /// Tổng hợp các submission còn Pending sau khi ReviewDeadline đã qua.
        /// Admin chỉ xem để biết GV nào chưa duyệt — KHÔNG có action duyệt.
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PendingAfterDeadline(int? timelineId, string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            var now = DateTime.Now;
            var timelinesForPeriod = selectedPeriod == null
                ? _context.Timelines.Where(_ => false)
                : FilterTimelinesByActivePeriod(_context.Timelines, selectedPeriod);

            var overdueTimelines = await timelinesForPeriod
                .Where(t => t.ReviewDeadline.HasValue && t.ReviewDeadline.Value < now)
                .OrderBy(t => t.Date)
                .ToListAsync();

            if (timelineId.HasValue && overdueTimelines.All(t => t.Id != timelineId.Value))
            {
                TempData["Error"] = "Mốc thời gian được chọn không thuộc đợt hiện tại hoặc chưa quá hạn duyệt.";
                timelineId = null;
            }

            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Where(s =>
                    s.Status == SubmissionStatus.Pending &&
                    s.Timeline != null &&
                    s.Timeline.ReviewDeadline.HasValue &&
                    s.Timeline.ReviewDeadline.Value < now)
                .AsQueryable();

            query = selectedPeriod == null
                ? query.Where(_ => false)
                : query.Where(s => s.Timeline != null && s.Timeline.RegistrationPeriodId == selectedPeriod.Id);

            if (timelineId.HasValue)
                query = query.Where(s => s.TimelineId == timelineId.Value);

            var overdue = await query
                .OrderBy(s => s.Timeline != null ? s.Timeline.ReviewDeadline : null)
                .ThenBy(s => s.Student != null ? s.Student.FullName : "")
                .ToListAsync();

            ViewBag.Timelines = overdueTimelines;
            ViewBag.TimelineId = timelineId;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            ViewBag.Semester = selectedPeriod?.SemesterCode;
            ViewBag.Year = selectedPeriod?.AcademicYear;

            // Thống kê nhanh cho Admin
            ViewBag.TotalOverdue = overdue.Count;
            var overdueStudentIds = overdue
                .Where(s => !string.IsNullOrEmpty(s.StudentId))
                .Select(s => s.StudentId!)
                .Distinct()
                .ToList();

            var affectedLecturersQuery = _context.Registrations
                .Where(r => overdueStudentIds.Contains(r.StudentId) &&
                    r.Status == "Approved" &&
                    r.Topic != null &&
                    r.Topic.LecturerId != null);

            if (selectedPeriod != null)
            {
                affectedLecturersQuery = affectedLecturersQuery.Where(r =>
                    r.RegistrationPeriodId == selectedPeriod.Id
                    || (r.RegistrationPeriodId == null && r.Topic != null && r.Topic.Semester == selectedPeriod.Name));
            }
            else
            {
                affectedLecturersQuery = affectedLecturersQuery.Where(_ => false);
            }

            ViewBag.AffectedLecturers = await affectedLecturersQuery
                .Select(r => r.Topic.LecturerId)
                .Distinct()
                .CountAsync();

            return View(overdue);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DefenseEligibility(string? status = null, string? keyword = null, string? semester = null, string? year = null)
        {
            keyword = keyword?.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
            var (allRows, selectedPeriod, requiredTimelineCount) = await BuildDefenseEligibilityRowsAsync(status, keyword, semester, year);
            var rows = status switch
            {
                "eligible" => allRows.Where(x => x.IsEligible).ToList(),
                "blocked" => allRows.Where(x => !x.IsEligible).ToList(),
                _ => allRows
            };

            ViewBag.SelectedStatus = status;
            ViewBag.Keyword = keyword;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            ViewBag.RequiredTimelineCount = requiredTimelineCount;
            ViewBag.TotalAll = allRows.Count;
            ViewBag.TotalEligible = allRows.Count(x => x.IsEligible);
            ViewBag.TotalBlocked = allRows.Count(x => !x.IsEligible);
            ViewBag.Semester = selectedPeriod?.SemesterCode;
            ViewBag.Year = selectedPeriod?.AcademicYear;

            return View(rows.OrderBy(x => x.IsEligible).ThenBy(x => x.StudentName).ToList());
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportDefenseEligibility(string? status = null, string? keyword = null, string? semester = null, string? year = null)
        {
            keyword = keyword?.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
            var (allRows, selectedPeriod, requiredTimelineCount) = await BuildDefenseEligibilityRowsAsync(status, keyword, semester, year);
            var rows = status switch
            {
                "eligible" => allRows.Where(x => x.IsEligible).ToList(),
                "blocked" => allRows.Where(x => !x.IsEligible).ToList(),
                _ => allRows
            };
            rows = rows.OrderBy(x => x.IsEligible).ThenBy(x => x.StudentName).ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Dieu kien bao cao");

            var periodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName() ?? "Dot hien tai";
            var exportTitle = status switch
            {
                "eligible" => "Danh sách sinh viên đủ điều kiện báo cáo khóa luận",
                "blocked" => "Danh sách sinh viên chưa đủ điều kiện báo cáo khóa luận",
                _ => "Danh sách điều kiện báo cáo khóa luận"
            };

            worksheet.Cell(1, 1).Value = exportTitle;
            worksheet.Range(1, 1, 1, 12).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Đợt: {periodName} | Mốc bắt buộc: {requiredTimelineCount} | Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
            worksheet.Range(2, 1, 2, 12).Merge();
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

            var headers = new[]
            {
                "STT",
                "MSSV",
                "Họ tên",
                "Đề tài",
                "Giảng viên hướng dẫn",
                "Mốc bắt buộc",
                "Đã duyệt",
                "Chờ duyệt",
                "Từ chối",
                "Chưa nộp",
                "Tỷ lệ hoàn thành",
                "Trạng thái"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(4, 1, 4, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            headerRange.Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var rowIndex = 5;
            for (var i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                var progress = item.RequiredCount > 0
                    ? Math.Min(100, item.ApprovedCount * 100 / item.RequiredCount)
                    : 0;

                worksheet.Cell(rowIndex, 1).Value = i + 1;
                worksheet.Cell(rowIndex, 2).Value = item.StudentCode;
                worksheet.Cell(rowIndex, 3).Value = item.StudentName;
                worksheet.Cell(rowIndex, 4).Value = item.TopicTitle;
                worksheet.Cell(rowIndex, 5).Value = item.LecturerName;
                worksheet.Cell(rowIndex, 6).Value = item.RequiredCount;
                worksheet.Cell(rowIndex, 7).Value = item.ApprovedCount;
                worksheet.Cell(rowIndex, 8).Value = item.PendingCount;
                worksheet.Cell(rowIndex, 9).Value = item.RejectedCount;
                worksheet.Cell(rowIndex, 10).Value = item.MissingCount;
                worksheet.Cell(rowIndex, 11).Value = $"{progress}%";
                worksheet.Cell(rowIndex, 12).Value = item.IsEligible ? "Đủ điều kiện" : "Chưa đủ điều kiện";

                worksheet.Cell(rowIndex, 12).Style.Font.FontColor = item.IsEligible
                    ? XLColor.FromHtml("#15803D")
                    : XLColor.FromHtml("#DC2626");

                rowIndex++;
            }

            if (rows.Any())
            {
                var dataRange = worksheet.Range(5, 1, rowIndex - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Range(5, 1, rowIndex - 1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range(5, 6, rowIndex - 1, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.Column(4).Width = Math.Min(worksheet.Column(4).Width, 45);
            worksheet.Column(5).Width = Math.Min(worksheet.Column(5).Width, 28);
            worksheet.Column(4).Style.Alignment.WrapText = true;
            worksheet.SheetView.FreezeRows(4);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var filePrefix = status switch
            {
                "eligible" => "Sinh-vien-du-dieu-kien-bao-cao",
                "blocked" => "Sinh-vien-chua-du-dieu-kien-bao-cao",
                _ => "Dieu-kien-bao-cao"
            };
            var safePeriodName = string.Join("-", periodName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var fileName = $"{filePrefix}-{safePeriodName}-{DateTime.Now:yyyyMMddHHmm}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DefenseResults(string? status = null, string? keyword = null, string? semester = null, string? year = null)
        {
            keyword = keyword?.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "waiting" : status.Trim().ToLowerInvariant();
            var (rows, reportRows, selectedPeriod, requiredTimelineCount) =
                await BuildDefenseResultRowsAsync(status, keyword, semester, year);

            ViewBag.SelectedStatus = status;
            ViewBag.Keyword = keyword;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            ViewBag.RequiredTimelineCount = requiredTimelineCount;
            ViewBag.TotalReportable = reportRows.Count;
            ViewBag.TotalWaiting = reportRows.Count(x => x.IsEligible && !x.HasCompletedThesis);
            ViewBag.TotalCompleted = reportRows.Count(x => x.HasCompletedThesis);
            ViewBag.Semester = selectedPeriod?.SemesterCode;
            ViewBag.Year = selectedPeriod?.AcademicYear;

            return View(rows
                .OrderBy(x => x.HasCompletedThesis)
                .ThenBy(x => x.StudentName)
                .ToList());
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportDefenseResults(string? status = null, string? keyword = null, string? semester = null, string? year = null)
        {
            keyword = keyword?.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "waiting" : status.Trim().ToLowerInvariant();
            var (rows, _, selectedPeriod, requiredTimelineCount) =
                await BuildDefenseResultRowsAsync(status, keyword, semester, year);
            rows = rows
                .OrderBy(x => x.HasCompletedThesis)
                .ThenBy(x => x.StudentName)
                .ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Ket qua bao cao");
            var periodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName() ?? "Dot hien tai";
            var exportTitle = status switch
            {
                "completed" => "Danh sách sinh viên đã hoàn thành khóa luận",
                "waiting" => "Danh sách sinh viên chờ kết quả báo cáo khóa luận",
                _ => "Danh sách kết quả báo cáo khóa luận"
            };

            worksheet.Cell(1, 1).Value = exportTitle;
            worksheet.Range(1, 1, 1, 9).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Đợt: {periodName} | Mốc bắt buộc: {requiredTimelineCount} | Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
            worksheet.Range(2, 1, 2, 9).Merge();
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

            var headers = new[]
            {
                "STT",
                "MSSV",
                "Họ tên",
                "Đề tài",
                "Giảng viên hướng dẫn",
                "Mốc đã duyệt",
                "Mốc bắt buộc",
                "Trạng thái",
                "Ngày hoàn thành"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(4, 1, 4, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            headerRange.Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var rowIndex = 5;
            for (var i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                worksheet.Cell(rowIndex, 1).Value = i + 1;
                worksheet.Cell(rowIndex, 2).Value = item.StudentCode;
                worksheet.Cell(rowIndex, 3).Value = item.StudentName;
                worksheet.Cell(rowIndex, 4).Value = item.TopicTitle;
                worksheet.Cell(rowIndex, 5).Value = item.LecturerName;
                worksheet.Cell(rowIndex, 6).Value = item.ApprovedCount;
                worksheet.Cell(rowIndex, 7).Value = item.RequiredCount;
                worksheet.Cell(rowIndex, 8).Value = item.HasCompletedThesis ? "Hoàn thành" : "Chờ kết quả";
                worksheet.Cell(rowIndex, 9).Value = item.ThesisCompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                worksheet.Cell(rowIndex, 8).Style.Font.FontColor = item.HasCompletedThesis
                    ? XLColor.FromHtml("#15803D")
                    : XLColor.FromHtml("#3730A3");
                rowIndex++;
            }

            if (rows.Any())
            {
                var dataRange = worksheet.Range(5, 1, rowIndex - 1, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Range(5, 1, rowIndex - 1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range(5, 6, rowIndex - 1, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.Column(4).Width = Math.Min(worksheet.Column(4).Width, 45);
            worksheet.Column(5).Width = Math.Min(worksheet.Column(5).Width, 28);
            worksheet.Column(4).Style.Alignment.WrapText = true;
            worksheet.SheetView.FreezeRows(4);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var filePrefix = status switch
            {
                "completed" => "Sinh-vien-hoan-thanh-khoa-luan",
                "waiting" => "Sinh-vien-cho-ket-qua-bao-cao",
                _ => "Ket-qua-bao-cao"
            };
            var safePeriodName = string.Join("-", periodName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var fileName = $"{filePrefix}-{safePeriodName}-{DateTime.Now:yyyyMMddHHmm}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDefenseResult(
            string studentId,
            bool completed,
            string? status = null,
            string? keyword = null,
            string? semester = null,
            string? year = null)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                TempData["Error"] = "Không tìm thấy sinh viên cần cập nhật.";
                return RedirectToAction(nameof(DefenseResults), new { status, keyword, semester, year });
            }

            var user = await _userManager.FindByIdAsync(studentId);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Student"))
            {
                TempData["Error"] = "Không tìm thấy sinh viên hợp lệ.";
                return RedirectToAction(nameof(DefenseResults), new { status, keyword, semester, year });
            }

            if (completed)
            {
                var (rows, _, _) = await BuildDefenseEligibilityRowsAsync("all", null, semester, year);
                var row = rows.FirstOrDefault(x => x.StudentId == studentId);
                if (row == null || !row.IsEligible)
                {
                    TempData["Error"] = "Sinh viên chưa đủ điều kiện báo cáo, không thể đánh dấu hoàn thành.";
                    return RedirectToAction(nameof(DefenseResults), new { status, keyword, semester, year });
                }
            }

            user.HasCompletedThesis = completed;
            user.ThesisCompletedAt = completed ? DateTime.Now : null;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Không thể cập nhật kết quả báo cáo.";
                return RedirectToAction(nameof(DefenseResults), new { status, keyword, semester, year });
            }

            if (completed)
            {
                var periodStudents = await _context.PeriodStudents
                    .Where(ps => ps.StudentId == user.Id && ps.IsEligible)
                    .ToListAsync();

                foreach (var periodStudent in periodStudents)
                {
                    periodStudent.IsEligible = false;
                }
            }

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = completed ? "Hoàn thành khóa luận" : "Cập nhật kết quả khóa luận",
                Content = completed
                    ? "Bạn đã được ghi nhận hoàn thành khóa luận. Bạn sẽ không cần đăng ký khóa luận ở các kỳ sau."
                    : "Trạng thái hoàn thành khóa luận của bạn đã được mở lại để tiếp tục xử lý.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "DefenseResult",
                Priority = completed ? 1 : 0,
                RedirectUrl = "/Student/Timeline",
                TargetUrl = "/Student/Timeline"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = completed
                ? $"Đã đánh dấu {user.FullName ?? user.UserName} hoàn thành khóa luận."
                : $"Đã hủy trạng thái hoàn thành của {user.FullName ?? user.UserName}.";

            return RedirectToAction(nameof(DefenseResults), new { status, keyword, semester, year });
        }

        private async Task<(List<DefenseEligibilityVM> Rows, List<DefenseEligibilityVM> AllReportRows, RegistrationPeriod? SelectedPeriod, int RequiredTimelineCount)> BuildDefenseResultRowsAsync(
            string? status,
            string? keyword,
            string? semester,
            string? year)
        {
            status = string.IsNullOrWhiteSpace(status) ? "waiting" : status.Trim().ToLowerInvariant();
            var (allRows, selectedPeriod, requiredTimelineCount) = await BuildDefenseEligibilityRowsAsync("all", keyword, semester, year);
            var reportRows = allRows
                .Where(x => x.IsEligible || x.HasCompletedThesis)
                .ToList();

            var rows = status switch
            {
                "completed" => reportRows.Where(x => x.HasCompletedThesis).ToList(),
                "waiting" => reportRows.Where(x => x.IsEligible && !x.HasCompletedThesis).ToList(),
                _ => reportRows
            };

            return (rows, reportRows, selectedPeriod, requiredTimelineCount);
        }

        private async Task<(List<DefenseEligibilityVM> Rows, RegistrationPeriod? SelectedPeriod, int RequiredTimelineCount)> BuildDefenseEligibilityRowsAsync(
            string? status,
            string? keyword,
            string? semester,
            string? year)
        {
            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            keyword = keyword?.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

            var requiredTimelines = selectedPeriod == null
                ? new List<Timeline>()
                : await FilterTimelinesByActivePeriod(_context.Timelines, selectedPeriod)
                    .Where(t => t.IsActive && t.AllowSubmission)
                    .OrderBy(t => t.Date)
                    .ToListAsync();

            var approvedRegistrations = selectedPeriod == null
                ? new List<Registration>()
                : await _context.Registrations
                    .Include(r => r.Student)
                    .Include(r => r.Topic!)
                        .ThenInclude(t => t.Lecturer)
                    .Where(r => r.Status == "Approved"
                        && (r.RegistrationPeriodId == selectedPeriod.Id
                            || (r.RegistrationPeriodId == null && r.Topic != null && r.Topic.Semester == selectedPeriod.Name)))
                    .OrderBy(r => r.Student!.FullName)
                    .ToListAsync();

            var studentIds = approvedRegistrations
                .Select(r => r.StudentId)
                .Distinct()
                .ToList();

            var timelineIds = requiredTimelines.Select(t => t.Id).ToList();
            var submissions = studentIds.Any() && timelineIds.Any()
                ? await _context.TimelineSubmissions
                    .Where(s => s.StudentId != null
                        && studentIds.Contains(s.StudentId)
                        && timelineIds.Contains(s.TimelineId))
                    .ToListAsync()
                : new List<TimelineSubmission>();

            var latestSubmissions = submissions
                .GroupBy(s => new { s.StudentId, s.TimelineId })
                .ToDictionary(
                    g => (g.Key.StudentId!, g.Key.TimelineId),
                    g => g.OrderByDescending(s => s.SubmittedAt).First());

            var rows = approvedRegistrations
                .GroupBy(r => r.StudentId)
                .Select(g =>
                {
                    var first = g.First();
                    int approved = 0;
                    int pending = 0;
                    int rejected = 0;
                    int missing = 0;

                    foreach (var timeline in requiredTimelines)
                    {
                        if (!latestSubmissions.TryGetValue((g.Key, timeline.Id), out var latest))
                        {
                            missing++;
                            continue;
                        }

                        if (latest.Status == SubmissionStatus.Approved) approved++;
                        else if (latest.Status == SubmissionStatus.Pending) pending++;
                        else if (latest.Status == SubmissionStatus.Rejected) rejected++;
                    }

                    return new DefenseEligibilityVM
                    {
                        StudentId = g.Key,
                        StudentName = first.Student?.FullName ?? first.Student?.Email ?? "Sinh viên",
                        StudentCode = first.Student?.UserCode ?? "",
                        TopicTitle = first.Topic?.Title ?? "",
                        LecturerName = first.Topic?.Lecturer?.FullName ?? first.Topic?.Lecturer?.Email ?? "Chưa có GV",
                        RequiredCount = requiredTimelines.Count,
                        ApprovedCount = approved,
                        PendingCount = pending,
                        RejectedCount = rejected,
                        MissingCount = missing,
                        IsEligible = requiredTimelines.Any() && approved == requiredTimelines.Count,
                        HasCompletedThesis = first.Student?.HasCompletedThesis == true,
                        ThesisCompletedAt = first.Student?.ThesisCompletedAt
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                rows = rows
                    .Where(x => x.StudentName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || x.StudentCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || x.TopicTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || x.LecturerName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return (rows, selectedPeriod, requiredTimelines.Count);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SubmissionDetail(
            int id,
            string? semester = null,
            string? year = null,
            string? keyword = null,
            string? timelineId = null,
            int page = 1)
        {
            var submission = await _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Include(s => s.ReviewedBy)
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();

            if (submission.Status != SubmissionStatus.Approved)
            {
                TempData["Error"] = "Admin chỉ xem chi tiết các bài đã được giảng viên duyệt.";
                return RedirectToAction(nameof(ApprovedSubmissions), new { keyword, timelineId, semester, year, page });
            }

            submission.Versions = submission.Versions
                .OrderByDescending(v => v.VersionNumber)
                .ThenByDescending(v => v.UploadedAt)
                .ToList();

            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            if (selectedPeriod == null && submission.Timeline?.RegistrationPeriodId != null)
            {
                selectedPeriod = await _context.RegistrationPeriods
                    .FirstOrDefaultAsync(p => p.Id == submission.Timeline.RegistrationPeriodId);
            }

            ViewBag.Semester = selectedPeriod?.SemesterCode ?? semester;
            ViewBag.Year = selectedPeriod?.AcademicYear ?? year;
            ViewBag.SelectedPeriodName = selectedPeriod?.Name ?? GetAdminSelectedPeriodName();
            ViewBag.Keyword = keyword;
            ViewBag.TimelineId = !string.IsNullOrWhiteSpace(timelineId)
                ? timelineId
                : submission.TimelineId.ToString();
            ViewBag.Page = page < 1 ? 1 : page;

            return View(submission);
        }

        /// <summary>
        /// Export danh sách kết quả đã duyệt ra file Excel/CSV.
        /// Admin dùng để tổng hợp cuối kỳ.
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportApproved(int? timelineId, string? semester = null, string? year = null)
        {
            return await ExportApprovedSubmissions(null, timelineId?.ToString(), semester, year);
        }

        // ══════════════════════════════════════
        // STUDENT: NỘP BÀI
        // ══════════════════════════════════════
        [Authorize(Roles = "Student")]
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> SubmitTimeline(
            int timelineId,
            IFormFile file)
        {
            var now = DateTime.Now;
            var tl = await _context.Timelines.FindAsync(timelineId);

            if (tl == null) return NotFound();

            // Guard 1: mốc có cho nộp không
            if (!tl.IsActive || !tl.AllowSubmission)
            {
                TempData["Error"] = "Mốc này không cho phép nộp bài.";
                return RedirectToAction("Timeline", "Student");
            }

            if (tl.Date > now)
            {
                TempData["Error"] = $"Mốc này chưa mở nộp. Thời gian mở: {tl.Date:dd/MM/yyyy HH:mm}.";
                return RedirectToAction("Timeline", "Student");
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file để nộp.";
                return RedirectToAction("Timeline", "Student");
            }

            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            var hasApprovedTopic = await _context.Registrations.AnyAsync(r =>
                r.StudentId == studentId &&
                r.Status == "Approved");

            if (!hasApprovedTopic)
            {
                TempData["Error"] = "Bạn cần có đề tài đã được duyệt trước khi nộp timeline.";
                return RedirectToAction("Timeline", "Student");
            }

            const long maxFileSize = 20 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                TempData["Error"] = "File không được vượt quá 20MB.";
                return RedirectToAction("Timeline", "Student");
            }

            var allowedExtensions = GetAllowedExtensions(tl);
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"Định dạng file không hợp lệ. Chỉ hỗ trợ: {FormatAllowedExtensions(allowedExtensions)}.";
                return RedirectToAction("Timeline", "Student");
            }

            // Tìm submission hiện tại
            var existing = await _context.TimelineSubmissions
                .Include(s => s.Versions)
                .Where(s =>
                    s.TimelineId == timelineId &&
                    s.StudentId == studentId)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync();

            // Guard 2: còn trong SubmissionDeadline không.
            // Nếu bài mới nhất bị từ chối, sinh viên được nộp lại bản chỉnh sửa.
            bool isRejectedResubmission = existing?.Status == SubmissionStatus.Rejected;
            if (tl.SubmissionDeadline.HasValue
                && now > tl.SubmissionDeadline.Value
                && !isRejectedResubmission)
            {
                TempData["Error"] =
                    $"Đã quá hạn nộp bài ({tl.SubmissionDeadline.Value:dd/MM/yyyy HH:mm}). Không thể nộp.";
                return RedirectToAction("Timeline", "Student");
            }

            if (existing != null && existing.Status != SubmissionStatus.Rejected)
            {
                TempData["Error"] = existing.Status == SubmissionStatus.Pending
                    ? "Bài của bạn đang chờ giảng viên xem xét. Không thể nộp thêm."
                    : "Bài của bạn đã được duyệt. Không thể nộp lại.";
                return RedirectToAction("Timeline", "Student");
            }

            // Lưu file
            var uploadsDir = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "timeline");
            Directory.CreateDirectory(uploadsDir);

            var nextVersion = existing?.Versions.Any() == true
                ? existing.Versions.Max(v => v.VersionNumber) + 1
                : 1;
            var fileName = $"{studentId}_{timelineId}_{now:yyyyMMddHHmmss}_v{nextVersion}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var fileUrl = $"/uploads/timeline/{fileName}";

            if (existing != null)
            {
                // Nộp lại sau khi bị từ chối
                existing.FilePath = fileUrl;
                existing.FileName = Path.GetFileName(file.FileName);
                existing.SubmittedAt = now;
                existing.Status = SubmissionStatus.Pending;
                existing.Comment = null;
                existing.Score = null;
                existing.ReviewedAt = null;
                existing.ReviewedById = null;
                existing.LecturerComment = null;
                existing.IsCompleted = false;
            }
            else
            {
                // Nộp lần đầu
                _context.TimelineSubmissions.Add(new TimelineSubmission
                {
                    TimelineId = timelineId,
                    StudentId = studentId,
                    FilePath = fileUrl,
                    FileName = Path.GetFileName(file.FileName),
                    SubmittedAt = now,
                    Status = SubmissionStatus.Pending
                });

                await _context.SaveChangesAsync();
                existing = await _context.TimelineSubmissions
                    .Include(s => s.Versions)
                    .FirstAsync(s => s.TimelineId == timelineId && s.StudentId == studentId);
            }

            _context.TimelineSubmissionVersions.Add(new TimelineSubmissionVersion
            {
                TimelineSubmissionId = existing.Id,
                FileName = Path.GetFileName(file.FileName),
                FilePath = fileUrl,
                VersionNumber = nextVersion,
                UploadedAt = now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Nộp bài thành công! Đang chờ giảng viên xem xét.";
            return RedirectToAction("Timeline", "Student");
        }

        // ══════════════════════════════════════
        // LECTURER: QUẢN LÝ TIMELINE
        // ══════════════════════════════════════
        [Authorize(Roles = "Lecturer")]
        public IActionResult TimelineManagement()
        {
            return RedirectToAction("TimelineManagement", "Lecturer");
        }

        // ══════════════════════════════════════
        // LECTURER: DUYỆT / TỪ CHỐI SUBMISSION
        // Chỉ Lecturer mới có quyền này
        // ══════════════════════════════════════

        [Authorize(Roles = "Lecturer")]  // Admin KHÔNG được gọi action này
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewSubmission(
            int submissionId,
            string action,       // "approve" | "reject"
            string? comment,
            double? score)
        {
            var now = DateTime.Now;

            var submission = await _context.TimelineSubmissions
                .Include(s => s.Timeline)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            if (submission.Timeline == null)
            {
                TempData["Error"] = "Không tìm thấy mốc thời gian của bài nộp.";
                return RedirectToLecturerTimeline();
            }

            // Guard 1: còn trong ReviewDeadline không
            if (submission.Timeline.ReviewDeadline.HasValue
                && now > submission.Timeline.ReviewDeadline.Value)
            {
                TempData["Error"] =
                    $"Đã quá hạn duyệt ({submission.Timeline.ReviewDeadline.Value:dd/MM/yyyy HH:mm}). " +
                    "Không thể duyệt hoặc từ chối nữa — Admin sẽ tổng hợp kết quả.";
                return RedirectToLecturerTimeline();
            }

            // Guard 2: chỉ xử lý bản Pending
            if (submission.Status != SubmissionStatus.Pending)
            {
                TempData["Error"] = "Bài này đã được xử lý trước đó.";
                return RedirectToLecturerTimeline();
            }

            // Guard 3: GV chỉ duyệt SV thuộc đề tài mình
            var lecturerId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(lecturerId) || string.IsNullOrEmpty(submission.StudentId))
            {
                TempData["Error"] = "Không xác định được giảng viên hoặc sinh viên của bài nộp.";
                return RedirectToLecturerTimeline();
            }

            var studentId = submission.StudentId;
            bool isMyStudent = await _context.Registrations.AnyAsync(r =>
                r.Topic.LecturerId == lecturerId &&
                r.StudentId == studentId &&
                r.Status == "Approved");

            if (!isMyStudent)
            {
                TempData["Error"] = "Bạn không có quyền duyệt bài của sinh viên này.";
                return RedirectToLecturerTimeline();
            }

            action = action?.Trim().ToLowerInvariant() ?? string.Empty;

            if (action == "approve")
            {
                if (score.HasValue && (score.Value < 0 || score.Value > 10))
                {
                    TempData["Error"] = "Điểm phải nằm trong khoảng 0 đến 10.";
                    return RedirectToLecturerTimeline();
                }

                submission.Status = SubmissionStatus.Approved;
                submission.Comment = comment?.Trim();
                submission.Score = score;
                submission.ReviewedAt = now;
                submission.ReviewedById = lecturerId;
                submission.IsCompleted = true;
                submission.LecturerComment = null;

                _context.Notifications.Add(new Notification
                {
                    UserId = studentId,
                    Title = "Tiến độ đã được duyệt",
                    Content = $"Mốc \"{submission.Timeline.Title}\" đã được duyệt.",
                    Type = "Timeline",
                    RedirectUrl = "/Student/Timeline",
                    IsRead = false,
                    CreatedAt = now
                });

                TempData["Success"] = $"Đã duyệt bài của {submission.Student?.FullName}.";
            }
            else if (action == "reject")
            {
                comment = comment?.Trim();
                if (string.IsNullOrWhiteSpace(comment) || comment.Length < 15)
                {
                    TempData["Error"] = "Vui lòng nhập lý do từ chối tối thiểu 15 ký tự.";
                    return RedirectToLecturerTimeline();
                }

                submission.Status = SubmissionStatus.Rejected;
                submission.Comment = comment;
                submission.LecturerComment = comment;
                submission.Score = null;
                submission.ReviewedAt = now;
                submission.ReviewedById = lecturerId;
                submission.IsCompleted = false;

                _context.Notifications.Add(new Notification
                {
                    UserId = studentId,
                    Title = "Tiến độ bị từ chối",
                    Content = $"Mốc \"{submission.Timeline.Title}\" chưa đạt yêu cầu.",
                    Type = "Timeline",
                    RedirectUrl = "/Student/Timeline",
                    IsRead = false,
                    CreatedAt = now
                });

                // Thông báo có thể nộp lại không
                bool canResubmit =
                    !submission.Timeline.SubmissionDeadline.HasValue ||
                    now <= submission.Timeline.SubmissionDeadline.Value;

                TempData["Success"] = canResubmit
                    ? $"Đã từ chối bài của {submission.Student?.FullName}. Sinh viên có thể nộp lại trong hạn."
                    : $"Đã từ chối bài của {submission.Student?.FullName}. Hạn nộp đã qua, sinh viên không thể nộp lại.";
            }
            else
            {
                TempData["Error"] = "Hành động không hợp lệ.";
                return RedirectToLecturerTimeline();
            }

            await _context.SaveChangesAsync();
            return RedirectToLecturerTimeline();
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportApprovedSubmissions(string? keyword, string? timelineId, string? semester = null, string? year = null)
        {
            var selectedPeriod = await GetSelectedPeriodForTimelineAsync(semester, year);
            int? selectedTimelineId = ResolveTimelineId(timelineId);

            if (!selectedTimelineId.HasValue)
            {
                TempData["Error"] = "Vui lòng chọn mốc thời gian trước khi xuất Excel.";
                return RedirectToAction(nameof(ApprovedSubmissions), new { keyword, semester, year });
            }

            keyword = keyword?.Trim();

            var timeline = await _context.Timelines
                .FirstOrDefaultAsync(t => t.Id == selectedTimelineId.Value);

            if (timeline == null || (selectedPeriod != null && timeline.RegistrationPeriodId != selectedPeriod.Id))
            {
                TempData["Error"] = "Không tìm thấy mốc thời gian.";
                return RedirectToAction(nameof(ApprovedSubmissions), new { keyword, semester, year });
            }

            int timelineFilterId = selectedTimelineId.Value;
            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Include(s => s.ReviewedBy)
                .Where(s =>
                    s.Status == SubmissionStatus.Approved &&
                    s.TimelineId == timelineFilterId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(s =>
                    (s.Student != null && (s.Student.FullName ?? "").Contains(keyword)) ||
                    (s.Student != null && (s.Student.UserCode ?? "").Contains(keyword)));
            }

            var data = await query
                .OrderBy(s => s.Student != null ? s.Student.UserCode : "")
                .ThenBy(s => s.Student != null ? s.Student.FullName : "")
                .ToListAsync();

            using var workbook = new XLWorkbook();

            var ws = workbook.Worksheets.Add("Danh sach da duyet");

            ws.Cell(1, 1).Value = "DANH SÁCH BÀI / ĐỀ TÀI ĐÃ DUYỆT";
            ws.Range(1, 1, 1, 11).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = "Mốc thời gian:";
            ws.Cell(2, 2).Value = timeline.Title;

            ws.Cell(3, 1).Value = "Ngày xuất:";
            ws.Cell(3, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            int headerRow = 5;

            ws.Cell(headerRow, 1).Value = "STT";
            ws.Cell(headerRow, 2).Value = "Mã sinh viên";
            ws.Cell(headerRow, 3).Value = "Họ tên sinh viên";
            ws.Cell(headerRow, 4).Value = "Mốc thời gian";
            ws.Cell(headerRow, 5).Value = "Ngày duyệt";
            ws.Cell(headerRow, 6).Value = "Giảng viên duyệt";
            ws.Cell(headerRow, 7).Value = "Điểm";
            ws.Cell(headerRow, 8).Value = "Nhận xét";
            ws.Cell(headerRow, 9).Value = "Trạng thái";
            ws.Cell(headerRow, 10).Value = "File";
            ws.Cell(headerRow, 11).Value = "Email";

            var header = ws.Range(headerRow, 1, headerRow, 11);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = headerRow + 1;
            int stt = 1;

            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = item.Student?.UserCode ?? "";
                ws.Cell(row, 3).Value = item.Student?.FullName ?? "";
                ws.Cell(row, 4).Value = timeline.Title;
                ws.Cell(row, 5).Value = item.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                ws.Cell(row, 6).Value = item.ReviewedBy?.FullName ?? item.ReviewedBy?.Email ?? "";
                ws.Cell(row, 7).Value = item.Score;
                ws.Cell(row, 8).Value = item.Comment ?? "";
                ws.Cell(row, 9).Value = "Đã duyệt";
                ws.Cell(row, 10).Value = Url.Action(
                    nameof(DownloadSubmissionFile),
                    "Timeline",
                    new { submissionId = item.Id },
                    Request.Scheme) ?? "";
                ws.Cell(row, 11).Value = item.Student?.Email ?? "";

                row++;
            }

            ws.Range(headerRow, 1, Math.Max(headerRow, row - 1), 11).SetAutoFilter();
            ws.SheetView.FreezeRows(headerRow);
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var invalidChars = Path.GetInvalidFileNameChars();
            string safeTimelineName = new string(timeline.Title
                .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
                .ToArray())
                .Trim();

            if (string.IsNullOrWhiteSpace(safeTimelineName))
            {
                safeTimelineName = $"Timeline-{timeline.Id}";
            }

            string fileName = $"Danh-sach-da-duyet-{safeTimelineName}-{DateTime.Now:yyyyMMddHHmm}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        private IActionResult RedirectToLecturerTimeline()
        {
            return RedirectToAction("TimelineManagement", "Lecturer");
        }

        private string? GetAdminSelectedPeriodName()
        {
            return HttpContext.Session.GetString("AdminSelectedPeriodName");
        }

        private async Task<RegistrationPeriod?> GetSelectedPeriodForTimelineAsync(string? semester = null, string? year = null)
        {
            if (User.IsInRole("Admin")
                && !string.IsNullOrWhiteSpace(semester)
                && !string.IsNullOrWhiteSpace(year))
            {
                var periodName = $"{semester.Trim()}-{year.Trim()}";
                HttpContext.Session.SetString("AdminSelectedPeriodName", periodName);
                ViewBag.AdminSelectedPeriodName = periodName;
                ViewBag.AdminSelectedSemester = semester.Trim();
                ViewBag.AdminSelectedYear = year.Trim();
            }

            if (User.IsInRole("Admin"))
            {
                var selectedPeriodName = GetAdminSelectedPeriodName();
                if (!string.IsNullOrWhiteSpace(selectedPeriodName))
                {
                    return await _context.RegistrationPeriods
                        .FirstOrDefaultAsync(p => p.Name == selectedPeriodName);
                }
            }

            return await GetOrCreateActiveRegistrationPeriodAsync();
        }

        private static string? ValidateTimelineInput(
            string? title,
            DateTime date,
            DateTime? submissionDeadline,
            DateTime? reviewDeadline,
            bool allowSubmission)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Tên mốc thời gian không được để trống.";
            }

            if (date == default)
            {
                return "Ngày hiển thị của mốc thời gian không hợp lệ.";
            }

            if (allowSubmission && !submissionDeadline.HasValue)
            {
                return "Mốc cho phép sinh viên nộp bài cần có hạn cuối nộp bài.";
            }

            if (allowSubmission && submissionDeadline.HasValue && date >= submissionDeadline.Value)
            {
                return "Thời điểm mở nộp phải trước hạn cuối sinh viên nộp bài.";
            }

            if (allowSubmission
                && submissionDeadline.HasValue && reviewDeadline.HasValue
                && reviewDeadline.Value <= submissionDeadline.Value)
            {
                return "Hạn GV duyệt phải sau hạn SV nộp bài.";
            }

            return null;
        }

        [Authorize(Roles = "Admin,Lecturer,Student")]
        public async Task<IActionResult> DownloadSubmissionFile(int submissionId, int? versionId = null)
        {
            var submission = await _context.TimelineSubmissions
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var canAccess = User.IsInRole("Admin")
                || (User.IsInRole("Student") && submission.StudentId == currentUserId)
                || (User.IsInRole("Lecturer")
                    && !string.IsNullOrEmpty(submission.StudentId)
                    && await _context.Registrations.AnyAsync(r =>
                        r.StudentId == submission.StudentId &&
                        r.Status == "Approved" &&
                        r.Topic != null &&
                        r.Topic.LecturerId == currentUserId));

            if (!canAccess) return Forbid();

            var filePath = submission.FilePath;
            var downloadName = submission.FileName;

            if (versionId.HasValue)
            {
                var version = submission.Versions.FirstOrDefault(v => v.Id == versionId.Value);
                if (version == null) return NotFound();

                filePath = version.FilePath;
                downloadName = version.FileName;
            }

            var storedName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(storedName)) return NotFound();

            var uploadsRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "timeline"));

            var physicalPath = Path.GetFullPath(Path.Combine(uploadsRoot, storedName));

            if (!physicalPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(physicalPath))
            {
                return NotFound();
            }

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(physicalPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(physicalPath, contentType, Path.GetFileName(downloadName ?? storedName));
        }

        private static string? NormalizeSubmissionType(string? submissionType)
        {
            var allowed = ParseAllowedExtensions(submissionType);

            return allowed.Count == 0
                ? null
                : string.Join(", ", allowed.Select(e => e.TrimStart('.')));
        }

        private static HashSet<string> GetAllowedExtensions(Timeline timeline)
        {
            var configured = ParseAllowedExtensions(timeline.SubmissionType);
            return configured.Count > 0 ? configured : DefaultAllowedExtensions();
        }

        private static HashSet<string> ParseAllowedExtensions(string? submissionType)
        {
            var defaults = DefaultAllowedExtensions();
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(submissionType))
            {
                return result;
            }

            var tokens = submissionType
                .Split(new[] { ',', ';', '|', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('*').ToLowerInvariant());

            foreach (var token in tokens)
            {
                var extension = token.StartsWith(".") ? token : "." + token;
                if (defaults.Contains(extension))
                {
                    result.Add(extension);
                }
            }

            return result;
        }

        private static HashSet<string> DefaultAllowedExtensions()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".rar", ".txt"
            };
        }

        private static string FormatAllowedExtensions(IEnumerable<string> extensions)
        {
            return string.Join(", ", extensions
                .OrderBy(e => e)
                .Select(e => e.TrimStart('.').ToUpperInvariant()));
        }
    }
}
