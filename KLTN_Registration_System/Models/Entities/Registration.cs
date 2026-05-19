using System;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Registration
    {
        public int Id { get; set; }

        // 🔥 FIX: int → string
        public string StudentId { get; set; } = string.Empty;

        public int TopicId { get; set; }

        public string Status { get; set; } = "Pending"; // Ví dụ: "Pending", "Approved", "Rejected"

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 🔥 FIX: User → IdentityUser
        public virtual ApplicationUser Student { get; set; } = null!;
        public Topic Topic { get; set; } = null!;

        // --- PHẦN BỔ SUNG MỚI ĐỂ KHỚP UI & QUẢN LÝ ---

        /// <summary>
        /// Ngày giảng viên hoặc hệ thống phê duyệt/từ chối đăng ký này
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Phản hồi của giảng viên nếu từ chối (Rejected) 
        /// Giúp sinh viên biết lý do tại sao không được chọn
        /// </summary>
        public string? Feedback { get; set; }

        /// <summary>
        /// Thứ tự ưu tiên nếu sinh viên được phép đăng ký nhiều nguyện vọng
        /// (Ví dụ: Nguyện vọng 1, Nguyện vọng 2)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Lưu vết người thực hiện phê duyệt (nếu không phải là Giảng viên của Topic đó)
        /// </summary>
        public string? ApprovedBy { get; set; }
    }
}
