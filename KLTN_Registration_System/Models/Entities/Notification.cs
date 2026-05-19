using System;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        // 🔥 FIX: int → string
        public string UserId { get; set; }

        public string Content { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        // 🔥 FIX: User → IdentityUser
        public ApplicationUser User { get; set; }
        public string Title { get; set; }
        public string? TargetUrl { get; set; }

        // --- PHẦN BỔ SUNG MỚI (CHỈ THÊM, KHÔNG XÓA) ---

        /// <summary>
        /// Đường dẫn liên kết khi người dùng nhấn vào thông báo
        /// Ví dụ: /Student/TopicDetails/5
        /// </summary>
        public string? RedirectUrl { get; set; }

        /// <summary>
        /// Phân loại thông báo (Ví dụ: "System", "Registration", "Deadline")
        /// Giúp bạn hiển thị các Icon khác nhau trên UI (Chuông, Xuất hiện lỗi, v.v.)
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Mức độ quan trọng (0: Thường, 1: Cao)
        /// Để bạn có thể tô màu đỏ cho các thông báo khẩn cấp trên UI
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// ID của đối tượng liên quan (ví dụ TopicId) 
        /// Giúp truy vấn nhanh mà không cần parse chuỗi URL
        /// </summary>
        public int? RelatedId { get; set; }
    }
}