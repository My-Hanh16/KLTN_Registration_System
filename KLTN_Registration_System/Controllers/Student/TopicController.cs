// ============================================================
// FILE: Controllers/TopicController.cs
// ============================================================
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using X.PagedList;
using X.PagedList.Extensions;

namespace KLTN_Registration_System.Controllers
{
    [Authorize]
    public class TopicController : BaseController
    {
        private readonly AppDbContext _context;

        public TopicController(AppDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
            _context = context;
        }

        // ============================================================
        // DANH SÁCH ĐỀ TÀI  →  /Topic/Index
        // View: Views/Topic/Index.cshtml  (@model IPagedList<Topic>)
        // ============================================================
        [Authorize(Roles = "Admin,Student,Lecturer")]
        public async Task<IActionResult> Index(
    int? page,
    int? majorId,
    string? status,
    string? level,
    string? type,
    string? search)
        {
            int pageSize = 6;
            int pageNumber = page ?? 1;
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var query = _context.Topics
                .Include(t => t.Registrations)
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Where(t => t.IsApproved && !t.IsStudentProposed)
                .AsQueryable();

            query = FilterTopicsByActivePeriod(query, activePeriod);

            List<int> allowedMajorIds = new();
            List<string> allowedFacultyNames = new();

            if (User.IsInRole("Student"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(t => t.CreatedByStudentId == null);
                var canParticipate = await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id);
                var participationMessage = await GetStudentEligibilityErrorMessageAsync(uid);
                ViewBag.CanParticipateInCurrentPeriod = canParticipate;
                ViewBag.StudentParticipationMessage = canParticipate ? "" : participationMessage;
                ViewBag.StudentHasCompletedThesis = !string.IsNullOrWhiteSpace(uid)
                    && await _context.Users.AnyAsync(u => u.Id == uid && u.HasCompletedThesis);

                if (!canParticipate)
                {
                    query = query.Where(_ => false);
                    TempData["Error"] = participationMessage;
                }

                var access = await GetUserTopicAccessAsync(uid);
                allowedMajorIds = access.MajorIds;
                allowedFacultyNames = access.FacultyNames;
                var allowedFacultyKeys = allowedFacultyNames
                    .Select(NormalizeAccessKey)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (allowedMajorIds.Any() || allowedFacultyNames.Any())
                {
                    query = query.Where(t =>
                        (t.MajorId.HasValue && allowedMajorIds.Contains(t.MajorId.Value))
                        || (t.Major != null
                            && t.Major.FacultyName != null
                            && allowedFacultyKeys.Contains(t.Major.FacultyName.Trim().ToUpper()))
                        || (t.Faculty != null
                            && allowedFacultyKeys.Contains(t.Faculty.Trim().ToUpper())));
                }
                else
                {
                    query = query.Where(_ => false);
                    TempData["Error"] = "Tài khoản của bạn chưa được gán khoa/chuyên ngành nên chưa thể xem danh sách đề tài.";
                }
            }

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

            var fullTopicsQuery = _context.Topics
                .Include(t => t.Major)
                .Where(t => t.IsApproved && !t.IsStudentProposed)
                .AsQueryable();
            fullTopicsQuery = FilterTopicsByActivePeriod(fullTopicsQuery, activePeriod);

            if (User.IsInRole("Student") && (allowedMajorIds.Any() || allowedFacultyNames.Any()))
            {
                fullTopicsQuery = fullTopicsQuery.Where(t => t.CreatedByStudentId == null);
                var allowedFacultyKeys = allowedFacultyNames
                    .Select(NormalizeAccessKey)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                fullTopicsQuery = fullTopicsQuery.Where(t =>
                    (t.MajorId.HasValue && allowedMajorIds.Contains(t.MajorId.Value))
                    || (t.Major != null
                        && t.Major.FacultyName != null
                        && allowedFacultyKeys.Contains(t.Major.FacultyName.Trim().ToUpper()))
                    || (t.Faculty != null
                        && allowedFacultyKeys.Contains(t.Faculty.Trim().ToUpper())));
            }

            var fullTopicIds = fullTopicsQuery
                .Where(t => t.Registrations!.Count(r => r.Status == "Pending" || r.Status == "Approved") >= t.MaxStudents)
                .Select(t => t.Id)
                .ToList();

            ViewBag.FullTopicIds = fullTopicIds;
            ViewBag.TotalAvailable = query.Count();
            var majorsQuery = _context.Majors
                .Where(m => m.IsActive)
                .AsQueryable();

            if (User.IsInRole("Student") && (allowedMajorIds.Any() || allowedFacultyNames.Any()))
            {
                majorsQuery = majorsQuery.Where(m =>
                    allowedMajorIds.Contains(m.Id)
                    || (m.FacultyName != null && allowedFacultyNames.Contains(m.FacultyName)));
            }

            ViewBag.Majors = majorsQuery.ToList();

            if (User.IsInRole("Student"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var canParticipate = await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id);
                ViewBag.CanParticipateInCurrentPeriod = canParticipate;
                ViewBag.StudentParticipationMessage = canParticipate ? "" : await GetStudentEligibilityErrorMessageAsync(uid);
                ViewBag.StudentHasCompletedThesis = !string.IsNullOrWhiteSpace(uid)
                    && await _context.Users.AnyAsync(u => u.Id == uid && u.HasCompletedThesis);

                var myReg = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                    .FirstOrDefaultAsync(r =>
                    r.StudentId == uid &&
                    (r.Status == "Pending" || r.Status == "Approved"));

                ViewBag.HasRegistered = myReg != null;
                ViewBag.RegisteredTopicId = myReg?.TopicId ?? 0;
                ViewBag.HasPendingProposal = await HasPendingProposalAsync(uid, activePeriod.Id);
                ViewBag.RestrictedByFaculty = true;
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            var topic = await _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Include(t => t.Registrations!).ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            if (topic.RegistrationPeriodId != activePeriod.Id && topic.Semester != activePeriod.Name)
            {
                TempData["Error"] = "Đề tài này không thuộc đợt đăng ký đang mở.";
                return RedirectToAction(nameof(Index));
            }

            int approved = topic.Registrations?.Count(r => r.Status == "Approved") ?? 0;
            int pending = topic.Registrations?.Count(r => r.Status == "Pending") ?? 0;
            int reserved = approved + pending;
            ViewBag.ApprovedCount = approved;
            ViewBag.PendingCount = pending;
            ViewBag.RemainingSlots = Math.Max(0, topic.MaxStudents - reserved);
            ViewBag.ActivePeriodName = activePeriod.Name;
            ViewBag.ReservedCount = reserved;
            ViewBag.IsStudentContext = User.Identity?.IsAuthenticated == true && User.IsInRole("Student");

            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var canParticipate = await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id);
                ViewBag.CanParticipateInCurrentPeriod = canParticipate;
                ViewBag.StudentParticipationMessage = canParticipate ? "" : await GetStudentEligibilityErrorMessageAsync(uid);
                ViewBag.StudentHasCompletedThesis = !string.IsNullOrWhiteSpace(uid)
                    && await _context.Users.AnyAsync(u => u.Id == uid && u.HasCompletedThesis);

                if (!await CanStudentAccessTopicAsync(uid, topic.MajorId, topic.Faculty))
                {
                    TempData["Error"] = "Bạn chỉ được xem và đăng ký đề tài thuộc khoa/chuyên ngành của mình.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.HasRegisteredThisTopic = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                    .AnyAsync(r => r.TopicId == id && r.StudentId == uid && r.Status != "Rejected");

                ViewBag.HasActiveRegistration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                    .AnyAsync(r => r.StudentId == uid && (r.Status == "Pending" || r.Status == "Approved"));

                ViewBag.HasPendingProposal = await HasPendingProposalAsync(uid, activePeriod.Id);
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var query = _context.Registrations
                .Include(r => r.Topic)
                .Include(r => r.Student)
                .AsQueryable();

            query = FilterRegistrationsByActivePeriod(query, activePeriod);

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(r => r.Topic!.LecturerId == uid);
            }

            var registrations = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.ActivePeriodName = activePeriod.Name;
            ViewBag.PendingCount = registrations.Count(r => r.Status == "Pending");
            ViewBag.ApprovedCount = registrations.Count(r => r.Status == "Approved");
            ViewBag.RejectedCount = registrations.Count(r => r.Status == "Rejected");
            ViewBag.TopicCount = registrations.Select(r => r.TopicId).Distinct().Count();

            return View(registrations);
        }

