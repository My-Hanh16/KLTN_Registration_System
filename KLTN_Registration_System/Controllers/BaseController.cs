using DocumentFormat.OpenXml.InkML;
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;

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
            }
            catch
            {
                // Tránh crash layout nếu DB lỗi
                ViewBag.UnreadCount = 0;
            }

            await next();
        }
        protected async Task SetStudentLayoutData()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null) return;

            var registration = await _db.Registrations
                .FirstOrDefaultAsync(r =>
                    r.StudentId == user.Id &&
                    r.Status == "Approved");

            ViewBag.ActiveTopicId = registration?.TopicId;
        }
    }
}