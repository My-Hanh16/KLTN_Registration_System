using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class TimelineController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TimelineController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ══════════════════════════════════════
        // VIEW TIMELINE (Admin + Lecturer)
        // ══════════════════════════════════════
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
            if (submissionDeadline.HasValue && reviewDeadline.HasValue
                && reviewDeadline.Value <= submissionDeadline.Value)
            {
                TempData["Error"] = "Hạn GV duyệt phải sau hạn SV nộp bài.";
                return RedirectToAction(nameof(Index));
            }

            _context.Timelines.Add(new Timeline
            {
                Title = title,
                Description = description,
                Date = date,
                Type = type,
                SubmissionDeadline = submissionDeadline,
                ReviewDeadline = reviewDeadline,
                SubmissionType = submissionType,
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

            if (submissionDeadline.HasValue && reviewDeadline.HasValue
                && reviewDeadline.Value <= submissionDeadline.Value)
            {
                TempData["Error"] = "Hạn GV duyệt phải sau hạn SV nộp bài.";
                return RedirectToAction(nameof(Index));
            }

            tl.Title = title;
            tl.Description = description;
            tl.Date = date;
            tl.Type = type;
            tl.SubmissionDeadline = submissionDeadline;
            tl.ReviewDeadline = reviewDeadline;
            tl.SubmissionType = submissionType;
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
            var tl = await _context.Timelines.FindAsync(id);
            if (tl == null) return NotFound();

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
            int? timelineId,
            int page = 1)
        {
            const int pageSize = 15;

            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Where(s => s.Status == SubmissionStatus.Approved)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(s =>
                    s.Student.FullName.Contains(keyword) ||
                    s.Timeline.Title.Contains(keyword));

            if (timelineId.HasValue)
                query = query.Where(s => s.TimelineId == timelineId.Value);

            var total = await query.CountAsync();

            var submissions = await query
                .OrderByDescending(s => s.ReviewedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Timelines = await _context.Timelines.OrderBy(t => t.Date).ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Keyword = keyword;
            ViewBag.TimelineId = timelineId;
            ViewBag.TotalApproved = total;

            return View(submissions);
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
                    .ThenInclude(t => t.TimelineSubmissions)
                        .ThenInclude(ts => ts.Student)
                .Where(s =>
                    s.Status == SubmissionStatus.Pending &&
                    s.Timeline.ReviewDeadline.HasValue &&
                    s.Timeline.ReviewDeadline.Value < now)
                .AsQueryable();

            if (timelineId.HasValue)
                query = query.Where(s => s.TimelineId == timelineId.Value);

            var overdue = await query
                .OrderBy(s => s.Timeline.ReviewDeadline)
                .ThenBy(s => s.Student.FullName)
                .ToListAsync();

            ViewBag.Timelines = await _context.Timelines
                .Where(t => t.ReviewDeadline.HasValue && t.ReviewDeadline.Value < now)
                .OrderBy(t => t.Date)
                .ToListAsync();

            ViewBag.TimelineId = timelineId;

            // Thống kê nhanh cho Admin
            ViewBag.TotalOverdue = overdue.Count;
            ViewBag.AffectedLecturers = overdue
                .Where(s => s.Timeline != null)
                .Select(s => s.Timeline.Id)
                .Distinct()
                .Count();

            return View(overdue);
        }

        /// <summary>
        /// Export danh sách kết quả đã duyệt ra file Excel/CSV.
        /// Admin dùng để tổng hợp cuối kỳ.
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportApproved(int? timelineId)
        {
            var query = _context.TimelineSubmissions
                .Include(s => s.Student)
                .Include(s => s.Timeline)
                .Where(s => s.Status == SubmissionStatus.Approved)
                .AsQueryable();

            if (timelineId.HasValue)
                query = query.Where(s => s.TimelineId == timelineId.Value);

            var submissions = await query
                .OrderBy(s => s.Timeline.Date)
                .ThenBy(s => s.Student.FullName)
                .ToListAsync();

            // Tạo CSV đơn giản
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("Mốc thời gian,MSSV,Họ tên,Email,Ngày nộp,Điểm,Nhận xét,Ngày duyệt");

            foreach (var s in submissions)
            {
                lines.AppendLine(string.Join(",",
                    $"\"{s.Timeline?.Title}\"",
                    $"\"{s.Student?.UserCode}\"",
                    $"\"{s.Student?.FullName}\"",
                    $"\"{s.Student?.Email}\"",
                    s.SubmittedAt.ToString("dd/MM/yyyy HH:mm"),
                    s.Score?.ToString() ?? "",
                    $"\"{s.Comment?.Replace("\"", "'")}\"",
                    s.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? ""
                ));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                          .Concat(System.Text.Encoding.UTF8.GetBytes(lines.ToString()))
                          .ToArray();

            var fileName = timelineId.HasValue
                ? $"ketqua_moctiengian_{timelineId}_{DateTime.Now:yyyyMMdd}.csv"
                : $"ketqua_tatca_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // ══════════════════════════════════════
        // STUDENT: NỘP BÀI
        // ══════════════════════════════════════
        [Authorize(Roles = "Student")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTimeline(
            int timelineId,
            IFormFile file)
        {
            var now = DateTime.Now;
            var tl = await _context.Timelines.FindAsync(timelineId);

            if (tl == null) return NotFound();

            // Guard 1: mốc có cho nộp không
            if (!tl.AllowSubmission)
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

            var studentId = _userManager.GetUserId(User)!;

            // Lưu file
            var uploadsDir = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "timeline");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{studentId}_{timelineId}_{now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var fileUrl = $"/uploads/timeline/{fileName}";

            // Tìm submission hiện tại
            var existing = await _context.TimelineSubmissions
                .FirstOrDefaultAsync(s =>
                    s.TimelineId == timelineId &&
                    s.StudentId == studentId);

            if (existing != null)
            {
                // Guard 3: chỉ nộp lại khi bị Rejected
                if (existing.Status != SubmissionStatus.Rejected)
                {
                    TempData["Error"] = existing.Status == SubmissionStatus.Pending
                        ? "Bài của bạn đang chờ giảng viên xem xét. Không thể nộp thêm."
                        : "Bài của bạn đã được duyệt. Không thể nộp lại.";
                    return RedirectToAction("Timeline", "Student");
                }

                // Nộp lại sau khi bị từ chối
                existing.FilePath = fileUrl;
                existing.FileName = file.FileName;
                existing.SubmittedAt = now;
                existing.Status = SubmissionStatus.Pending;
                existing.Comment = null;
                existing.Score = null;
                existing.ReviewedAt = null;
            }
            else
            {
                // Nộp lần đầu
                _context.TimelineSubmissions.Add(new TimelineSubmission
                {
                    TimelineId = timelineId,
                    StudentId = studentId,
                    FilePath = fileUrl,
                    FileName = file.FileName,
                    SubmittedAt = now,
                    Status = SubmissionStatus.Pending
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Nộp bài thành công! Đang chờ giảng viên xem xét.";
            return RedirectToAction("Timeline", "Student");
        }

        // ══════════════════════════════════════
        // LECTURER: QUẢN LÝ TIMELINE
        // ══════════════════════════════════════
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> TimelineManagement()
        {
            var lecturerId = _userManager.GetUserId(User);

            // Chỉ load SV thuộc đề tài GV đang hướng dẫn
            var myStudentIds = await _context.Registrations
                .Where(r => r.Topic.LecturerId == lecturerId && r.Status == "Approved")
                .Select(r => r.StudentId)
                .Distinct()
                .ToListAsync();

            var timelines = await _context.Timelines
                .Where(t => t.IsActive)
                .Include(t => t.TimelineSubmissions
                    .Where(s => myStudentIds.Contains(s.StudentId)))
                    .ThenInclude(s => s.Student)
                .OrderBy(t => t.Date)
                .ToListAsync();

            var now = DateTime.Now;
            ViewBag.MyStudentIds = myStudentIds;
            ViewBag.Now = now;

            return View(timelines);
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

            // Guard 1: còn trong ReviewDeadline không
            if (submission.Timeline.ReviewDeadline.HasValue
                && now > submission.Timeline.ReviewDeadline.Value)
            {
                TempData["Error"] =
                    $"Đã quá hạn duyệt ({submission.Timeline.ReviewDeadline.Value:dd/MM/yyyy HH:mm}). " +
                    "Không thể duyệt hoặc từ chối nữa — Admin sẽ tổng hợp kết quả.";
                return RedirectToAction(nameof(TimelineManagement));
            }

            // Guard 2: chỉ xử lý bản Pending
            if (submission.Status != SubmissionStatus.Pending)
            {
                TempData["Error"] = "Bài này đã được xử lý trước đó.";
                return RedirectToAction(nameof(TimelineManagement));
            }

            // Guard 3: GV chỉ duyệt SV thuộc đề tài mình
            var lecturerId = _userManager.GetUserId(User);
            bool isMyStudent = await _context.Registrations.AnyAsync(r =>
                r.Topic.LecturerId == lecturerId &&
                r.StudentId == submission.StudentId &&
                r.Status == "Approved");

            if (!isMyStudent)
            {
                TempData["Error"] = "Bạn không có quyền duyệt bài của sinh viên này.";
                return RedirectToAction(nameof(TimelineManagement));
            }

            if (action == "approve")
            {
                submission.Status = SubmissionStatus.Approved;
                submission.Comment = comment;
                submission.Score = score;
                submission.ReviewedAt = now;
                TempData["Success"] = $"Đã duyệt bài của {submission.Student?.FullName}.";
            }
            else if (action == "reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                submission.Comment = comment;
                submission.ReviewedAt = now;

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
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(TimelineManagement));
        }
    }
}