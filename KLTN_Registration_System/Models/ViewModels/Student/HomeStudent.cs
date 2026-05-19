using System.Collections.Generic;

namespace KLTN_Registration_System.Models.ViewModels.Student
{
    public class HomeStudent
    {
        public string StudentName { get; set; } = string.Empty;
        public int DaysLeft { get; set; }

        public string CurrentTopic { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // ✅ FIX: khởi tạo tránh null
        public List<TopicVM> SuggestedTopics { get; set; } = new List<TopicVM>();
        public List<DeadlineVM> Deadlines { get; set; } = new List<DeadlineVM>();
        public List<NotificationVM> Notifications { get; set; } = new List<NotificationVM>();

        // --- BỔ SUNG MỚI (CHỈ THÊM) ---
        /// <summary>
        /// ID của đề tài hiện tại để làm link "Xem chi tiết" nhanh
        /// </summary>
        public int? CurrentTopicId { get; set; }
    }

    public class TopicVM
    {
        public string? Category { get; set; }
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Lecturer { get; set; } = string.Empty;

        // 🔥 giữ nguyên
        public string Department { get; set; } = string.Empty;
        public int CurrentStudents { get; set; }
        public int MaxStudents { get; set; }

        // --- BỔ SUNG MỚI (CHỈ THÊM) ---
        /// <summary>
        /// Để hiển thị Badge màu sắc trên UI (VD: Easy - Green, Hard - Red)
        /// </summary>
        public string? BadgeClass { get; set; }
    }

    public class DeadlineVM
    {
        public string Date { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SubContent { get; set; } = string.Empty;

        // --- BỔ SUNG MỚI (CHỈ THÊM) ---
        /// <summary>
        /// Dùng để định dạng CSS nếu deadline sắp hết hạn (VD: < 3 ngày thì đổi màu đỏ)
        /// </summary>
        public bool IsUrgent { get; set; }
    }

    public class NotificationVM
    {
        public string Title { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;

        // --- BỔ SUNG MỚI (CHỈ THÊM) ---
        public bool IsRead { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
