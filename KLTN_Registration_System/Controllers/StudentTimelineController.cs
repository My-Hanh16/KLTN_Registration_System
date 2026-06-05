using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KLTN_Registration_System.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentTimelineController : BaseController
    {
        private readonly AppDbContext _context;

        public StudentTimelineController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            var timelines = await FilterTimelinesByActivePeriod(_context.Timelines, activePeriod)
                .Where(t => t.IsActive)
                .OrderBy(t => t.Date)
                .ToListAsync();

            var approvedRegistration = await FilterRegistrationsByActivePeriod(
                    _context.Registrations.Include(r => r.Topic),
                    activePeriod)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId &&
                    r.Status == "Approved");

            var timelineIds = timelines.Select(t => t.Id).ToList();
            var submissions = await _context.TimelineSubmissions
                .Include(x => x.Versions)
                .Where(x => x.StudentId == studentId && timelineIds.Contains(x.TimelineId))
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);

            ViewBag.Submissions = submissions;
            ViewBag.StudentId = studentId;
            ViewBag.HasApprovedTopic = approvedRegistration != null;
            ViewBag.ActiveTopicId = approvedRegistration?.TopicId;
            ViewBag.ActivePeriod = activePeriod;
            ViewBag.HasCompletedThesis = user?.HasCompletedThesis == true;
            await SetStudentLayoutData();

            return View(timelines);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit(
            int timelineId,
            string? content,
            IFormFile? file)
        {
            var now = DateTime.Now;
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(studentId)) return Unauthorized();

            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user?.HasCompletedThesis == true)
            {
                TempData["Error"] = "Bạn đã hoàn thành khóa luận nên không cần nộp timeline nữa.";
                return RedirectToAction(nameof(Index));
            }

            var timeline = await _context.Timelines.FindAsync(timelineId);
            if (timeline == null)
            {
                TempData["Error"] = "Không tìm thấy mốc thời gian.";
                return RedirectToAction(nameof(Index));
            }

            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (timeline.RegistrationPeriodId.HasValue && timeline.RegistrationPeriodId.Value != activePeriod.Id)
            {
                TempData["Error"] = "Mốc thời gian này không thuộc đợt khóa luận hiện tại.";
                return RedirectToAction(nameof(Index));
            }

            if (!timeline.IsActive || !timeline.AllowSubmission)
            {
                TempData["Error"] = "Mốc này chưa cho phép nộp báo cáo.";
                return RedirectToAction(nameof(Index));
            }

            var hasApprovedTopic = await FilterRegistrationsByActivePeriod(
                    _context.Registrations.Include(r => r.Topic),
                    activePeriod)
                .AnyAsync(r =>
                    r.StudentId == studentId &&
                    r.Status == "Approved");

            if (!hasApprovedTopic)
            {
                TempData["Error"] = "Bạn cần có đề tài đã được duyệt trước khi nộp báo cáo tiến độ.";
                return RedirectToAction(nameof(Index));
            }

            var submission = await _context.TimelineSubmissions
                .Include(x => x.Versions)
                .Where(x =>
                    x.TimelineId == timelineId &&
                    x.StudentId == studentId)
                .OrderByDescending(x => x.SubmittedAt)
                .FirstOrDefaultAsync();

            bool isRejectedResubmission = submission?.Status == SubmissionStatus.Rejected;
            if (timeline.Date > now && !isRejectedResubmission)
            {
                TempData["Error"] = $"Mốc này chưa mở nộp. Thời gian mở: {timeline.Date:dd/MM/yyyy HH:mm}.";
                return RedirectToAction(nameof(Index));
            }

            if (timeline.SubmissionDeadline.HasValue
                && now > timeline.SubmissionDeadline.Value
                && !isRejectedResubmission)
            {
                TempData["Error"] = $"Đã quá hạn nộp ({timeline.SubmissionDeadline.Value:dd/MM/yyyy HH:mm}).";
                return RedirectToAction(nameof(Index));
            }

            if (submission != null && submission.Status != SubmissionStatus.Rejected)
            {
                TempData["Error"] = submission.Status == SubmissionStatus.Pending
                    ? "Bài nộp đang chờ giảng viên duyệt, không thể nộp thêm."
                    : "Bài nộp đã được duyệt, không thể nộp lại.";
                return RedirectToAction(nameof(Index));
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file báo cáo để nộp.";
                return RedirectToAction(nameof(Index));
            }

            if (file.Length > 20 * 1024 * 1024)
            {
                TempData["Error"] = "File báo cáo tối đa 20MB.";
                return RedirectToAction(nameof(Index));
            }

            var allowedExt = GetAllowedTimelineExtensions(timeline.SubmissionType);
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExt.Contains(extension))
            {
                TempData["Error"] = $"Định dạng file không hợp lệ. Chỉ hỗ trợ: {FormatTimelineExtensions(allowedExt)}.";
                return RedirectToAction(nameof(Index));
            }

            if (submission == null)
            {
                submission = new TimelineSubmission
                {
                    StudentId = studentId,
                    TimelineId = timelineId,
                    SubmittedAt = now,
                    Status = SubmissionStatus.Pending
                };

                _context.TimelineSubmissions.Add(submission);

                await _context.SaveChangesAsync();
            }

            submission.ProgressDescription = content?.Trim();

            submission.SubmittedAt = now;

            submission.Status = SubmissionStatus.Pending;

            submission.Comment = null;

            submission.Score = null;

            submission.ReviewedAt = null;

            submission.ReviewedById = null;

            submission.LecturerComment = null;

            submission.IsCompleted = false;

            var folder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot", "uploads", "timeline");

            Directory.CreateDirectory(folder);

            int nextVersion = submission.Versions.Any()
                ? submission.Versions.Max(v => v.VersionNumber) + 1
                : 1;
            var fileName = $"timeline_{timelineId}_student_{studentId}_v{nextVersion}_{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var filePath = "/uploads/timeline/" + fileName;

            submission.FilePath = filePath;
            submission.FileName = Path.GetFileName(file.FileName);

            _context.TimelineSubmissionVersions.Add(new TimelineSubmissionVersion
            {
                TimelineSubmissionId = submission.Id,
                FileName = submission.FileName,
                FilePath = filePath,
                VersionNumber = nextVersion,
                UploadedAt = now,
                Note = submission.ProgressDescription
            });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Nộp báo cáo thành công!";

            return RedirectToAction(nameof(Index));
        }

        private static HashSet<string> GetAllowedTimelineExtensions(string? submissionType)
        {
            var defaults = DefaultTimelineExtensions();
            var configured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(submissionType))
            {
                var tokens = submissionType
                    .Split(new[] { ',', ';', '|', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().TrimStart('*').ToLowerInvariant());

                foreach (var token in tokens)
                {
                    var extension = token.StartsWith(".") ? token : "." + token;
                    if (defaults.Contains(extension))
                    {
                        configured.Add(extension);
                    }
                }
            }

            return configured.Count > 0 ? configured : defaults;
        }

        private static HashSet<string> DefaultTimelineExtensions()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".rar", ".txt"
            };
        }

        private static string FormatTimelineExtensions(IEnumerable<string> extensions)
        {
            return string.Join(", ", extensions
                .OrderBy(e => e)
                .Select(e => e.TrimStart('.').ToUpperInvariant()));
        }
    }
}
