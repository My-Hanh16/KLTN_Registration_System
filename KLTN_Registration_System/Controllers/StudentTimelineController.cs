using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using KLTN_Registration_System.Models.Enums;

namespace KLTN_Registration_System.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentTimelineController : Controller
    {
        private readonly AppDbContext _context;

        public StudentTimelineController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // DANH SÁCH TIMELINE
        // =========================
        public async Task<IActionResult> Index()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var timelines = await _context.Timelines
                .Include(t => t.TimelineSubmissions)
                    .ThenInclude(s => s.Versions)
                .OrderBy(t => t.Date)
                .ToListAsync();

            // Submission của sinh viên hiện tại
            var submissions = await _context.TimelineSubmissions
                .Include(x => x.Versions)
                .Where(x => x.StudentId == studentId)
                .ToListAsync();

            ViewBag.Submissions = submissions;

            ViewBag.StudentId = studentId;

            return View(timelines);
        }

        // =========================
        // NỘP BÁO CÁO
        // =========================
        [HttpPost]
        public async Task<IActionResult> Submit(
    int timelineId,
    string content,
    IFormFile? file)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Tìm submission cũ
            var submission = await _context.TimelineSubmissions
                .Include(x => x.Versions)
                .FirstOrDefaultAsync(x =>
                    x.TimelineId == timelineId &&
                    x.StudentId == studentId);

            // Nếu chưa có thì tạo mới
            if (submission == null)
            {
                submission = new TimelineSubmission
                {
                    StudentId = studentId,
                    TimelineId = timelineId,
                    SubmittedAt = DateTime.Now,
                    Status = SubmissionStatus.Pending
                };

                _context.TimelineSubmissions.Add(submission);

                await _context.SaveChangesAsync();
            }

            // Update nội dung mới nhất
            submission.ProgressDescription = content;

            submission.SubmittedAt = DateTime.Now;

            // Reset review
            submission.Status = SubmissionStatus.Pending;

            submission.Comment = null;

            submission.Score = null;

            submission.ReviewedAt = null;

            submission.ReviewedById = null;

            // Upload file
            if (file != null && file.Length > 0)
            {
                var folder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/uploads/progress");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Version mới
                int nextVersion = submission.Versions.Count + 1;

                var extension = Path.GetExtension(file.FileName);

                var fileName =
                    $"timeline_{timelineId}_student_{studentId}_v{nextVersion}{extension}";

                var fullPath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var filePath = "/uploads/progress/" + fileName;

                // Lưu latest version
                submission.FilePath = filePath;

                submission.FileName = fileName;

                // Add history version
                var version = new TimelineSubmissionVersion
                {
                    TimelineSubmissionId = submission.Id,

                    FileName = fileName,

                    FilePath = filePath,

                    VersionNumber = nextVersion,

                    UploadedAt = DateTime.Now,

                    Note = content
                };

                _context.TimelineSubmissionVersions.Add(version);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Nộp báo cáo thành công!";

            return RedirectToAction(nameof(Index));
        }
    }
}