using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? UserCode { get; set; }

        // Thêm các trường mới cho Giảng viên
        public string? Faculty { get; set; }    // Khoa
        public string? Degree { get; set; }     // Trình độ (Thạc sĩ, Tiến sĩ...)
        public string? Position { get; set; }   // Chức vụ

        // ── Thêm để AdminController có thể Include/query ──────────
        // Không phá cấu trúc cũ, chỉ thêm navigation
        public int? MajorId { get; set; }
        public Major? Major { get; set; }

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}