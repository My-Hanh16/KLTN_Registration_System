using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly AppDbContext _db;
        protected readonly AppDbContext context;
        protected readonly UserManager<ApplicationUser> _userManager;

        protected BaseController(AppDbContext db,UserManager<ApplicationUser> userManager)
        {
            _db = db;
            context = db;
            _userManager = userManager;
        }

        /// <summary>
        /// Chạy trước mọi action
        /// Đếm số thông báo chưa đọc để render badge notification
        /// </summary>
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            try
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userId = _userManager.GetUserId(User);

                    if (!string.IsNullOrEmpty(userId))
                    {
                        ViewBag.UnreadCount = await _db.Notifications
                            .AsNoTracking()
                            .CountAsync(n =>
                                n.UserId == userId &&
                                !n.IsRead);
                    }
                    else
                    {
                        ViewBag.UnreadCount = 0;
                    }
                }
                else
                {
                    ViewBag.UnreadCount = 0;
                }

                var selectedAdminPeriod = HttpContext.Session.GetString("AdminSelectedPeriodName");
                if (!string.IsNullOrWhiteSpace(selectedAdminPeriod))
                {
                    ViewBag.AdminSelectedPeriodName = selectedAdminPeriod;
                    var parts = selectedAdminPeriod.Split('-', 2);
                    if (parts.Length == 2)
                    {
                        ViewBag.AdminSelectedSemester = parts[0];
                        ViewBag.AdminSelectedYear = parts[1];
                    }
                }
            }
            catch
            {
                ViewBag.UnreadCount = 0;
            }

            await next();
        }
        protected async Task SetStudentLayoutData()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null) return;

            var activePeriod = await GetOrCreateActiveRegistrationPeriodAsync();
            ViewBag.StudentHasCompletedThesis = user.HasCompletedThesis;
            ViewBag.CanParticipateInCurrentPeriod = await IsStudentEligibleForPeriodAsync(user.Id, activePeriod.Id);

            var registration = await FilterRegistrationsByActivePeriod(_db.Registrations.Include(r => r.Topic), activePeriod)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == user.Id &&
                    r.Status == "Approved");

            ViewBag.ActiveTopicId = registration?.TopicId;
        }

        protected async Task<RegistrationPeriod> GetOrCreateActiveRegistrationPeriodAsync()
        {
            var activePeriods = await _db.RegistrationPeriods
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var activePeriod = activePeriods.FirstOrDefault();

            if (activePeriod != null)
            {
                if (activePeriods.Count > 1)
                {
                    foreach (var extraPeriod in activePeriods.Skip(1))
                    {
                        extraPeriod.IsActive = false;
                    }

                    await _db.SaveChangesAsync();
                }

                await EnsureLegacyEligibleStudentsSeededAsync(activePeriod);
                return activePeriod;
            }

            activePeriod = await _db.RegistrationPeriods
                .OrderByDescending(p => p.AcademicYear)
                .ThenByDescending(p => p.SemesterCode)
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (activePeriod != null)
            {
                activePeriod.IsActive = true;
                await _db.SaveChangesAsync();
                await EnsureLegacyEligibleStudentsSeededAsync(activePeriod);
                return activePeriod;
            }

            string academicYear = GetCurrentAcademicYear();
            string semesterCode = "HK2";
            string name = $"{semesterCode}-{academicYear}";
            var settings = await _db.Settings.ToListAsync();

            DateTime semesterStart = ParseSettingDate(settings, "Semester_Start", DateTime.Today);
            DateTime semesterEnd = ParseSettingDate(settings, "Semester_End", DateTime.Today.AddMonths(4));
            DateTime registrationOpenAt = ParseSettingDate(settings, "Registration_Start", DateTime.Today);
            DateTime registrationCloseAt = ParseSettingDate(settings, "Registration_End", DateTime.Today.AddDays(30));

            activePeriod = await _db.RegistrationPeriods
                .FirstOrDefaultAsync(p => p.Name == name);

            if (activePeriod == null)
            {
                activePeriod = new RegistrationPeriod
                {
                    Name = name,
                    AcademicYear = academicYear,
                    SemesterCode = semesterCode,
                    SemesterStart = semesterStart,
                    SemesterEnd = semesterEnd,
                    RegistrationOpenAt = registrationOpenAt,
                    RegistrationCloseAt = registrationCloseAt,
                    IsActive = true
                };
                _db.RegistrationPeriods.Add(activePeriod);
            }
            else
            {
                activePeriod.IsActive = true;
            }

            await _db.SaveChangesAsync();
            await EnsureLegacyEligibleStudentsSeededAsync(activePeriod);
            return activePeriod;
        }

        protected async Task EnsureLegacyEligibleStudentsSeededAsync(RegistrationPeriod activePeriod)
        {
            if (await _db.PeriodStudents.AnyAsync())
            {
                return;
            }

            var studentRoleId = await _db.Roles
                .Where(r => r.Name == "Student")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(studentRoleId))
            {
                return;
            }

            var studentIds = await _db.UserRoles
                .Where(ur => ur.RoleId == studentRoleId)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            if (!studentIds.Any())
            {
                return;
            }

            foreach (var studentId in studentIds)
            {
                _db.PeriodStudents.Add(new PeriodStudent
                {
                    RegistrationPeriodId = activePeriod.Id,
                    StudentId = studentId,
                    ImportedAt = DateTime.Now,
                    IsEligible = !await _db.Users
                        .AnyAsync(u => u.Id == studentId && u.HasCompletedThesis)
                });
            }

            await _db.SaveChangesAsync();
        }

        protected IQueryable<Topic> FilterTopicsByActivePeriod(IQueryable<Topic> query, RegistrationPeriod activePeriod)
        {
            return query.Where(t =>
                t.RegistrationPeriodId == activePeriod.Id
                || (t.RegistrationPeriodId == null && t.Semester == activePeriod.Name));
        }

        protected IQueryable<Registration> FilterRegistrationsByActivePeriod(IQueryable<Registration> query, RegistrationPeriod activePeriod)
        {
            return query.Where(r =>
                r.RegistrationPeriodId == activePeriod.Id
                || (r.RegistrationPeriodId == null && r.Topic != null && r.Topic.Semester == activePeriod.Name));
        }

        protected IQueryable<Timeline> FilterTimelinesByActivePeriod(IQueryable<Timeline> query, RegistrationPeriod activePeriod)
        {
            return query.Where(t => t.RegistrationPeriodId == activePeriod.Id);
        }

        protected async Task<bool> IsStudentEligibleForPeriodAsync(string? studentId, int registrationPeriodId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return false;
            }

            return await _db.PeriodStudents.AnyAsync(ps =>
                ps.StudentId == studentId &&
                ps.RegistrationPeriodId == registrationPeriodId &&
                ps.IsEligible &&
                !ps.Student.HasCompletedThesis);
        }

        protected static string GetCurrentAcademicYear()
        {
            var now = DateTime.Now;
            int startYear = now.Month >= 8 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static DateTime ParseSettingDate(IEnumerable<Setting> settings, string name, DateTime fallback)
        {
            string? value = settings.FirstOrDefault(s => s.Name == name)?.Value;
            return DateTime.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}
