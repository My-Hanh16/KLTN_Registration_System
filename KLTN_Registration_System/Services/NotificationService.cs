using KLTN_Registration_System.Hubs;
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        // THÊM
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,

            // THÊM
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;

            // THÊM
            _hubContext = hubContext;
        }

        public async Task SendDualNotification(
            string userId,
            string title,
            string content,
            string type = "System",
            int priority = 0,
            int? relatedId = null)
        {
            // ====================================================
            // 1. Lưu Database
            // ====================================================

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Content = content,
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = type,
                Priority = priority,
                RelatedId = relatedId,
                RedirectUrl = relatedId != null
                    ? $"/Topic/Details/{relatedId}"
                    : "/Notification"
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            // ====================================================
            // 2. Realtime SignalR
            // ====================================================

            int unreadCount = await _context.Notifications
                .CountAsync(n =>
                    n.UserId == userId &&
                    !n.IsRead);

            await _hubContext.Clients
                .Group(userId)
                .SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    type = notification.Type,
                    priority = notification.Priority,
                    redirectUrl = notification.RedirectUrl,
                    createdAt = notification.CreatedAt
                        .ToString("HH:mm dd/MM/yyyy"),
                    unreadCount = unreadCount
                });

            // ====================================================
            // 3. Gửi Email
            // ====================================================

            var student = await _userManager.FindByIdAsync(userId);

            if (student != null &&
                !string.IsNullOrEmpty(student.Email))
            {
                string emailBody = $@"
                    <h3>Thông báo từ Hệ thống Quản lý Khóa luận</h3>

                    <p>
                        <b>Tiêu đề:</b> {title}
                    </p>

                    <p>
                        <b>Nội dung:</b> {content}
                    </p>

                    <p>
                        Vui lòng đăng nhập vào website để biết thêm chi tiết.
                    </p>";

                await _emailService.SendEmailAsync(
                    student.Email,
                    title,
                    emailBody);
            }
        }

        // ====================================================
        // Đánh dấu đã đọc
        // ====================================================

        public async Task MarkAsRead(
            int notificationId,
            string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.UserId == userId);

            if (notification != null &&
                !notification.IsRead)
            {
                notification.IsRead = true;

                await _context.SaveChangesAsync();

                int unreadCount = await _context.Notifications
                    .CountAsync(n =>
                        n.UserId == userId &&
                        !n.IsRead);

                await _hubContext.Clients
                    .Group(userId)
                    .SendAsync("UpdateBadge", unreadCount);
            }
        }

        // ====================================================
        // Đánh dấu tất cả đã đọc
        // ====================================================

        public async Task MarkAllAsRead(string userId)
        {
            var unread = await _context.Notifications
                .Where(n =>
                    n.UserId == userId &&
                    !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group(userId)
                .SendAsync("UpdateBadge", 0);
        }
    }
}