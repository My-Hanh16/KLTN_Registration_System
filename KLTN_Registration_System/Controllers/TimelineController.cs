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

        // ══════════════════════════════════════
        // VIEW TIMELINE (Admin + Lecturer)
        // ══════════════════════════════════════
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Index()
        {
            var timelines = await _context.Timelines
                .Include(t => t.TimelineSubmissions)
                    .ThenInclude(s => s.Student)
                .OrderBy(t => t.Date)
                .ToListAsync();

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

            _context.Timelines.Add(new Timeline
            {
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
            int page = 1)
        {
            const int pageSize = 15;

            keyword = keyword?.Trim();
            page = Math.Max(page, 1);
            int? selectedTimelineIdFromRequest = ResolveTimelineId(timelineId);

            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Include(s => s.ReviewedBy)
                .Where(s => s.Status == SubmissionStatus.Approved)
                .AsQueryable();

            var timelines = await _context.Timelines
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
        public async Task<IActionResult> PendingAfterDeadline(int? timelineId)
        {
            var now = DateTime.Now;

            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Where(s =>
                    s.Status == SubmissionStatus.Pending &&
                    s.Timeline != null &&
                    s.Timeline.ReviewDeadline.HasValue &&
                    s.Timeline.ReviewDeadline.Value < now)
                .AsQueryable();

            if (timelineId.HasValue)
                query = query.Where(s => s.TimelineId == timelineId.Value);

            var overdue = await query
                .OrderBy(s => s.Timeline != null ? s.Timeline.ReviewDeadline : null)
                .ThenBy(s => s.Student != null ? s.Student.FullName : "")
                .ToListAsync();

            ViewBag.Timelines = await _context.Timelines
                .Where(t => t.ReviewDeadline.HasValue && t.ReviewDeadline.Value < now)
                .OrderBy(t => t.Date)
                .ToListAsync();

            ViewBag.TimelineId = timelineId;

            // Thống kê nhanh cho Admin
            ViewBag.TotalOverdue = overdue.Count;
            var overdueStudentIds = overdue
                .Where(s => !string.IsNullOrEmpty(s.StudentId))
                .Select(s => s.StudentId!)
                .Distinct()
                .ToList();

            ViewBag.AffectedLecturers = await _context.Registrations
                .Where(r => overdueStudentIds.Contains(r.StudentId) &&
                    r.Status == "Approved" &&
                    r.Topic != null &&
                    r.Topic.LecturerId != null)
                .Select(r => r.Topic.LecturerId)
                .Distinct()
                .CountAsync();

            return View(overdue);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SubmissionDetail(int id)
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
                return RedirectToAction(nameof(ApprovedSubmissions));
            }

            submission.Versions = submission.Versions
                .OrderByDescending(v => v.VersionNumber)
                .ThenByDescending(v => v.UploadedAt)
                .ToList();

            return View(submission);
        }

        /// <summary>
        /// Export danh sách kết quả đã duyệt ra file Excel/CSV.
        /// Admin dùng để tổng hợp cuối kỳ.
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportApproved(int? timelineId)
        {
            return await ExportApprovedSubmissions(null, timelineId?.ToString());
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

            // Guard 2: còn trong SubmissionDeadline không
            if (tl.SubmissionDeadline.HasValue && now > tl.SubmissionDeadline.Value)
            {
                TempData["Error"] =
                    $"Đã quá hạn nộp bài ({tl.SubmissionDeadline.Value:dd/MM/yyyy HH:mm}). Không thể nộp.";
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
                .FirstOrDefaultAsync(s =>
                    s.TimelineId == timelineId &&
                    s.StudentId == studentId);

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
        public async Task<IActionResult> ExportApprovedSubmissions(string? keyword, string? timelineId)
        {
            int? selectedTimelineId = ResolveTimelineId(timelineId);

            if (!selectedTimelineId.HasValue)
            {
                TempData["Error"] = "Vui lòng chọn mốc thời gian trước khi xuất Excel.";
                return RedirectToAction(nameof(ApprovedSubmissions), new { keyword });
            }

            keyword = keyword?.Trim();

            var timeline = await _context.Timelines
                .FirstOrDefaultAsync(t => t.Id == selectedTimelineId.Value);

            if (timeline == null)
            {
                TempData["Error"] = "Không tìm thấy mốc thời gian.";
                return RedirectToAction(nameof(ApprovedSubmissions), new { keyword });
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

            if (submissionDeadline.HasValue && reviewDeadline.HasValue
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
