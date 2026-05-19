// ============================================================
// FILE: Hubs/ChatHub.cs
// ============================================================

using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // =====================================================
        // JOIN ROOM
        // =====================================================
        public async Task JoinTopicRoom(int topicId)
        {
            if (topicId <= 0) return;

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
            var name = user?.FullName ?? user?.UserName ?? "Ai đó";

            await Clients.OthersInGroup($"topic-{topicId}")
                .SendAsync("UserTyping", name);
        }

        // =====================================================
        // SEND MESSAGE
        // =====================================================
        public async Task SendMessage(
            int topicId,
            string content,
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

                if (string.IsNullOrWhiteSpace(content) && attachmentUrl == null)
                    return;

                if (content != null && content.Length > 2000)
                    return;

                var user = await _userManager.GetUserAsync(Context.User!);
                if (user == null) return;

                var topic = await _db.Topics
                    .Include(t => t.Registrations)
                    .FirstOrDefaultAsync(t => t.Id == topicId);

                if (topic == null) return;
                if (topic.Status == TopicStatus.Closed) return;

                var roles = await _userManager.GetRolesAsync(user);

                bool isLecturer =
                    roles.Contains("Lecturer") &&
                    topic.LecturerId == user.Id;

                bool isStudent =
                    roles.Contains("Student") &&
                    topic.Registrations.Any(r =>
                        r.StudentId == user.Id &&
                        r.Status == "Approved");

                if (!isLecturer && !isStudent) return;

                var comment = new TopicComment
                {
                    TopicId = topicId,
                    SenderId = user.Id,

                    Content = content ?? "",

                    AttachmentUrl = attachmentUrl,
                    AttachmentName = attachmentName,

                    CreatedAt = DateTime.UtcNow,

                    SenderRole = isLecturer ? "Lecturer" : "Student",

                    IsDeleted = false,
                    IsRead = false
                };

                _db.TopicComments.Add(comment);
                await _db.SaveChangesAsync();

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
                        attachmentUrl = comment.AttachmentUrl,
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
    }
}