        // ============================================================
        // DUYỆT ĐĂNG KÝ  →  POST /Topic/Approve/{id}
        // Được gọi từ Approval.cshtml (asp-controller="Topic")
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Approve(int id, string? returnTo = null)
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
                return RedirectAfterTopicDecision(returnTo);
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
            return RedirectAfterTopicDecision(returnTo);
        }

        // ============================================================
        // TỪ CHỐI ĐĂNG KÝ  →  POST /Topic/Reject/{id}
        // Được gọi từ Approval.cshtml (asp-controller="Topic")
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Reject(int id, string? feedback = null, string? returnTo = null)
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
            return RedirectAfterTopicDecision(returnTo);
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (!await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = await GetStudentEligibilityErrorMessageAsync(uid);
                return RedirectToAction(nameof(Index));
            }
            var settings = await _context.Settings.ToListAsync();
            var startStr = settings.FirstOrDefault(s => s.Name == "Registration_Start")?.Value
                ?? activePeriod.RegistrationOpenAt.ToString("O");
            var endStr = settings.FirstOrDefault(s => s.Name == "Registration_End")?.Value
                ?? activePeriod.RegistrationCloseAt.ToString("O");

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

            if (await StudentHasActiveRegistrationAsync(uid))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký tối đa 1 đề tài!";
                return RedirectToAction(nameof(Index));
            }

            if (await HasPendingProposalAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = "Bạn đang có đề xuất đề tài chờ duyệt. Hãy hủy đề xuất đó trước khi đăng ký đề tài của giảng viên.";
                return RedirectToAction(nameof(Index));
            }

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            if (topic.RegistrationPeriodId != activePeriod.Id && topic.Semester != activePeriod.Name)
            {
                TempData["Error"] = "Đề tài này không thuộc đợt đăng ký đang mở.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanStudentAccessTopicAsync(uid, topic.MajorId, topic.Faculty))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký đề tài thuộc khoa/chuyên ngành của mình.";
                return RedirectToAction(nameof(Index));
            }

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
                RegistrationPeriodId = activePeriod.Id,
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
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (!await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = await GetStudentEligibilityErrorMessageAsync(uid);
                return RedirectToAction(nameof(Index));
            }

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            if (topic.RegistrationPeriodId != activePeriod.Id && topic.Semester != activePeriod.Name)
            {
                TempData["Error"] = "Đề tài này không thuộc đợt đăng ký đang mở.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanStudentAccessTopicAsync(uid, topic.MajorId, topic.Faculty))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký đề tài thuộc khoa/chuyên ngành của mình.";
                return RedirectToAction(nameof(Index));
            }

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

            bool leaderBusy = await StudentHasActiveRegistrationAsync(uid);
            if (leaderBusy)
            {
                TempData["Error"] = "Bạn đang có một đề tài trong trạng thái chờ hoặc đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            if (await HasPendingProposalAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = "Bạn đang có đề xuất đề tài chờ duyệt. Hãy hủy đề xuất đó trước khi đăng ký đề tài của giảng viên.";
                return RedirectToAction(nameof(Index));
            }

            int current = topic.Registrations!
                .Count(r => r.Status == "Pending" || r.Status == "Approved");
            int remainingSlots = topic.MaxStudents - current;

            if (remainingSlots < 2)
            {
                TempData["Error"] = remainingSlots <= 0
                    ? "Đề tài đã đủ thành viên!"
                    : "Đề tài nhóm cần ít nhất 2 sinh viên, nhưng hiện chỉ còn 1 chỗ trống.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CurrentMembers = current;
            ViewBag.RemainingSlots = remainingSlots;

            return View(topic);
        }

        // ============================================================
        // XỬ LÝ ĐĂNG KÝ NHÓM  →  POST /Topic/SubmitGroupRegistration
        // ============================================================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitGroupRegistration(int topicId, List<string>? memberEmails, string? returnTo = null)
        {
            var leaderId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.Now;
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            IActionResult RedirectGroupForm()
                => string.Equals(returnTo, "Index", StringComparison.OrdinalIgnoreCase)
                    ? RedirectToAction(nameof(Index))
                    : RedirectToAction(nameof(RegisterGroup), new { id = topicId });

            if (!await IsStudentEligibleForPeriodAsync(leaderId, activePeriod.Id))
            {
                TempData["Error"] = await GetStudentEligibilityErrorMessageAsync(leaderId);
                return RedirectToAction(nameof(Index));
            }

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound();

            if (topic.RegistrationPeriodId != activePeriod.Id && topic.Semester != activePeriod.Name)
            {
                TempData["Error"] = "Đề tài này không thuộc đợt đăng ký đang mở.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanStudentAccessTopicAsync(leaderId, topic.MajorId, topic.Faculty))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký đề tài thuộc khoa/chuyên ngành của mình.";
                return RedirectToAction(nameof(Index));
            }

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

            bool leaderBusy = await StudentHasActiveRegistrationAsync(leaderId);
            if (leaderBusy)
            {
                TempData["Error"] = "Bạn đang có một đề tài trong trạng thái chờ hoặc đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            if (await HasPendingProposalAsync(leaderId, activePeriod.Id))
            {
                TempData["Error"] = "Bạn đang có đề xuất đề tài chờ duyệt. Hãy hủy đề xuất đó trước khi đăng ký đề tài của giảng viên.";
                return RedirectToAction(nameof(Index));
            }

            // Lọc email sạch
            var cleanEmails = (memberEmails ?? new List<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!cleanEmails.Any())
            {
                TempData["Error"] = "Đề tài nhóm bắt buộc phải có ít nhất 2 sinh viên. Vui lòng nhập thêm 1 thành viên.";
                return RedirectGroupForm();
            }

            int current = topic.Registrations!
                .Count(r => r.Status == "Pending" || r.Status == "Approved");
            int remainingSlots = topic.MaxStudents - current;

            if (remainingSlots < 2)
            {
                TempData["Error"] = remainingSlots <= 0
                    ? "Đề tài đã đủ thành viên!"
                    : "Đề tài nhóm cần ít nhất 2 sinh viên, nhưng hiện chỉ còn 1 chỗ trống.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra sĩ số: Trưởng nhóm (1) + thành viên
            if (cleanEmails.Count + 1 > remainingSlots)
            {
                TempData["Error"] = $"Đề tài chỉ còn {remainingSlots} chỗ trống. Nhóm của bạn đang có {cleanEmails.Count + 1} sinh viên.";
                return RedirectGroupForm();
            }

            var allIds = new List<string> { leaderId };

            foreach (var email in cleanEmails)
            {
                var member = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email || u.UserName == email || u.UserCode == email);

                if (member == null)
                {
                    TempData["Error"] = $"Không tìm thấy sinh viên: {email}";
                    return RedirectGroupForm();
                }

                if (member.Id == leaderId)
                {
                    TempData["Error"] = "Không thể nhập chính bạn làm thành viên nhóm. Vui lòng nhập sinh viên khác.";
                    return RedirectGroupForm();
                }

                if (!await _userManager.IsInRoleAsync(member, "Student"))
                {
                    TempData["Error"] = $"{email} không phải tài khoản sinh viên.";
                    return RedirectGroupForm();
                }

                if (allIds.Contains(member.Id)) continue;

                if (!await IsStudentEligibleForPeriodAsync(member.Id, activePeriod.Id))
                {
                    TempData["Error"] = member.HasCompletedThesis
                        ? $"Sinh viên {email} đã hoàn thành KLTN nên không thể tham gia đăng ký ở đợt này."
                        : $"Sinh viên {email} chưa nằm trong danh sách đủ điều kiện của đợt đăng ký hiện tại.";
                    return RedirectGroupForm();
                }

                if (!await CanStudentAccessTopicAsync(member.Id, topic.MajorId, topic.Faculty))
                {
                    TempData["Error"] = $"Sinh viên {email} không thuộc khoa/chuyên ngành của đề tài.";
                    return RedirectGroupForm();
                }

                bool isBusy = await StudentHasActiveRegistrationAsync(member.Id);

                if (isBusy)
                {
                    TempData["Error"] = $"Sinh viên {email} đã có đề tài khác!";
                    return RedirectGroupForm();
                }

                if (await HasPendingProposalAsync(member.Id, activePeriod.Id))
                {
                    TempData["Error"] = $"Sinh viên {email} đang có đề xuất đề tài chờ duyệt.";
                    return RedirectGroupForm();
                }

                allIds.Add(member.Id);
            }

            if (allIds.Count < 2)
            {
                TempData["Error"] = "Đề tài nhóm bắt buộc phải có ít nhất 2 sinh viên.";
                return RedirectGroupForm();
            }

            // Lưu đăng ký cho cả nhóm
            foreach (var sid in allIds)
            {
                _context.Registrations.Add(new Registration
                {
                    StudentId = sid,
                    TopicId = topicId,
                    RegistrationPeriodId = activePeriod.Id,
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
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(uid)) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (!await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = await GetStudentEligibilityErrorMessageAsync(uid);
                return RedirectToAction(nameof(Index));
            }

            var access = await GetUserTopicAccessAsync(uid);
            ViewBag.ActivePeriodName = activePeriod.Name;
            ViewBag.Lecturers = await GetLecturerOptionsForAccessAsync(access.FacultyNames);
            ViewBag.PrimaryMajor = await ResolvePrimaryMajorAsync(access);
            ViewBag.PendingProposalCount = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .CountAsync(t => t.CreatedByStudentId == uid
                              && t.IsStudentProposed
                              && t.Status == TopicStatus.Pending);
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Propose(Topic topic)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(uid)) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (!await IsStudentEligibleForPeriodAsync(uid, activePeriod.Id))
            {
                TempData["Error"] = await GetStudentEligibilityErrorMessageAsync(uid);
                return RedirectToAction(nameof(Index));
            }
            var access = await GetUserTopicAccessAsync(uid);
            var primaryMajor = await ResolvePrimaryMajorAsync(access);
            var lecturers = await GetLecturerOptionsForAccessAsync(access.FacultyNames);

            void PrepareViewData()
            {
                ViewBag.ActivePeriodName = activePeriod.Name;
                ViewBag.Lecturers = lecturers;
                ViewBag.PrimaryMajor = primaryMajor;
            }

            topic.Title = topic.Title?.Trim() ?? "";
            topic.Description = topic.Description?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(topic.Title) || string.IsNullOrWhiteSpace(topic.Description))
            {
                TempData["Error"] = "Tên đề tài và mô tả không được để trống.";
                PrepareViewData();
                return View(topic);
            }

            if (await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod).AnyAsync(r =>
                r.StudentId == uid && (r.Status == "Pending" || r.Status == "Approved")))
            {
                TempData["Error"] = "Bạn đang có đề tài chờ duyệt hoặc đã được duyệt, không thể đề xuất thêm.";
                PrepareViewData();
                return View(topic);
            }

            if (primaryMajor == null)
            {
                TempData["Error"] = "Tài khoản của bạn chưa được gán chuyên ngành nên chưa thể đề xuất đề tài.";
                PrepareViewData();
                return View(topic);
            }

            int pendingProposals = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .CountAsync(t => t.CreatedByStudentId == uid
                              && t.IsStudentProposed
                              && t.Status == TopicStatus.Pending);
            if (pendingProposals >= 3)
            {
                TempData["Error"] = "Bạn đã có 3 đề xuất đang chờ duyệt, không thể thêm.";
                PrepareViewData();
                return View(topic);
            }

            if (!string.IsNullOrWhiteSpace(topic.LecturerId))
            {
                var allowedLecturerIds = lecturers.Select(l => l.Id)
                    .ToHashSet();

                if (!allowedLecturerIds.Contains(topic.LecturerId))
                {
                    TempData["Error"] = "Giảng viên hướng dẫn được chọn không thuộc khoa của bạn.";
                    PrepareViewData();
                    return View(topic);
                }
            }

            var user = await _userManager.FindByIdAsync(uid!);
            var topicCount = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .CountAsync(t => t.CreatedByStudentId == uid);
            var studentCodePart = !string.IsNullOrWhiteSpace(user?.UserCode)
                ? user.UserCode.Trim().ToUpperInvariant()
                : uid[..Math.Min(4, uid.Length)].ToUpperInvariant();

            topic.MajorId = primaryMajor.Id;
            topic.DepartmentName = primaryMajor.Name;
            topic.Faculty = primaryMajor.FacultyName;
            topic.Semester = activePeriod.Name;
            topic.RegistrationPeriodId = activePeriod.Id;
            topic.CreatedByStudentId = uid;
            topic.IsStudentProposed = true;
            topic.IsApproved = false;
            topic.IsRegistrationOpen = false;
            topic.CreatedAt = DateTime.Now;
            topic.MaxStudents = 1;
            topic.Deadline = DateTime.Now.AddMonths(3);
            topic.Status = TopicStatus.Pending;
            topic.Level = TopicLevel.Medium;
            topic.TopicCode = $"PROP-{studentCodePart}-{(topicCount + 1):D3}";

            // Bỏ qua validation navigation properties
            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");

            if (ModelState.IsValid)
            {
                _context.Topics.Add(topic);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(topic.LecturerId))
                {
                    await AddNotification(topic.LecturerId,
                        "Sinh viên đề xuất đề tài",
                        $"Sinh viên {user?.FullName ?? user?.Email} muốn bạn xem xét hướng dẫn đề tài: \"{topic.Title}\".",
                        "NewTopic", "/Lecturer/Approval?tab=proposals");
                }
                else
                {
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins)
                    {
                        await AddNotification(admin.Id,
                            "Đề xuất cần phân công giảng viên",
                            $"Sinh viên {user?.FullName ?? user?.Email} đề xuất đề tài \"{topic.Title}\" nhưng chưa chọn giảng viên hướng dẫn.",
                            "NewTopic", "/Admin/StudentProposals?status=pending");
                    }
                }

                TempData["Success"] = !string.IsNullOrEmpty(topic.LecturerId)
                    ? "Đề xuất đề tài thành công! Giảng viên mong muốn sẽ xem xét trước khi chuyển Admin."
                    : "Đề xuất đề tài thành công! Admin sẽ phân công giảng viên phù hợp.";
                return RedirectToAction("MyRegistration", "Student");
            }

            PrepareViewData();
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

        private IActionResult RedirectAfterTopicDecision(string? returnTo)
        {
            if (string.Equals(returnTo, "Manage", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Manage));
            }

            return RedirectToApproval();
        }

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
                RedirectUrl = NotificationService.NormalizeRedirectUrl(redirectUrl),
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

        private async Task<(List<int> MajorIds, List<string> FacultyNames)> GetUserTopicAccessAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (new List<int>(), new List<string>());
            }

            var user = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return (new List<int>(), new List<string>());
            }

            var majorIds = user.UserMajors
                .Select(um => um.MajorId)
                .ToList();

            if (user.MajorId.HasValue)
            {
                majorIds.Add(user.MajorId.Value);
            }

            var facultyNames = user.UserMajors
                .Select(um => um.Major?.FacultyName)
                .Append(user.Major?.FacultyName)
                .Append(user.Faculty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!majorIds.Any() && !facultyNames.Any())
            {
                var registeredMajors = await _context.Registrations
                    .Include(r => r.Topic)
                        .ThenInclude(t => t!.Major)
                    .Where(r => r.StudentId == userId
                        && (r.Status == "Pending" || r.Status == "Approved")
                        && r.Topic != null
                        && r.Topic.MajorId.HasValue)
                    .Select(r => new
                    {
                        MajorId = r.Topic!.MajorId!.Value,
                        FacultyName = r.Topic.Major != null ? r.Topic.Major.FacultyName : null
                    })
                    .ToListAsync();

                majorIds.AddRange(registeredMajors.Select(m => m.MajorId));
                facultyNames.AddRange(registeredMajors
                    .Select(m => m.FacultyName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim()));
            }

            return (
                majorIds.Distinct().ToList(),
                facultyNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            );
        }

        private async Task<bool> CanStudentAccessTopicAsync(string? userId, int? topicMajorId, string? topicFaculty = null)
        {
            var access = await GetUserTopicAccessAsync(userId);
            var facultyKeys = access.FacultyNames.Select(NormalizeAccessKey).ToList();

            if (!string.IsNullOrWhiteSpace(topicFaculty)
                && facultyKeys.Contains(NormalizeAccessKey(topicFaculty)))
            {
                return true;
            }

            if (!topicMajorId.HasValue)
            {
                return false;
            }

            var topicMajor = await _context.Majors.FindAsync(topicMajorId.Value);
            if (topicMajor == null)
            {
                return false;
            }

            return access.MajorIds.Contains(topicMajorId.Value)
                || (!string.IsNullOrWhiteSpace(topicMajor.FacultyName)
                    && facultyKeys.Contains(NormalizeAccessKey(topicMajor.FacultyName)));
        }

        private static string NormalizeAccessKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim()
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        private async Task<Major?> ResolvePrimaryMajorAsync((List<int> MajorIds, List<string> FacultyNames) access)
        {
            if (access.MajorIds.Any())
            {
                var major = await _context.Majors
                    .Where(m => m.IsActive && access.MajorIds.Contains(m.Id))
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync();
                if (major != null)
                {
                    return major;
                }
            }

            var facultyKeys = access.FacultyNames
                .Select(NormalizeAccessKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            if (!facultyKeys.Any())
            {
                return null;
            }

            var activeMajors = await _context.Majors
                .Where(m => m.IsActive)
                .OrderBy(m => m.Id)
                .ToListAsync();

            return activeMajors.FirstOrDefault(m =>
                facultyKeys.Contains(NormalizeAccessKey(m.FacultyName))
                || facultyKeys.Contains(NormalizeAccessKey(m.Name))
                || (!string.IsNullOrWhiteSpace(m.MajorCode)
                    && facultyKeys.Contains(NormalizeAccessKey(m.MajorCode))));
        }

        private async Task<string> GetStudentEligibilityErrorMessageAsync(string? studentId)
        {
            if (!string.IsNullOrWhiteSpace(studentId)
                && await _context.Users.AnyAsync(u => u.Id == studentId && u.HasCompletedThesis))
            {
                return "Bạn đã hoàn thành KLTN nên không thể đăng ký hoặc đề xuất ở các đợt sau.";
            }

            return "Bạn chưa nằm trong danh sách sinh viên đủ điều kiện của đợt đăng ký hiện tại.";
        }

        private async Task<bool> HasPendingProposalAsync(string? studentId, int? registrationPeriodId = null)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return false;
            }

            var query = _context.Topics.Where(t =>
                t.CreatedByStudentId == studentId
                && t.IsStudentProposed
                && !t.IsApproved
                && t.Status != TopicStatus.Rejected);

            if (registrationPeriodId.HasValue)
            {
                query = query.Where(t => t.RegistrationPeriodId == registrationPeriodId.Value);
            }

            return await query.AnyAsync();
        }

        private async Task<bool> StudentHasActiveRegistrationAsync(string? studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return false;
            }

            return await _context.Registrations.AnyAsync(r =>
                r.StudentId == studentId &&
                (r.Status == "Pending" || r.Status == "Approved"));
        }

        private async Task<List<ApplicationUser>> GetLecturerOptionsForAccessAsync(List<string> facultyNames)
        {
            if (!facultyNames.Any())
            {
                return new List<ApplicationUser>();
            }

            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            var lecturerIds = lecturers.Select(l => l.Id).ToList();

            var assignedLecturerIds = await _context.UserMajors
                .Include(um => um.Major)
                .Where(um => lecturerIds.Contains(um.UserId)
                    && um.Major.FacultyName != null
                    && facultyNames.Contains(um.Major.FacultyName))
                .Select(um => um.UserId)
                .Distinct()
                .ToListAsync();

            return lecturers
                .Where(l => assignedLecturerIds.Contains(l.Id)
                    || (!string.IsNullOrWhiteSpace(l.Faculty)
                        && facultyNames.Contains(l.Faculty, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(l => l.FullName)
                .ToList();
        }

        [Authorize(Roles = "Admin,Lecturer")]
        public IActionResult Create()
        {
            return User.IsInRole("Lecturer")
                ? RedirectToAction("Create", "Lecturer")
                : RedirectToAction("ManageTopics", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Create(Topic topic)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            ModelState.Remove("Lecturer");
            ModelState.Remove("Major");
            ModelState.Remove("Student");
            ModelState.Remove("Registrations");
            ModelState.Remove("Comments");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ!";
                return User.IsInRole("Lecturer")
                    ? RedirectToAction("Create", "Lecturer")
                    : RedirectToAction("ManageTopics", "Admin");
            }

            topic.Title = topic.Title?.Trim() ?? string.Empty;
            topic.Description = topic.Description?.Trim() ?? string.Empty;
            topic.Semester = string.IsNullOrWhiteSpace(topic.Semester) ? activePeriod.Name : topic.Semester.Trim();
            topic.RegistrationPeriodId = activePeriod.Id;
            topic.MaxStudents = Math.Clamp(topic.MaxStudents <= 0 ? 1 : topic.MaxStudents, 1, 10);
            topic.Deadline = topic.Deadline == default ? DateTime.Now.AddMonths(3) : topic.Deadline;
            topic.CreatedAt = DateTime.Now;
            topic.LecturerId = User.IsInRole("Lecturer") ? uid : topic.LecturerId;

            if (User.IsInRole("Lecturer"))
            {
                var access = await GetUserTopicAccessAsync(uid);
                if (!topic.MajorId.HasValue
                    || !(access.MajorIds.Contains(topic.MajorId.Value)
                        || await _context.Majors.AnyAsync(m => m.Id == topic.MajorId.Value
                            && m.FacultyName != null
                            && access.FacultyNames.Contains(m.FacultyName))))
                {
                    TempData["Error"] = "Bạn chỉ được tạo đề tài thuộc khoa/chuyên ngành đã được Admin phân công.";
                    return RedirectToAction("Create", "Lecturer");
                }
            }

            topic.IsApproved = User.IsInRole("Admin");
            topic.IsRegistrationOpen = topic.IsApproved;
            topic.Status = topic.IsApproved ? TopicStatus.Available : TopicStatus.Pending;

            topic.TopicCode = $"TP{DateTime.Now.Ticks.ToString()[^5..]}";

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo đề tài thành công!";

            return User.IsInRole("Lecturer")
                ? RedirectToAction("ThesisManagement", "Lecturer")
                : RedirectToAction("ManageTopics", "Admin");
        }
    }
}
