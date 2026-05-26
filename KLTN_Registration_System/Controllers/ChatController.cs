using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly IWebHostEnvironment _env;
        private readonly NotificationService _notifService;

        public ChatController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            NotificationService notifService)
            : base(db, userManager)
        {
            _env = env;
            _notifService = notifService;
        }

        // =====================================================
        // CHAT HOME
        // =====================================================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            // =====================================================
            // LECTURER
            // =====================================================
            if (roles.Contains("Lecturer"))
            {
                var topics = await FilterTopicsByActivePeriod(_db.Topics, activePeriod)
                    .Where(t => t.LecturerId == user.Id)
                    .Where(t => t.Registrations!.Any(r => r.Status == "Approved"))
                    .Include(t => t.Registrations!)
                        .ThenInclude(r => r.Student)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var topicIds = topics.Select(t => t.Id).ToList();

                // COUNT UNREAD TRỰC TIẾP DB
                var unreadMap = await _db.TopicComments
                    .Where(c =>
                        topicIds.Contains(c.TopicId) &&
                        !c.IsDeleted &&
                        !c.IsRead &&
                        c.SenderId != user.Id)
                    .GroupBy(c => c.TopicId)
                    .Select(g => new
                    {
                        TopicId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(x => x.TopicId, x => x.Count);

                ViewBag.UnreadMap = unreadMap;

                return View("LecturerTopics", topics);
            }

            // =====================================================
            // STUDENT
            // =====================================================
            if (roles.Contains("Student"))
            {
                var topics = await FilterRegistrationsByActivePeriod(_db.Registrations, activePeriod)
                    .Include(r => r.Topic)
                        .ThenInclude(t => t.Lecturer)
                    .Include(r => r.Topic)
                        .ThenInclude(t => t.Major)
                    .Include(r => r.Topic)
                        .ThenInclude(t => t.Comments)

                    .Where(r =>
                        r.StudentId == user.Id &&
                        r.Status == "Approved" &&
                        r.Topic != null)

                    .Select(r => r.Topic!)
                    .ToListAsync();

                // unread từ lecturer/admin gửi cho student
                var topicIds = topics.Select(t => t.Id).ToList();

                var unreadMap = await _db.TopicComments
                    .AsNoTracking()
                    .Where(c =>
                        topicIds.Contains(c.TopicId) &&
                        !c.IsDeleted &&
                        !c.IsRead &&
                        c.SenderId != user.Id)
                    .GroupBy(c => c.TopicId)
                    .Select(g => new
                    {
                        TopicId = g.Key,
                        Count = g.Count()
                    })
                    .ToDictionaryAsync(x => x.TopicId, x => x.Count);

                ViewBag.UnreadMap = unreadMap;

                return View("StudentTopics", topics);
            }

            return RedirectToAction("Index", "Home");
        }

        // =====================================================
        // CHAT ROOM
        // =====================================================
        public async Task<IActionResult> Topic(int topicId)
        {
            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var topic = await FilterTopicsByActivePeriod(_db.Topics, activePeriod)
                .Include(t => t.Lecturer)
                .Include(t => t.Registrations!)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            // =================================================
            // CHECK PERMISSION
            // =================================================
            bool isLecturer =
                roles.Contains("Lecturer") &&
                topic.LecturerId == user.Id;

            bool isStudent =
                roles.Contains("Student") &&
                await FilterRegistrationsByActivePeriod(_db.Registrations, activePeriod).AnyAsync(r =>
                    r.TopicId == topic.Id &&
                    r.StudentId == user.Id &&
                    r.Status == "Approved");

            if (!isLecturer && !isStudent)
                return Forbid();

            // =================================================
            // LOAD MESSAGES (LAST 50)
            // =================================================
            var messages = await _db.TopicComments
                .Where(c => c.TopicId == topicId && !c.IsDeleted)
                .Include(c => c.Sender)
                .OrderByDescending(c => c.Id)
                .Take(50)
                .OrderBy(c => c.Id)
                .Select(c => new ChatMessageVM
                {
                    Id = c.Id,
                    SenderId = c.SenderId,

                    SenderName =
                        c.Sender != null
                            ? (c.Sender.FullName ?? c.Sender.UserName ?? "")
                            : "Unknown",

                    SenderRole = c.SenderRole ?? "Student",
                    Content = c.Content ?? "",

                    AttachmentUrl = c.AttachmentUrl,
                    AttachmentName = c.AttachmentName,

                    CreatedAt = c.CreatedAt,

                    CreatedAtFmt = c.CreatedAt
                        .ToLocalTime()
                        .ToString("HH:mm dd/MM/yyyy")
                })
                .ToListAsync();

            // =================================================
            // MARK AS READ
            // =================================================
            var unread = await _db.TopicComments
                .Where(c =>
                    c.TopicId == topicId &&
                    c.SenderId != user.Id &&
                    !c.IsDeleted &&
                    !c.IsRead)
                .ToListAsync();

            foreach (var m in unread)
                m.IsRead = true;

            if (unread.Count > 0)
                await _db.SaveChangesAsync();

            // =================================================
            // VIEWBAG
            // =================================================
            ViewBag.TopicId = topicId;
            ViewBag.TopicTitle = topic.Title;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.IsLecturer = isLecturer;
            ViewBag.Messages = messages;

            return View(topic);
        }

        // =====================================================
        // LOAD MORE
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> LoadMore(int topicId, int beforeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var topic = await FilterTopicsByActivePeriod(_db.Topics, activePeriod)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            bool canAccess =
                (roles.Contains("Lecturer") && topic.LecturerId == user.Id) ||
                (roles.Contains("Student") &&
                 await FilterRegistrationsByActivePeriod(_db.Registrations, activePeriod).AnyAsync(r =>
                     r.TopicId == topic.Id &&
                     r.StudentId == user.Id &&
                     r.Status == "Approved"));

            if (!canAccess)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bạn không có quyền tải file lên đề tài này." });

            var messages = await _db.TopicComments
                .Where(c =>
                    c.TopicId == topicId &&
                    !c.IsDeleted &&
                    c.Id < beforeId)
                .Include(c => c.Sender)
                .OrderByDescending(c => c.Id)
                .Take(30)
                .OrderBy(c => c.Id)
                .Select(c => new ChatMessageVM
                {
                    Id = c.Id,
                    SenderId = c.SenderId,

                    SenderName =
                        c.Sender != null
                            ? (c.Sender.FullName ?? c.Sender.UserName ?? "")
                            : "Unknown",

                    SenderRole = c.SenderRole ?? "Student",
                    Content = c.Content ?? "",

                    AttachmentUrl = c.AttachmentUrl,
                    AttachmentName = c.AttachmentName,

                    CreatedAt = c.CreatedAt,

                    CreatedAtFmt = c.CreatedAt
                        .ToLocalTime()
                        .ToString("HH:mm dd/MM/yyyy")
                })
                .ToListAsync();

            return Json(messages);
        }

        // =====================================================
        // UPLOAD FILE
        // =====================================================
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadFile(IFormFile file, int topicId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized(new { error = "Bạn cần đăng nhập." });
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var topic = await FilterTopicsByActivePeriod(_db.Topics, activePeriod)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound(new { error = "Không tìm thấy đề tài." });

            var roles = await _userManager.GetRolesAsync(user);
            bool canAccess =
                (roles.Contains("Lecturer") && topic.LecturerId == user.Id) ||
                (roles.Contains("Student") &&
                 await FilterRegistrationsByActivePeriod(_db.Registrations, activePeriod).AnyAsync(r =>
                     r.TopicId == topic.Id &&
                     r.StudentId == user.Id &&
                     r.Status == "Approved"));

            if (!canAccess) return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "File tối đa 10MB." });

            var allowedExt = new[]
            {
                ".pdf",".doc",".docx",".png",".jpg",".jpeg",".zip",".rar",".xlsx"
            };

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                return BadRequest(new { error = "File không hợp lệ." });

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "chat");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(uploadDir, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return Ok(new
            {
                success = true,
                url = Url.Action(nameof(DownloadUploadedFile), "Chat", new { topicId, fileName }),
                storedName = fileName,
                name = Path.GetFileName(file.FileName)
            });
        }

        public async Task<IActionResult> DownloadAttachment(int messageId)
        {
            var comment = await _db.TopicComments
                .Include(c => c.Topic)
                    .ThenInclude(t => t!.Registrations)
                .FirstOrDefaultAsync(c => c.Id == messageId && !c.IsDeleted);

            if (comment == null || comment.Topic == null || string.IsNullOrWhiteSpace(comment.AttachmentUrl))
                return NotFound();

            if (!await CanAccessTopic(comment.Topic))
                return Forbid();

            return DownloadChatFile(comment.AttachmentUrl, comment.AttachmentName);
        }

        public async Task<IActionResult> DownloadUploadedFile(int topicId, string fileName)
        {
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            var topic = await FilterTopicsByActivePeriod(_db.Topics, activePeriod)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();
            if (!await CanAccessTopic(topic)) return Forbid();

            return DownloadChatFile(fileName, fileName);
        }

        private async Task<bool> CanAccessTopic(Topic topic)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            var roles = await _userManager.GetRolesAsync(user);
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var isCurrentPeriodTopic = topic.RegistrationPeriodId == activePeriod.Id
                || (topic.RegistrationPeriodId == null && topic.Semester == activePeriod.Name);
            if (!isCurrentPeriodTopic)
            {
                return false;
            }

            return (roles.Contains("Lecturer") && topic.LecturerId == user.Id) ||
                   (roles.Contains("Student") &&
                    await FilterRegistrationsByActivePeriod(_db.Registrations, activePeriod)
                        .AnyAsync(r =>
                            r.TopicId == topic.Id &&
                            r.StudentId == user.Id &&
                            r.Status == "Approved"));
        }

        private IActionResult DownloadChatFile(string storedPathOrName, string? downloadName)
        {
            var storedName = Path.GetFileName(storedPathOrName);
            if (string.IsNullOrWhiteSpace(storedName)) return NotFound();

            var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "chat"));
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
    }

    // =========================================================
    // VIEWMODEL
    // =========================================================
    public class ChatMessageVM
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SenderRole { get; set; } = "";
        public string Content { get; set; } = "";
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFmt { get; set; } = "";
    }
}
