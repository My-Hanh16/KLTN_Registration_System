using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Models.Enums;
using KLTN_Registration_System.Models.ViewModels.Student;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using X.PagedList;
using X.PagedList.Extensions;

namespace KLTN_Registration_System.Controllers.Student
{
    [Authorize(Roles = "Student")]
    public class StudentController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public StudentController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService)
        :base(context, userManager)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Home()
        {
            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            bool isEligible = await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id);

            var settings = await _context.Settings.ToListAsync();
            var startStr = settings.FirstOrDefault(s => s.Name == "Registration_Start")?.Value;
            var endStr = settings.FirstOrDefault(s => s.Name == "Registration_End")?.Value;
            var portalOpenStr = settings.FirstOrDefault(s => s.Name == "IsTopicRegistrationOpen")?.Value;

            DateTime startDate = DateTime.TryParse(startStr, out var sd) ? sd : DateTime.MinValue;
            DateTime endDate = DateTime.TryParse(endStr, out var ed) ? ed : DateTime.MaxValue;
            DateTime now = DateTime.Now;

            bool isPortalOpen = bool.TryParse(portalOpenStr, out var configuredPortalOpen) && configuredPortalOpen;
            bool isOpening = isPortalOpen && now >= startDate && now <= endDate;
            int daysLeft = Math.Max(0, (endDate - now).Days);

            var registration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .FirstOrDefaultAsync(r => r.StudentId == user.Id &&
                    (r.Status == "Pending" || r.Status == "Approved"));

            string currentTopic = registration?.Topic?.Title ?? "Chưa đăng ký";
            string status = registration?.Status ?? "Chưa có";
            int? currentTopicId = registration?.TopicId;

            ViewBag.ActiveTopicId = registration?.TopicId;

            
            var topicQuery = _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Include(t => t.Registrations)
                .Where(t => t.IsApproved
                    && !t.IsStudentProposed
                    && t.CreatedByStudentId == null)
                .AsQueryable();
            topicQuery = FilterTopicsByActivePeriod(topicQuery, activePeriod);
            if (!isEligible)
            {
                topicQuery = topicQuery.Where(_ => false);
            }

            var allowedMajorIds = await GetStudentMajorIdsAsync(user.Id);
            var allowedFacultyNames = await GetStudentFacultyNamesAsync(user.Id);
            var allowedFacultyKeys = allowedFacultyNames
                .Select(NormalizeAccessKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var candidateTopics = await topicQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var topics = (allowedMajorIds.Any() || allowedFacultyKeys.Any())
                ? candidateTopics
                    .Where(t =>
                        (t.MajorId.HasValue && allowedMajorIds.Contains(t.MajorId.Value))
                        || (t.Major?.FacultyName != null
                            && allowedFacultyKeys.Contains(NormalizeAccessKey(t.Major.FacultyName)))
                        || (!string.IsNullOrWhiteSpace(t.Faculty)
                            && allowedFacultyKeys.Contains(NormalizeAccessKey(t.Faculty))))
                    .Take(2)
                    .ToList()
                : new List<Topic>();

            var suggestedTopics = topics.Select(t => new TopicVM
            {
                Id = t.Id,
                Title = t.Title,
                Category = t.Category,
                Level = t.Level.ToString() switch
                {
                    "Hard" => "Nâng cao",
                    "Medium" => "Trung bình",
                    _ => "Cơ bản"
                },
                BadgeClass = t.Level.ToString() switch
                {
                    "Hard" => "bg-level-hard",
                    "Medium" => "bg-level-medium",
                    _ => "bg-level-easy"
                },
                Lecturer = !string.IsNullOrEmpty(t.Lecturer?.FullName)
                    ? t.Lecturer.FullName
                    : (t.Lecturer?.Email ?? "Chưa có GV"),
                Department = t.DepartmentName ?? t.Major?.Name ?? "Bộ môn chung",
                CurrentStudents = t.Registrations?.Count(r => r.Status == "Approved") ?? 0,
                MaxStudents = t.MaxStudents
            }).ToList();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(3)
                .ToListAsync();

            var notifVM = notifications.Select(n => new NotificationVM
            {
                Title = n.Title,
                Time = n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                IsRead = n.IsRead,
                RedirectUrl = n.RedirectUrl
            }).ToList();

            var activeTimelines = await FilterTimelinesByActivePeriod(_context.Timelines, activePeriod)
                .Where(t => t.IsActive)
                .ToListAsync();

            var timelines = activeTimelines
                .Select(t => new
                {
                    Timeline = t,
                    EffectiveDeadline = t.ReviewDeadline ?? t.SubmissionDeadline ?? t.Date
                })
                .Where(t => t.EffectiveDeadline >= now)
                .OrderBy(t => t.EffectiveDeadline)
                .Take(3)
                .ToList();

            var deadlines = timelines.Select(t => new DeadlineVM
            {
                Date = t.EffectiveDeadline.Day.ToString("00"),
                Month = t.EffectiveDeadline.Month.ToString("00"),
                Content = t.Timeline.Title,
                SubContent = t.Timeline.Description ?? "",
                IsUrgent = (t.EffectiveDeadline - now).TotalDays <= 3
            }).ToList();

            var model = new HomeStudent
            {
                StudentName = !string.IsNullOrEmpty(user.FullName) ? user.FullName : (user.Email ?? "Sinh viên"),
                DaysLeft = daysLeft,
                CurrentTopic = currentTopic,
                CurrentTopicId = currentTopicId,
                Status = status,
                SuggestedTopics = suggestedTopics,
                Deadlines = deadlines,
                Notifications = notifVM
            };

            ViewBag.IsOpening = isOpening;
            ViewBag.TopicRegistrationPortalOpen = isPortalOpen;
            ViewBag.RegistrationStartDate = startDate == DateTime.MinValue ? "" : startDate.ToString("dd/MM/yyyy");
            ViewBag.RegistrationEndDate = endDate.ToString("dd/MM/yyyy");
            ViewBag.HasRegistered = registration != null;
            ViewBag.RegisteredTopicId = registration?.TopicId ?? 0;
            ViewBag.IsEligibleForCurrentPeriod = isEligible;
            ViewBag.StudentHasCompletedThesis = user.HasCompletedThesis;
            ViewBag.CanParticipateInCurrentPeriod = isEligible;
            ViewBag.StudentParticipationMessage = user.HasCompletedThesis
                ? "Bạn đã hoàn thành KLTN. Tài khoản vẫn có thể xem lịch sử, nhưng không thể đăng ký hoặc đề xuất ở các đợt sau."
                : !isEligible
                    ? "Bạn chưa nằm trong danh sách sinh viên đủ điều kiện của đợt đăng ký hiện tại."
                    : "";
            ViewBag.FullName = model.StudentName;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Register(int topicId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (!await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id))
            {
                TempData["Error"] = user.HasCompletedThesis
                    ? "Bạn đã hoàn thành KLTN nên không thể đăng ký ở các đợt sau."
                    : "Bạn chưa nằm trong danh sách sinh viên đủ điều kiện của đợt đăng ký hiện tại.";
                return RedirectToAction(nameof(Home));
            }

            var settings = await _context.Settings.ToListAsync();
            var startStr = settings.FirstOrDefault(s => s.Name == "Registration_Start")?.Value;
            var endStr = settings.FirstOrDefault(s => s.Name == "Registration_End")?.Value;
            var now = DateTime.Now;

            if (DateTime.TryParse(startStr, out var startDate) && now < startDate)
            {
                TempData["Error"] = $"Cổng đăng ký chưa mở. Thời gian bắt đầu: {startDate:dd/MM/yyyy HH:mm}.";
                return RedirectToAction(nameof(Home));
            }

            if (DateTime.TryParse(endStr, out var endDate) && now > endDate)
            {
                TempData["Error"] = "Đã hết thời hạn đăng ký đề tài!";
                return RedirectToAction(nameof(Home));
            }

            bool alreadyRegistered = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .AnyAsync(r => r.StudentId == user.Id && (r.Status == "Pending" || r.Status == "Approved"));

            if (alreadyRegistered)
            {
                TempData["Error"] = "Bạn đang có một đề tài trong trạng thái chờ hoặc đã được duyệt!";
                return RedirectToAction(nameof(Home));
            }

            if (await HasPendingProposalAsync(user.Id, activePeriod.Id))
            {
                TempData["Error"] = "Bạn đang có đề xuất đề tài chờ duyệt. Hãy hủy đề xuất đó trước khi đăng ký đề tài của giảng viên.";
                return RedirectToAction(nameof(Home));
            }

            var topic = await _context.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null)
            {
                TempData["Error"] = "Đề tài không tồn tại!";
                return RedirectToAction(nameof(Home));
            }

            if (topic.RegistrationPeriodId != activePeriod.Id && topic.Semester != activePeriod.Name)
            {
                TempData["Error"] = "Đề tài này không thuộc đợt đăng ký đang mở.";
                return RedirectToAction(nameof(Home));
            }

            if (!topic.IsApproved || !topic.IsRegistrationOpen || topic.IsStudentProposed)
            {
                TempData["Error"] = "Đề tài này hiện không mở cho sinh viên đăng ký.";
                return RedirectToAction(nameof(Home));
            }

            if (!await CanStudentAccessTopicAsync(user.Id, topic.MajorId, topic.Faculty))
            {
                TempData["Error"] = "Bạn chỉ được đăng ký đề tài thuộc khoa/chuyên ngành của mình.";
                return RedirectToAction(nameof(Home));
            }

            int approvedCount = topic.Registrations?.Count(r => r.Status == "Approved") ?? 0;
            if (approvedCount >= topic.MaxStudents)
            {
                TempData["Error"] = "Đề tài đã đủ số lượng sinh viên!";
                return RedirectToAction(nameof(Home));
            }

            _context.Registrations.Add(new Registration
            {
                StudentId = user.Id,
                TopicId = topicId,
                RegistrationPeriodId = activePeriod.Id,
                Status = "Pending",
                CreatedAt = DateTime.Now,
                Priority = 1
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(topic.LecturerId))
                await AddNotification(topic.LecturerId,
                    "Sinh viên đăng ký đề tài",
                    $"Sinh viên {user.FullName ?? user.Email} vừa đăng ký đề tài \"{topic.Title}\".",
                    "NewRegistration", "/Lecturer/Approval");

            TempData["Success"] = "Đăng ký thành công! Vui lòng chờ giảng viên phê duyệt.";
            return RedirectToAction(nameof(Home));
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyRegistration()
        {
            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var myRegistrations = await _context.Registrations
                .Include(r => r.Topic).ThenInclude(t => t!.Lecturer)
                .Include(r => r.Topic).ThenInclude(t => t!.Major)
                .Where(r => r.StudentId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var proposedTopics = await _context.Topics
                .Include(t => t.Lecturer)
                .Include(t => t.Major)
                .Where(t => t.CreatedByStudentId == user.Id && t.IsStudentProposed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.ProposedTopics = proposedTopics;
            var canParticipate = await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id);
            ViewBag.StudentHasCompletedThesis = user.HasCompletedThesis;
            ViewBag.CanParticipateInCurrentPeriod = canParticipate;
            ViewBag.ActivePeriodName = activePeriod.Name;

            var approvedRegistration = await FilterRegistrationsByActivePeriod(
                    _context.Registrations.Include(r => r.Topic),
                    activePeriod)
                .FirstOrDefaultAsync(r => r.StudentId == user.Id && r.Status == "Approved");

            ViewBag.ActiveTopicId = approvedRegistration?.TopicId;

            return View(myRegistrations);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelRegistration(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var registration = await _context.Registrations
                .Include(r => r.Topic)
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == user.Id);

            if (registration == null)
            {
                TempData["Error"] = "Không tìm thấy đăng ký.";
                return RedirectToAction(nameof(MyRegistration));
            }

            if (registration.Status != "Pending")
            {
                TempData["Error"] = "Không thể hủy đăng ký đã được duyệt hoặc bị từ chối.";
                return RedirectToAction(nameof(MyRegistration));
            }

            string topicTitle = registration.Topic?.Title ?? "";
            _context.Registrations.Remove(registration);
            await _context.SaveChangesAsync();

            await AddNotification(user.Id,
                "Hủy đăng ký",
                $"Bạn đã hủy đăng ký đề tài \"{topicTitle}\" thành công.",
                "System", "/Topic/Index");

            TempData["Success"] = "Đã hủy đăng ký thành công.";
            return RedirectToAction(nameof(MyRegistration));
        }

        public async Task<IActionResult> Notifications(int? page)
        {
            await SetStudentLayoutData();

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            int pageSize = 6;
            int pageNumber = Math.Max(page ?? 1, 1);

            var query = _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt);

            ViewBag.TotalUnreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            ViewBag.TopicNotificationCount = await _context.Notifications
                .CountAsync(n =>
                    n.UserId == user.Id &&
                    (n.Type == "TopicApproved"
                     || n.Type == "TopicRejected"
                     || n.Type == "NewRegistration"
                     || n.Type == "RegistrationApproved"
                     || n.Type == "RegistrationRejected"
                     || n.Type == "Registration"
                     || n.Type == "NewTopic"
                     || n.Type == "Topic"
                     || (n.Title != null && (n.Title.Contains("duyệt") || n.Title.Contains("từ chối")))));

            var pagedNotifications =
                query.ToPagedList(pageNumber, pageSize);

            return View(pagedNotifications);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            await _notificationService.MarkAllAsRead(user.Id);

            if (!IsAjaxRequest())
            {
                TempData["Success"] = unreadCount > 0
                    ? $"Đã đánh dấu {unreadCount} thông báo là đã đọc."
                    : "Không có thông báo chưa đọc.";
                return RedirectToAction(nameof(Notifications));
            }

            return Json(new { success = true, count = unreadCount });
        }

        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var notification = await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (notification == null)
            {
                TempData["Error"] = "Không tìm thấy thông báo.";
                return RedirectToAction(nameof(Notifications));
            }

            await _notificationService.MarkAsRead(id, user.Id);

            var redirectUrl = NotificationService.NormalizeRedirectUrl(notification.RedirectUrl)
                ?? NotificationService.NormalizeRedirectUrl(notification.TargetUrl)
                ?? Url.Action(nameof(Notifications), "Student")
                ?? "/Student/Notifications";

            return Redirect(redirectUrl);
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
        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0 });

            var count = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(new { count });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Profile()
        {
            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            var myReg = await FilterRegistrationsByActivePeriod(_context.Registrations
                .Include(r => r.Topic).ThenInclude(t => t!.Lecturer)
                .Include(r => r.Topic).ThenInclude(t => t!.Major), activePeriod)
                .Where(r => r.StudentId == user.Id && (r.Status == "Pending" || r.Status == "Approved"))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            ViewBag.CurrentRegistration = myReg;
            ViewBag.Majors = await GetStudentMajorsAsync(user.Id);
            ViewBag.TotalNotifications = await _context.Notifications.CountAsync(n => n.UserId == user.Id);
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(n => n.UserId == user.Id && !n.IsRead);
            ViewBag.TotalRegistrations = await _context.Registrations.CountAsync(r => r.StudentId == user.Id);
            ViewBag.ActivePeriodName = activePeriod.Name;
            ViewBag.StudentHasCompletedThesis = user.HasCompletedThesis;
            ViewBag.ThesisCompletedAt = user.ThesisCompletedAt;
            ViewBag.CanParticipateInCurrentPeriod = await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id);

            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> UpdateProfile(string? phoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            phoneNumber = phoneNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(phoneNumber)
                && (phoneNumber.Length > 20 || !phoneNumber.All(c => char.IsDigit(c) || c == '+' || c == ' ' || c == '-' || c == '.')))
            {
                TempData["Error"] = "Số điện thoại không hợp lệ.";
                return RedirectToAction(nameof(Profile));
            }

            user.PhoneNumber = phoneNumber;

            var result = await _userManager.UpdateAsync(user);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "Cập nhật hồ sơ thành công!"
                : "Lỗi: " + string.Join(", ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Profile));
        }


        [Authorize(Roles = "Student")]
        public async Task<IActionResult> ProposeTopic()
        {
            await SetStudentLayoutData();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            if (!await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id))
            {
                TempData["Error"] = user.HasCompletedThesis
                    ? "Bạn đã hoàn thành KLTN nên không thể tham gia đăng ký hoặc đề xuất lại."
                    : "Tài khoản của bạn chưa thuộc danh sách sinh viên đủ điều kiện của đợt hiện tại.";
                return RedirectToAction(nameof(Home));
            }

            bool hasActiveRegistration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .AnyAsync(r => r.StudentId == user.Id && (r.Status == "Pending" || r.Status == "Approved"));
            if (hasActiveRegistration)
            {
                TempData["Error"] = "Bạn đang có đề tài chờ duyệt hoặc đã được duyệt, không thể đề xuất thêm.";
                return RedirectToAction(nameof(MyRegistration));
            }

            var myProposals = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .Include(t => t.Lecturer)
                .Where(t => t.CreatedByStudentId == user.Id && t.IsStudentProposed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.MyProposals = myProposals;
            var facultyNames = await GetStudentFacultyNamesAsync(user.Id);
            ViewBag.PrimaryMajor = await ResolveStudentPrimaryMajorAsync(user.Id);

            ViewBag.Lecturers = await GetLecturersByFacultyAsync(facultyNames);

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> ProposeTopic(
            string title, string description,
            string? category, string? preferredLecturerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();

            if (!await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id))
            {
                TempData["Error"] = user.HasCompletedThesis
                    ? "Bạn đã hoàn thành KLTN nên không thể tham gia đăng ký hoặc đề xuất lại."
                    : "Tài khoản của bạn chưa thuộc danh sách sinh viên đủ điều kiện của đợt hiện tại.";
                return RedirectToAction(nameof(ProposeTopic));
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["Error"] = "Tiêu đề và mô tả không được để trống.";
                return RedirectToAction(nameof(ProposeTopic));
            }

            bool hasActiveRegistration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .AnyAsync(r => r.StudentId == user.Id && (r.Status == "Pending" || r.Status == "Approved"));
            if (hasActiveRegistration)
            {
                TempData["Error"] = "Bạn đang có đề tài chờ duyệt hoặc đã được duyệt, không thể đề xuất thêm.";
                return RedirectToAction(nameof(ProposeTopic));
            }

            var primaryMajor = await ResolveStudentPrimaryMajorAsync(user.Id);
            if (primaryMajor == null)
            {
                TempData["Error"] = "Tài khoản của bạn chưa được gán chuyên ngành nên chưa thể đề xuất đề tài.";
                return RedirectToAction(nameof(ProposeTopic));
            }

            if (!string.IsNullOrWhiteSpace(preferredLecturerId))
            {
                var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
                var facultyNames = await GetStudentFacultyNamesAsync(user.Id);
                var allowedLecturerIds = (await GetLecturersByFacultyAsync(facultyNames))
                    .Select(l => l.Id)
                    .ToHashSet();
                bool lecturerExists = lecturers.Any(l => l.Id == preferredLecturerId)
                    && allowedLecturerIds.Contains(preferredLecturerId);

                if (!lecturerExists)
                {
                    TempData["Error"] = "Giảng viên hướng dẫn được chọn không thuộc khoa của bạn.";
                    return RedirectToAction(nameof(ProposeTopic));
                }
            }

            int pendingProposals = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .CountAsync(t => t.CreatedByStudentId == user.Id
                              && t.IsStudentProposed
                              && t.Status == TopicStatus.Pending);
            if (pendingProposals >= 3)
            {
                TempData["Error"] = "Bạn đã có 3 đề xuất đang chờ duyệt, không thể thêm.";
                return RedirectToAction(nameof(ProposeTopic));
            }

            var topicCount = await FilterTopicsByActivePeriod(_context.Topics, activePeriod)
                .CountAsync(t => t.CreatedByStudentId == user.Id);
            var studentCodePart = !string.IsNullOrWhiteSpace(user.UserCode)
                ? user.UserCode.Trim().ToUpperInvariant()
                : user.Id[..Math.Min(4, user.Id.Length)].ToUpperInvariant();

            var topic = new Topic
            {
                Title = title.Trim(),
                Description = description.Trim(),
                Category = category?.Trim(),
                MajorId = primaryMajor.Id,
                DepartmentName = primaryMajor.Name,
                Faculty = primaryMajor.FacultyName,
                LecturerId = preferredLecturerId,   
                CreatedByStudentId = user.Id,
                IsStudentProposed = true,
                IsApproved = false,
                IsRegistrationOpen = false,
                Status = TopicStatus.Pending,
                Level = TopicLevel.Medium,
                MaxStudents = 1,
                TopicCode = $"PROP-{studentCodePart}-{(topicCount + 1):D3}",
                Deadline = DateTime.Now.AddMonths(4),
                CreatedAt = DateTime.Now,
                Semester = activePeriod.Name,
                RegistrationPeriodId = activePeriod.Id
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(preferredLecturerId))
            {
                await AddNotification(preferredLecturerId,
                    "Sinh viên đề xuất đề tài",
                    $"Sinh viên {user.FullName ?? user.Email} muốn bạn xem xét hướng dẫn đề tài: \"{title}\".",
                    "NewTopic", "/Lecturer/Approval?tab=proposals");
            }
            else
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in admins)
                {
                    await AddNotification(admin.Id,
                        "Đề xuất cần phân công giảng viên",
                        $"Sinh viên {user.FullName ?? user.Email} đề xuất đề tài \"{title}\" nhưng chưa chọn giảng viên hướng dẫn.",
                        "NewTopic", "/Admin/StudentProposals?status=pending");
                }
            }

            TempData["Success"] = !string.IsNullOrEmpty(preferredLecturerId)
                ? "Đề xuất đề tài thành công! Giảng viên mong muốn sẽ xem xét trước khi chuyển Admin."
                : "Đề xuất đề tài thành công! Admin sẽ phân công giảng viên phù hợp.";
            return RedirectToAction(nameof(ProposeTopic));
        }


        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelProposal(int id, string? returnTo = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == id
                    && t.CreatedByStudentId == user.Id
                    && t.IsStudentProposed
                    && t.Status == TopicStatus.Pending);

            if (topic == null)
            {
                TempData["Error"] = "Không tìm thấy đề xuất hoặc đề xuất đã được duyệt.";
                return string.Equals(returnTo, "myregistration", StringComparison.OrdinalIgnoreCase)
                    ? RedirectToAction(nameof(MyRegistration))
                    : RedirectToAction(nameof(ProposeTopic));
            }

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã huỷ đề xuất \"{topic.Title}\".";
            return string.Equals(returnTo, "myregistration", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(MyRegistration))
                : RedirectToAction(nameof(ProposeTopic));
        }
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Timeline()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            var timelines = await FilterTimelinesByActivePeriod(_context.Timelines, activePeriod)
                .Where(t => t.IsActive)
                .OrderBy(t => t.Date)
                .ToListAsync();

            var submissions = await _context.TimelineSubmissions
                .Include(s => s.Versions)
                .Where(s => s.StudentId == user.Id)
                .ToListAsync();

            var approvedRegistration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == user.Id &&
                    r.Status == "Approved");

            var requiredTimelines = timelines
                .Where(t => t.AllowSubmission)
                .ToList();

            var latestSubmissionsByTimeline = submissions
                .GroupBy(s => s.TimelineId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(s => s.SubmittedAt).First());

            int approvedRequiredCount = requiredTimelines.Count(t =>
                latestSubmissionsByTimeline.TryGetValue(t.Id, out var latest)
                && latest.Status == SubmissionStatus.Approved);

            int pendingRequiredCount = requiredTimelines.Count(t =>
                latestSubmissionsByTimeline.TryGetValue(t.Id, out var latest)
                && latest.Status == SubmissionStatus.Pending);

            int rejectedRequiredCount = requiredTimelines.Count(t =>
                latestSubmissionsByTimeline.TryGetValue(t.Id, out var latest)
                && latest.Status == SubmissionStatus.Rejected);

            int missingRequiredCount = requiredTimelines.Count(t =>
                !latestSubmissionsByTimeline.ContainsKey(t.Id));

            var now = DateTime.Now;
            bool isDefenseEligible = approvedRegistration != null
                && requiredTimelines.Any()
                && approvedRequiredCount == requiredTimelines.Count;
            bool isDefenseWindowClosed = requiredTimelines.Any()
                && requiredTimelines.All(t => (t.SubmissionDeadline ?? t.Date) < now);
            var blockingRequiredTimelineNames = requiredTimelines
                .Where(t => !latestSubmissionsByTimeline.TryGetValue(t.Id, out var latest)
                    || latest.Status != SubmissionStatus.Approved)
                .Select(t => t.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            ViewBag.Submissions = submissions;
            ViewBag.HasApprovedTopic = approvedRegistration != null;
            ViewBag.ActiveTopicId = approvedRegistration?.TopicId;
            ViewBag.RequiredTimelineCount = requiredTimelines.Count;
            ViewBag.ApprovedRequiredTimelineCount = approvedRequiredCount;
            ViewBag.PendingRequiredTimelineCount = pendingRequiredCount;
            ViewBag.RejectedRequiredTimelineCount = rejectedRequiredCount;
            ViewBag.MissingRequiredTimelineCount = missingRequiredCount;
            ViewBag.IsDefenseEligible = isDefenseEligible;
            ViewBag.IsDefenseWindowClosed = isDefenseWindowClosed;
            ViewBag.IsDefenseBlockedByDeadline = approvedRegistration != null
                && requiredTimelines.Any()
                && !isDefenseEligible
                && isDefenseWindowClosed;
            ViewBag.BlockingRequiredTimelineNames = blockingRequiredTimelineNames;
            ViewBag.HasCompletedThesis = user.HasCompletedThesis;
            ViewBag.ThesisCompletedAt = user.ThesisCompletedAt;
            await SetStudentLayoutData();

            return View(timelines);
        }
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitTimeline(
            int timelineId,
            string? content,
            IFormFile file)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var now = DateTime.Now;
            if (user.HasCompletedThesis)
            {
                TempData["Error"] = "Bạn đã hoàn thành khóa luận nên không cần nộp timeline nữa.";
                return RedirectToAction(nameof(Timeline));
            }

            var timeline = await _context.Timelines.FindAsync(timelineId);
            if (timeline == null)
            {
                TempData["Error"] = "Không tìm thấy mốc thời gian.";
                return RedirectToAction(nameof(Timeline));
            }

            if (!timeline.IsActive || !timeline.AllowSubmission)
            {
                TempData["Error"] = "Mốc này không cho phép nộp bài.";
                return RedirectToAction(nameof(Timeline));
            }

            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            if (timeline.RegistrationPeriodId.HasValue && timeline.RegistrationPeriodId.Value != activePeriod.Id)
            {
                TempData["Error"] = "Mốc thời gian này không thuộc đợt khóa luận hiện tại.";
                return RedirectToAction(nameof(Timeline));
            }

            var approvedRegistration = await FilterRegistrationsByActivePeriod(_context.Registrations.Include(r => r.Topic), activePeriod)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == user.Id &&
                    r.Status == "Approved");

            if (approvedRegistration == null)
            {
                TempData["Error"] = "Bạn cần có đề tài đã được duyệt trước khi nộp timeline.";
                return RedirectToAction(nameof(Timeline));
            }

            var submission = await _context.TimelineSubmissions
                .Include(s => s.Versions)
                .Where(s => s.TimelineId == timelineId && s.StudentId == user.Id)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync();

            bool isRejectedResubmission = submission?.Status == SubmissionStatus.Rejected;
            if (timeline.Date > now && !isRejectedResubmission)
            {
                TempData["Error"] = $"Mốc này chưa mở nộp. Thời gian mở: {timeline.Date:dd/MM/yyyy HH:mm}.";
                return RedirectToAction(nameof(Timeline));
            }

            var requiredTimelines = await FilterTimelinesByActivePeriod(_context.Timelines, activePeriod)
                .Where(t => t.IsActive && t.AllowSubmission)
                .ToListAsync();
            bool isDefenseWindowClosed = requiredTimelines.Any()
                && requiredTimelines.All(t => (t.SubmissionDeadline ?? t.Date) < now);
            if (isDefenseWindowClosed)
            {
                var requiredTimelineIds = requiredTimelines.Select(t => t.Id).ToList();
                var studentRequiredSubmissions = requiredTimelineIds.Any()
                    ? await _context.TimelineSubmissions
                        .Where(s => s.StudentId == user.Id && requiredTimelineIds.Contains(s.TimelineId))
                        .ToListAsync()
                    : new List<TimelineSubmission>();
                var latestByTimeline = studentRequiredSubmissions
                    .GroupBy(s => s.TimelineId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(s => s.SubmittedAt).First());
                bool isDefenseEligible = requiredTimelines.Any()
                    && requiredTimelines.All(t =>
                        latestByTimeline.TryGetValue(t.Id, out var latest)
                        && latest.Status == SubmissionStatus.Approved);

                if (!isDefenseEligible)
                {
                    TempData["Error"] = "Timeline bắt buộc đã hết hạn và bạn chưa đủ điều kiện báo cáo khóa luận. Vui lòng liên hệ GVHD hoặc admin để được xem xét.";
                    return RedirectToAction(nameof(Timeline));
                }
            }

            if (timeline.SubmissionDeadline.HasValue
                && now > timeline.SubmissionDeadline.Value
                && !isRejectedResubmission)
            {
                TempData["Error"] =
                    $"Đã quá hạn nộp bài ({timeline.SubmissionDeadline.Value:dd/MM/yyyy HH:mm}). Không thể nộp.";
                return RedirectToAction(nameof(Timeline));
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file";
                return RedirectToAction(nameof(Timeline));
            }

            const long maxFileSize = 20 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                TempData["Error"] = "File không được vượt quá 20MB.";
                return RedirectToAction(nameof(Timeline));
            }

            var allowedExtensions = GetAllowedTimelineExtensions(timeline.SubmissionType);
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                TempData["Error"] = $"Định dạng file không hợp lệ. Chỉ hỗ trợ: {FormatTimelineExtensions(allowedExtensions)}.";
                return RedirectToAction(nameof(Timeline));
            }

            if (submission != null && submission.Status != SubmissionStatus.Rejected)
            {
                TempData["Error"] = submission.Status == SubmissionStatus.Pending
                    ? "Bài của bạn đang chờ giảng viên xem xét. Không thể nộp thêm."
                    : "Bài của bạn đã được duyệt. Không thể nộp lại.";
                return RedirectToAction(nameof(Timeline));
            }

            var uploads = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/uploads/timeline");

            Directory.CreateDirectory(uploads);

            int nextVersion = submission?.Versions.Any() == true
                ? submission.Versions.Max(v => v.VersionNumber) + 1
                : 1;
            var fileName = $"{user.Id}_{timelineId}_{now:yyyyMMddHHmmss}_v{nextVersion}_{Guid.NewGuid():N}{extension}";

            var path = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var filePath = "/uploads/timeline/" + fileName;

            if (submission == null)
            {
                submission = new TimelineSubmission
                {
                    TimelineId = timelineId,
                    StudentId = user.Id,
                    SubmittedAt = now
                };

                _context.TimelineSubmissions.Add(submission);
                await _context.SaveChangesAsync();
            }

            submission.FilePath = filePath;
            submission.FileName = Path.GetFileName(file.FileName);
            submission.ProgressDescription = content?.Trim();
            submission.SubmittedAt = now;
            submission.Status = SubmissionStatus.Pending;
            submission.Comment = null;
            submission.Score = null;
            submission.ReviewedAt = null;
            submission.ReviewedById = null;
            submission.LecturerComment = null;
            submission.IsCompleted = false;

            _context.TimelineSubmissionVersions.Add(new TimelineSubmissionVersion
            {
                TimelineSubmissionId = submission.Id,
                FileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                VersionNumber = nextVersion,
                UploadedAt = now,
                Note = submission.ProgressDescription
            });

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(approvedRegistration.Topic?.LecturerId))
            {
                await AddNotification(
                    approvedRegistration.Topic.LecturerId,
                    "Sinh viên nộp báo cáo tiến độ",
                    $"Sinh viên {user.FullName ?? user.Email} vừa nộp bài cho mốc \"{timeline.Title}\".",
                    "Timeline",
                    "/Lecturer/TimelineManagement");
            }

            TempData["Success"] = "Nộp file thành công. Vui lòng chờ giảng viên xem xét.";

            return RedirectToAction(nameof(Timeline));
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

        private async Task<List<string>> GetStudentFacultyNamesAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                return new List<string>();
            }

            var facultyNames = student.UserMajors
                .Select(um => um.Major?.FacultyName)
                .Append(student.Major?.FacultyName)
                .Append(student.Faculty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!facultyNames.Any())
            {
                facultyNames = await _context.Registrations
                    .Include(r => r.Topic)
                        .ThenInclude(t => t!.Major)
                    .Where(r => r.StudentId == studentId
                        && (r.Status == "Pending" || r.Status == "Approved")
                        && r.Topic != null
                        && r.Topic.Major != null
                        && r.Topic.Major.FacultyName != null)
                    .Select(r => r.Topic!.Major!.FacultyName!)
                    .Distinct()
                    .ToListAsync();
            }

            return facultyNames
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<List<int>> GetStudentMajorIdsAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.UserMajors)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                return new List<int>();
            }

            var majorIds = student.UserMajors
                .Select(um => um.MajorId)
                .ToHashSet();

            if (student.MajorId.HasValue)
            {
                majorIds.Add(student.MajorId.Value);
            }

            return majorIds.ToList();
        }

        private async Task<Major?> ResolveStudentPrimaryMajorAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                return null;
            }

            if (student.Major != null && student.Major.IsActive)
            {
                return student.Major;
            }

            var assignedMajor = student.UserMajors
                .Select(um => um.Major)
                .FirstOrDefault(m => m != null && m.IsActive);
            if (assignedMajor != null)
            {
                return assignedMajor;
            }

            var facultyNames = await GetStudentFacultyNamesAsync(studentId);
            var facultyKeys = facultyNames
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

        private async Task<bool> HasPendingProposalAsync(string studentId, int? registrationPeriodId = null)
        {
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

        private async Task<List<Major>> GetStudentMajorsAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.Major)
                .Include(u => u.UserMajors)
                    .ThenInclude(um => um.Major)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                return new List<Major>();
            }

            var assignedMajorIds = student.UserMajors
                .Select(um => um.MajorId)
                .ToHashSet();

            if (student.MajorId.HasValue)
            {
                assignedMajorIds.Add(student.MajorId.Value);
            }

            var facultyNames = student.UserMajors
                .Select(um => um.Major?.FacultyName)
                .Append(student.Major?.FacultyName)
                .Append(student.Faculty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return await _context.Majors
                .Where(m => m.IsActive
                    && (assignedMajorIds.Contains(m.Id)
                        || (m.FacultyName != null && facultyNames.Contains(m.FacultyName))))
                .OrderBy(m => m.FacultyName)
                .ThenBy(m => m.Name)
                .ToListAsync();
        }

        private async Task<bool> CanStudentAccessTopicAsync(string studentId, int? topicMajorId, string? topicFaculty = null)
        {
            var assignedMajorIds = await GetStudentMajorIdsAsync(studentId);
            if (topicMajorId.HasValue && assignedMajorIds.Contains(topicMajorId.Value))
            {
                return true;
            }

            var facultyNames = await GetStudentFacultyNamesAsync(studentId);
            var facultyKeys = facultyNames.Select(NormalizeAccessKey).ToList();
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

            return !string.IsNullOrWhiteSpace(topicMajor.FacultyName)
                && facultyKeys.Contains(NormalizeAccessKey(topicMajor.FacultyName));
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

        private async Task<List<ApplicationUser>> GetLecturersByFacultyAsync(List<string> facultyNames)
        {
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            var facultyKeys = facultyNames
                .Select(NormalizeAccessKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            if (!facultyKeys.Any())
            {
                return new List<ApplicationUser>();
            }

            var lecturerIds = lecturers.Select(l => l.Id).ToList();
            var lecturerMajorAssignments = await _context.UserMajors
                .Include(um => um.Major)
                .Where(um => lecturerIds.Contains(um.UserId))
                .ToListAsync();

            var assignedLecturerIds = lecturerMajorAssignments
                .Where(um => um.Major?.FacultyName != null
                    && facultyKeys.Contains(NormalizeAccessKey(um.Major.FacultyName)))
                .Select(um => um.UserId)
                .Distinct()
                .ToHashSet();

            return lecturers
                .Where(l => assignedLecturerIds.Contains(l.Id)
                    || (!string.IsNullOrWhiteSpace(l.Faculty)
                        && facultyKeys.Contains(NormalizeAccessKey(l.Faculty))))
                .OrderBy(l => l.FullName)
                .ToList();
        }
    }
}
