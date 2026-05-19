using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Major
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // 🔥 FIX: User → IdentityUser (Giữ nguyên theo yêu cầu của bạn)
        public List<ApplicationUser>? Users { get; set; }

        public List<Topic>? Topics { get; set; }

        // --- PHẦN BỔ SUNG MỚI (CHỈ THÊM, KHÔNG XÓA) ---

        /// <summary>
        /// Mã chuyên ngành (Ví dụ: CNTT, HTTT, KHMT)
        /// Thường dùng để hiển thị mã lớp hoặc mã sinh viên (VD: IT2024 trong ảnh của bạn)
        /// </summary>
        public string? MajorCode { get; set; }

        /// <summary>
        /// Mô tả ngắn gọn về chuyên ngành
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Trạng thái hoạt động của chuyên ngành (để ẩn/hiện khi đăng ký)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Thuộc về Khoa nào (Ví dụ: Khoa Công nghệ thông tin)
        /// </summary>
        public string? FacultyName { get; set; }
    }
}