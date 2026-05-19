// ============================================================
// FILE: Controllers/TopicController.cs
// ============================================================
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using X.PagedList;
using X.PagedList.Extensions;

namespace KLTN_Registration_System.Controllers
{
    [Authorize]
    public class TopicController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TopicController(AppDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ============================================================
        // DANH SÁCH ĐỀ TÀI  →  /Topic/Index
        // View: Views/Topic/Index.cshtml  (@model IPagedList<Topic>)
        // ============================================================
        [Authorize(Roles = "Admin,Student,Lecturer")]
        public IActionResult Index(
    int? page,
    int? majorId,
    string? status,
    string? level,
    string? type,
    string? search)
        {
            int pageSize = 6;
            int pageNumber = page ?? 1;

            var query = _context.Topics
                .Include(t => t.Registrations)
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Where(t => t.IsApproved && !t.IsStudentProposed)
                .AsQueryable();

            // =========================
            // FILTER MAJOR
            // =========================

            if (majorId.HasValue)
                query = query.Where(t => t.MajorId == majorId);

            // =========================
            // FILTER LEVEL
            // =========================

            if (!string.IsNullOrEmpty(level)
                && Enum.TryParse<TopicLevel>(level, out var lvl))
            {
                query = query.Where(t => t.Level == lvl);
            }

            // =========================
            // FILTER TYPE
            // =========================

            if (!string.IsNullOrEmpty(type))
            {
                if (type == "group")
                {
                    query = query.Where(t => t.MaxStudents > 1);
                }
                else if (type == "individual")
                {
                    query = query.Where(t => t.MaxStudents == 1);
                }
            }

            // =========================
            // FILTER STATUS
            // =========================

            if (status == "available")
            {
                query = query.Where(t =>
                    t.IsRegistrationOpen &&
                    t.Registrations!.Count(r => r.Status == "Pending" || r.Status == "Approved") < t.MaxStudents);
            }
            else if (status == "full")
            {
                query = query.Where(t =>
                    !t.IsRegistrationOpen ||
                    t.Registrations!.Count(r => r.Status == "Pending" || r.Status == "Approved") >= t.MaxStudents);
            }

            // =========================
            // SEARCH
            // =========================

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    t.Description.Contains(search) ||
                    (t.Lecturer != null && t.Lecturer.FullName!.Contains(search)) ||
                    (t.TopicCode != null && t.TopicCode.Contains(search)));
            }

            // =========================
            // VIEWBAG
            // =========================

            ViewBag.Search = search;

            var topics = query
                .OrderByDescending(t => t.CreatedAt)
                .ToPagedList(pageNumber, pageSize);

            var fullTopicIds = _context.Topics
                .Where(t => t.IsApproved && !t.IsStudentProposed)
                .Where(t => !t.IsRegistrationOpen ||
                    t.Registrations!.Count(r => r.Status == "Pending" || r.Status == "Approved") >= t.MaxStudents)
                .Select(t => t.Id)
                .ToList();

            ViewBag.FullTopicIds = fullTopicIds;
            ViewBag.TotalAvailable = query.Count();
            ViewBag.Majors = _context.Majors
                .Where(m => m.IsActive)
                .ToList();

            if (User.IsInRole("Student"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var myReg = _context.Registrations.FirstOrDefault(r =>
                    r.StudentId == uid &&
                    (r.Status == "Pending" || r.Status == "Approved"));

                ViewBag.HasRegistered = myReg != null;
                ViewBag.RegisteredTopicId = myReg?.TopicId ?? 0;
            }

            return View(topics);
        }

        // ============================================================
        // CHI TIẾT ĐỀ TÀI  →  /Topic/Details/{id}
        // View: Views/Topic/Details.cshtml  (@model Topic)
        // ============================================================
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Include(t => t.Registrations).ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            int approved = topic.Registrations?.Count(r => r.Status == "Approved") ?? 0;
            int pending = topic.Registrations?.Count(r => r.Status == "Pending") ?? 0;
            int reserved = approved + pending;
            ViewBag.ApprovedCount = approved;
            ViewBag.PendingCount = pending;
            ViewBag.RemainingSlots = Math.Max(0, topic.MaxStudents - reserved);

            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

