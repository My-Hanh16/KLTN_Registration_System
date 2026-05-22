// ============================================================
// FILE: Hubs/ChatHub.cs
// ============================================================

using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace KLTN_Registration_System.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            ILogger<ChatHub> logger)
        {
            _db = db;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        // =====================================================
        // JOIN ROOM
        // =====================================================
        public async Task JoinTopicRoom(int topicId)
        {
            if (topicId <= 0) return;

            var user = await _userManager.GetUserAsync(Context.User!);
            if (user == null) return;

            if (!await CanAccessTopic(topicId, user))
                return;

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"topic-{topicId}");
        }

        public async Task LeaveTopicRoom(int topicId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"topic-{topicId}");
        }

        // =====================================================
        // TYPING
        // =====================================================
        public async Task Typing(int topicId)
        {
            var user = await _userManager.GetUserAsync(Context.User!);
            if (user == null || !await CanAccessTopic(topicId, user)) return;

            var name = user?.FullName ?? user?.UserName ?? "Ai đó";

            await Clients.OthersInGroup($"topic-{topicId}")
                .SendAsync("UserTyping", name);
        }

        // =====================================================
        // SEND MESSAGE
        // =====================================================
        public async Task SendMessage(
            int topicId,
            string? content,
            string? attachmentUrl = null,
            string? attachmentName = null)
        {
            try
            {
                if (topicId <= 0) return;

                content = content?.Trim();

                attachmentUrl = string.IsNullOrWhiteSpace(attachmentUrl)
                    ? null : attachmentUrl.Trim();

                attachmentName = string.IsNullOrWhiteSpace(attachmentName)
                    ? null : attachmentName.Trim();

                var storedAttachmentName = NormalizeStoredAttachmentName(attachmentUrl);

                if (string.IsNullOrWhiteSpace(content) && storedAttachmentName == null)
                    return;

                if (content != null && content.Length > 2000)
                    return;

                var user = await _userManager.GetUserAsync(Context.User!);
                if (user == null) return;

                var topic = await _db.Topics
                    .Include(t => t.Lecturer)
                    .Include(t => t.Registrations!)
                        .ThenInclude(r => r.Student)
                    .FirstOrDefaultAsync(t => t.Id == topicId);

                if (topic == null) return;
                if (topic.Status == TopicStatus.Closed) return;

                var roles = await _userManager.GetRolesAsync(user);

                bool isLecturer =
                    roles.Contains("Lecturer") &&
                    topic.LecturerId == user.Id;

                bool isStudent =
                    roles.Contains("Student") &&
                    (topic.Registrations ?? new List<Registration>()).Any(r =>
                        r.StudentId == user.Id &&
                        r.Status == "Approved");

                if (!isLecturer && !isStudent) return;

                var comment = new TopicComment
                {
                    TopicId = topicId,
                    SenderId = user.Id,

                    Content = content ?? "",

                    AttachmentUrl = storedAttachmentName,
                    AttachmentName = attachmentName,

                    CreatedAt = DateTime.UtcNow,

                    SenderRole = isLecturer ? "Lecturer" : "Student",

                    IsDeleted = false,
                    IsRead = false
                };

                _db.TopicComments.Add(comment);
                await _db.SaveChangesAsync();

                await NotifyChatRecipientsByEmailAsync(
                    topic,
                    user,
                    comment.SenderRole ?? "Student",
                    comment.Content,
                    comment.AttachmentName);

                // 🔥 IMPORTANT: FORMAT CHUẨN KHỚP VIEW
                await Clients.Group($"topic-{topicId}")
                    .SendAsync("ReceiveMessage", new
                    {
                        id = comment.Id,
                        topicId = topicId,
                        senderId = user.Id,
                        senderName = user.FullName ?? user.UserName ?? "",
                        senderRole = comment.SenderRole,
                        content = comment.Content,
                        attachmentUrl = comment.AttachmentUrl == null
                            ? null
                            : $"/Chat/DownloadUploadedFile?topicId={topicId}&fileName={Uri.EscapeDataString(comment.AttachmentUrl)}",
                        attachmentName = comment.AttachmentName,

                        createdAt = comment.CreatedAt.ToLocalTime(),
                        createdAtShort = comment.CreatedAt.ToLocalTime().ToString("HH:mm"),
                        createdAtDate = comment.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy")
                    });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        // =====================================================
        // DELETE MESSAGE
        // =====================================================
        public async Task DeleteMessage(int commentId)
        {
            var user = await _userManager.GetUserAsync(Context.User!);
            if (user == null) return;

            var comment = await _db.TopicComments
                .FirstOrDefaultAsync(c =>
                    c.Id == commentId &&
                    c.SenderId == user.Id &&
                    !c.IsDeleted);

            if (comment == null) return;

            comment.IsDeleted = true;
            await _db.SaveChangesAsync();

            await Clients.Group($"topic-{comment.TopicId}")
                .SendAsync("MessageDeleted", commentId);
        }

        private async Task<bool> CanAccessTopic(int topicId, ApplicationUser user)
        {
            var topic = await _db.Topics
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return false;

            var roles = await _userManager.GetRolesAsync(user);

            return (roles.Contains("Lecturer") && topic.LecturerId == user.Id) ||
                   (roles.Contains("Student") &&
                    (topic.Registrations ?? new List<Registration>()).Any(r =>
                        r.StudentId == user.Id &&
                        r.Status == "Approved"));
        }

        private static string? NormalizeStoredAttachmentName(string? attachmentUrl)
        {
            if (string.IsNullOrWhiteSpace(attachmentUrl))
                return null;

            if (attachmentUrl.StartsWith("/Chat/DownloadUploadedFile", StringComparison.OrdinalIgnoreCase))
            {
                var queryStart = attachmentUrl.IndexOf('?');
                if (queryStart >= 0)
                {
                    var query = QueryHelpers.ParseQuery(attachmentUrl[queryStart..]);
                    if (query.TryGetValue("fileName", out var fileNameValue))
                    {
                        var fileName = Path.GetFileName(fileNameValue.ToString());
                        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
                    }
                }
            }

            var storedName = Path.GetFileName(attachmentUrl);
            return string.IsNullOrWhiteSpace(storedName) ? null : storedName;
        }

        private async Task NotifyChatRecipientsByEmailAsync(
            Topic topic,
            ApplicationUser sender,
            string senderRole,
            string content,
            string? attachmentName)
        {
            try
            {
                var recipients = new List<ApplicationUser>();

                if (senderRole == "Student")
                {
                    if (topic.Lecturer != null)
                    {
                        recipients.Add(topic.Lecturer);
                    }
                }
                else
                {
                    recipients.AddRange((topic.Registrations ?? new List<Registration>())
                        .Where(r => r.Status == "Approved" && r.Student != null)
                        .Select(r => r.Student!));
                }

                recipients = recipients
                    .Where(r => r.Id != sender.Id && !string.IsNullOrWhiteSpace(r.Email))
                    .GroupBy(r => r.Email!.Trim().ToUpperInvariant())
                    .Select(g => g.First())
                    .ToList();

                if (!recipients.Any())
                {
                    return;
                }

                var senderName = WebUtility.HtmlEncode(sender.FullName ?? sender.Email ?? "Người dùng");
                var topicTitle = WebUtility.HtmlEncode(topic.Title ?? "Đề tài");
                var safeContent = string.IsNullOrWhiteSpace(content)
                    ? "<em>Người gửi đã gửi một tệp đính kèm.</em>"
                    : WebUtility.HtmlEncode(content).Replace("\n", "<br/>");
                var safeAttachment = string.IsNullOrWhiteSpace(attachmentName)
                    ? string.Empty
                    : $"<p><strong>Tệp đính kèm:</strong> {WebUtility.HtmlEncode(attachmentName)}</p>";

                var subject = $"[KLTN Portal] Tin nhắn mới - {topic.Title}";
                var body = $@"
                    <div style='font-family:Arial,sans-serif;line-height:1.6;color:#0f172a'>
                        <h2 style='margin-bottom:8px'>Bạn có tin nhắn mới trên KLTN Portal</h2>
                        <p><strong>Đề tài:</strong> {topicTitle}</p>
                        <p><strong>Người gửi:</strong> {senderName}</p>
                        <div style='padding:12px 14px;border-left:4px solid #2563eb;background:#f8fafc;margin:12px 0'>
                            {safeContent}
                        </div>
                        {safeAttachment}
                        <p>Vui lòng đăng nhập KLTN Portal để phản hồi trong mục trao đổi.</p>
                    </div>";

                foreach (var recipient in recipients)
                {
                    await _emailService.SendEmailAsync(recipient.Email!, subject, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không gửi được email thông báo chat cho topic {TopicId}", topic.Id);
            }
        }
    }
}