                ViewBag.HasRegisteredThisTopic = await _context.Registrations
                    .AnyAsync(r => r.TopicId == id && r.StudentId == uid && r.Status != "Rejected");

                ViewBag.HasActiveRegistration = await _context.Registrations
                    .AnyAsync(r => r.StudentId == uid && (r.Status == "Pending" || r.Status == "Approved"));
            }

            return View(topic);
        }

        // ============================================================
        // MANAGE (Giảng viên xem danh sách đăng ký)  →  /Topic/Manage
        // View: Views/Topic/Manage.cshtml  (@model List<Registration>)
        // ============================================================
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Manage()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var registrations = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .Where(r => r.Topic!.LecturerId == uid)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(registrations);
        }

        // ============================================================
        // DUYỆT ĐĂNG KÝ  →  POST /Topic/Approve/{id}
        // Được gọi từ Approval.cshtml (asp-controller="Topic")
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();

            var topic = reg.Topic!;

            // Chỉ GV của đề tài hoặc Admin mới được duyệt
            if (topic.LecturerId != uid && !User.IsInRole("Admin"))
                return Forbid();

            // Kiểm tra số lượng slot
            int approvedCount = await _context.Registrations
                .CountAsync(r => r.TopicId == topic.Id && r.Status == "Approved");

            if (approvedCount >= topic.MaxStudents)
            {
                TempData["Error"] = $"Đề tài \"{topic.Title}\" đã đủ {topic.MaxStudents} sinh viên!";
                return RedirectToApproval();
            }

            // Duyệt đăng ký này
            reg.Status = "Approved";
            reg.ApprovedBy = uid;
            reg.UpdatedAt = DateTime.Now;

            // Từ chối tất cả Pending khác của cùng sinh viên này
            var otherPending = await _context.Registrations
                .Where(r => r.StudentId == reg.StudentId && r.Id != reg.Id && r.Status == "Pending")
                .ToListAsync();
            foreach (var other in otherPending)
            {
                other.Status = "Rejected";
                other.UpdatedAt = DateTime.Now;
            }

            // Đóng đề tài khi đủ slot
            if (approvedCount + 1 >= topic.MaxStudents)
            {
                topic.Status = TopicStatus.Full;
                topic.IsRegistrationOpen = false;
            }

            await _context.SaveChangesAsync();

            await AddNotification(reg.StudentId,
                "Đăng ký đề tài được duyệt",
                $"Chúc mừng! Đề tài \"{topic.Title}\" của bạn đã được phê duyệt.",
                "TopicApproved", "/Student/MyRegistration");

            TempData["Success"] = $"Đã duyệt {reg.Student?.FullName ?? reg.Student?.UserName}.";
            return RedirectToApproval();
        }

        // ============================================================
        // TỪ CHỐI ĐĂNG KÝ  →  POST /Topic/Reject/{id}
        // Được gọi từ Approval.cshtml (asp-controller="Topic")
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Reject(int id, string? feedback = null)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null) return NotFound();

            if (reg.Topic!.LecturerId != uid && !User.IsInRole("Admin"))
                return Forbid();

            reg.Status = "Rejected";
            reg.Feedback = feedback;
            reg.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await AddNotification(reg.StudentId,
                "Đăng ký đề tài bị từ chối",
                $"Rất tiếc, đề tài \"{reg.Topic?.Title}\" đã bị từ chối." +
                (string.IsNullOrEmpty(feedback) ? "" : $" Lý do: {feedback}"),
                "TopicRejected", "/Topic/Index");

            TempData["Error"] = $"Đã từ chối đăng ký của {reg.Student?.FullName ?? reg.Student?.UserName}.";
            return RedirectToApproval();
        }

        // ============================================================
        // SINH VIÊN ĐĂNG KÝ CÁ NHÂN  →  POST /Topic/Register
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Register(int topicId)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.Now;
            var settings = await _context.Settings.ToListAsync();
            var startStr = settings.FirstOrDefault(s => s.Name == "Registration_Start")?.Value;
            var endStr = settings.FirstOrDefault(s => s.Name == "Registration_End")?.Value;

            if (DateTime.TryParse(startStr, out var startDate) && now < startDate)
            {
                TempData["Error"] = $"Cổng đăng ký chưa mở. Thời gian bắt đầu: {startDate:dd/MM/yyyy HH:mm}.";
                return RedirectToAction(nameof(Index));
            }

            if (DateTime.TryParse(endStr, out var endDate) && now > endDate)
            {
                TempData["Error"] = "Đã hết thời hạn đăng ký!";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.Registrations.AnyAsync(r =>
                r.StudentId == uid && (r.Status == "Pending" || r.Status == "Approved")))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký tối đa 1 đề tài!";
                return RedirectToAction(nameof(Index));
            }

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            if (!topic.IsApproved || !topic.IsRegistrationOpen || topic.IsStudentProposed)
            {
                TempData["Error"] = "Đề tài này hiện không mở cho sinh viên đăng ký.";
                return RedirectToAction(nameof(Index));
            }

            if (topic.MaxStudents > 1)
            {
                TempData["Error"] = "Đề tài nhóm cần đăng ký bằng chức năng đăng ký nhóm.";
                return RedirectToAction(nameof(RegisterGroup), new { id = topicId });
            }

            if (topic.Deadline > DateTime.MinValue && now > topic.Deadline)
            {
                TempData["Error"] = "Đề tài đã hết hạn đăng ký!";
                return RedirectToAction(nameof(Index));
            }

            int currentCount = topic.Registrations!
                .Count(r => r.Status == "Pending" || r.Status == "Approved");

            if (currentCount >= topic.MaxStudents)
            {
                TempData["Error"] = "Đề tài đã đủ số lượng!";
                return RedirectToAction(nameof(Index));
            }

            _context.Registrations.Add(new Registration
            {
                StudentId = uid,
                TopicId = topicId,
                Status = "Pending",
                CreatedAt = now,
                Priority = 1
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.LecturerId))
                await AddNotification(topic.LecturerId,
                    "Sinh viên đăng ký đề tài",
                    $"Có sinh viên mới đăng ký đề tài \"{topic.Title}\".",
                    "NewRegistration", "/Lecturer/Approval");

            TempData["Success"] = "Đăng ký thành công! Chờ giảng viên phê duyệt.";
            return RedirectToAction("Home", "Student");
        }

        // ============================================================
        // ĐĂNG KÝ NHÓM — mở trang  →  GET /Topic/RegisterGroup/{id}
        // View: Views/Topic/RegisterGroup.cshtml  (@model Topic)
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> RegisterGroup(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            // Nếu đề tài 1 người → chuyển về đăng ký cá nhân
            if (topic.MaxStudents <= 1) return RedirectToAction(nameof(Index));

            if (!topic.IsApproved || !topic.IsRegistrationOpen || topic.IsStudentProposed)
            {
                TempData["Error"] = "Đề tài này hiện không mở cho đăng ký nhóm.";
                return RedirectToAction(nameof(Index));
            }

            var windowError = await GetRegistrationWindowError();
            if (windowError != null)
            {
                TempData["Error"] = windowError;
                return RedirectToAction(nameof(Index));
            }

            if (topic.Deadline > DateTime.MinValue && DateTime.Now > topic.Deadline)
            {
                TempData["Error"] = "Đề tài đã hết hạn đăng ký!";
                return RedirectToAction(nameof(Index));
            }

            bool leaderBusy = await _context.Registrations.AnyAsync(r =>
                r.StudentId == uid && (r.Status == "Pending" || r.Status == "Approved"));
            if (leaderBusy)
            {
                TempData["Error"] = "Bạn đang có một đề tài trong trạng thái chờ hoặc đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            int current = topic.Registrations!
                .Count(r => r.Status == "Pending" || r.Status == "Approved");

            if (current >= topic.MaxStudents)
            {
                TempData["Error"] = "Đề tài đã đủ thành viên!";
                return RedirectToAction(nameof(Index));
            }

            return View(topic);
        }

        // ============================================================
        // XỬ LÝ ĐĂNG KÝ NHÓM  →  POST /Topic/SubmitGroupRegistration
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitGroupRegistration(int topicId, List<string>? memberEmails)
        {
            var leaderId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.Now;

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            if (topic.MaxStudents <= 1)
            {
                TempData["Error"] = "Đề tài này không phải đề tài nhóm.";
                return RedirectToAction(nameof(Index));
            }

            if (!topic.IsApproved || !topic.IsRegistrationOpen || topic.IsStudentProposed)
            {
                TempData["Error"] = "Đề tài này hiện không mở cho đăng ký nhóm.";
                return RedirectToAction(nameof(Index));
            }

            var windowError = await GetRegistrationWindowError();
            if (windowError != null)
            {
                TempData["Error"] = windowError;
                return RedirectToAction(nameof(Index));
            }

            if (topic.Deadline > DateTime.MinValue && now > topic.Deadline)
            {
                TempData["Error"] = "Đề tài đã hết hạn đăng ký!";
                return RedirectToAction(nameof(Index));
            }

            bool leaderBusy = await _context.Registrations.AnyAsync(r =>
                r.StudentId == leaderId && (r.Status == "Pending" || r.Status == "Approved"));
            if (leaderBusy)
            {
                TempData["Error"] = "Bạn đang có một đề tài trong trạng thái chờ hoặc đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            // Lọc email sạch
            var cleanEmails = (memberEmails ?? new List<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct()
                .ToList();

            int current = topic.Registrations!
                .Count(r => r.Status == "Pending" || r.Status == "Approved");

            // Kiểm tra sĩ số: Trưởng nhóm (1) + thành viên
            if (current + cleanEmails.Count + 1 > topic.MaxStudents)
            {
                TempData["Error"] = $"Tối đa {topic.MaxStudents} thành viên cho đề tài này!";
                return RedirectToAction(nameof(RegisterGroup), new { id = topicId });
            }

            var allIds = new List<string> { leaderId };

            foreach (var email in cleanEmails)
            {
                var member = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email || u.UserName == email);

                if (member == null)
                {
                    TempData["Error"] = $"Không tìm thấy sinh viên: {email}";
                    return RedirectToAction(nameof(RegisterGroup), new { id = topicId });
                }

                if (member.Id == leaderId) continue;

                if (!await _userManager.IsInRoleAsync(member, "Student"))
                {
                    TempData["Error"] = $"{email} không phải tài khoản sinh viên.";
                    return RedirectToAction(nameof(RegisterGroup), new { id = topicId });
                }

                if (allIds.Contains(member.Id)) continue;

                bool isBusy = await _context.Registrations.AnyAsync(r =>
                    r.StudentId == member.Id && (r.Status == "Pending" || r.Status == "Approved"));

                if (isBusy)
                {
                    TempData["Error"] = $"Sinh viên {email} đã có đề tài khác!";
                    return RedirectToAction(nameof(RegisterGroup), new { id = topicId });
                }

                allIds.Add(member.Id);
            }

            // Lưu đăng ký cho cả nhóm
            foreach (var sid in allIds)
            {
                _context.Registrations.Add(new Registration
                {
                    StudentId = sid,
                    TopicId = topicId,
                    Status = "Pending",
                    CreatedAt = now,
                    Priority = 1
                });

                _context.Notifications.Add(new Notification
                {
                    UserId = sid,
                    Title = "Đăng ký nhóm",
                    Content = $"Bạn đã được thêm vào nhóm đề tài \"{topic.Title}\".",
                    Type = "Registration",
                    RedirectUrl = "/Student/MyRegistration",
                    IsRead = false,
                    CreatedAt = now
                });
            }

            if (!string.IsNullOrEmpty(topic.LecturerId))
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = topic.LecturerId,
                    Title = "Nhóm sinh viên đăng ký đề tài",
                    Content = $"Có nhóm {allIds.Count} sinh viên đăng ký đề tài \"{topic.Title}\".",
                    Type = "NewRegistration",
                    RedirectUrl = "/Lecturer/Approval",
                    IsRead = false,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đăng ký nhóm thành công! Chờ giảng viên phê duyệt.";
            return RedirectToAction("Home", "Student");
        }

        // ============================================================
        // HỦY ĐĂNG KÝ  →  POST /Topic/Cancel/{id}
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Cancel(int id)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reg = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == uid);

            if (reg == null) return NotFound();

            if (reg.Status == "Approved")
            {
                TempData["Error"] = "Đề tài đã được duyệt. Vui lòng liên hệ giảng viên để hủy!";
                return RedirectToAction("MyRegistration", "Student");
            }

            string topicTitle = reg.Topic?.Title ?? "";
            _context.Registrations.Remove(reg);
            await _context.SaveChangesAsync();

            await AddNotification(uid!,
                "Hủy đăng ký",
                $"Bạn đã hủy đăng ký đề tài \"{topicTitle}\".",
                "System", "/Topic/Index");

            TempData["Success"] = "Đã hủy đăng ký thành công.";
            return RedirectToAction("MyRegistration", "Student");
        }

        // ============================================================
        // SINH VIÊN TỰ ĐỀ XUẤT ĐỀ TÀI  →  GET /Topic/Propose
        // View: Views/Topic/Propose.cshtml  (@model Topic)
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Propose()
        {
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers
                .Select(u => new { u.Id, FullName = u.FullName ?? u.Email })
                .ToList();
            ViewBag.Majors = _context.Majors.Where(m => m.IsActive).ToList();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Propose(Topic topic)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            topic.CreatedByStudentId = uid;
            topic.IsStudentProposed = true;
            topic.IsApproved = false;
            topic.IsRegistrationOpen = false;
            topic.CreatedAt = DateTime.Now;
            topic.MaxStudents = 1;
            topic.Deadline = DateTime.Now.AddMonths(3);
            topic.Status = TopicStatus.Pending;
            topic.Level = TopicLevel.Easy;

            // Bỏ qua validation navigation properties
            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");

            if (ModelState.IsValid)
            {
                _context.Topics.Add(topic);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Gửi đề xuất thành công! Vui lòng chờ phê duyệt.";
                return RedirectToAction(nameof(Index));
            }

            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers
                .Select(u => new { u.Id, FullName = u.FullName ?? u.Email })
                .ToList();
            ViewBag.Majors = _context.Majors.Where(m => m.IsActive).ToList();
            return View(topic);
        }

        // ============================================================
        // HELPER (private)
        // ============================================================

        /// <summary>Redirect về trang Approval phù hợp theo Role.</summary>
        private IActionResult RedirectToApproval() =>
            User.IsInRole("Admin")
                ? RedirectToAction("Approval", "Admin")
                : RedirectToAction("Approval", "Lecturer");

        private async Task AddNotification(
            string userId, string title, string content,
            string type, string redirectUrl = "")
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Content = content,
                Type = type,
                RedirectUrl = redirectUrl,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        private async Task<string?> GetRegistrationWindowError()
        {
            var settings = await _context.Settings.ToListAsync();
            var startStr = settings.FirstOrDefault(s => s.Name == "Registration_Start")?.Value;
            var endStr = settings.FirstOrDefault(s => s.Name == "Registration_End")?.Value;
            var now = DateTime.Now;

            if (DateTime.TryParse(startStr, out var startDate) && now < startDate)
                return $"Cổng đăng ký chưa mở. Thời gian bắt đầu: {startDate:dd/MM/yyyy HH:mm}.";

            if (DateTime.TryParse(endStr, out var endDate) && now > endDate)
                return "Đã hết thời hạn đăng ký!";

            return null;
        }
        [Authorize(Roles = "Admin,Lecturer")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Create(Topic topic)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");
            ModelState.Remove("Registrations");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ!";
                return RedirectToAction("ManageTopics", "Admin");
            }

            topic.CreatedAt = DateTime.Now;
            topic.LecturerId = uid;

            topic.IsApproved = User.IsInRole("Admin");
            topic.IsRegistrationOpen = true;
            topic.Status = TopicStatus.Available;

            topic.TopicCode = $"TP{DateTime.Now.Ticks.ToString()[^5..]}";

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo đề tài thành công!";

            return RedirectToAction("ManageTopics", "Admin");
        }
    }
}
