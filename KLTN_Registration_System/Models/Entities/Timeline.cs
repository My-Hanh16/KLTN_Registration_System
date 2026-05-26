using System.ComponentModel.DataAnnotations;

namespace KLTN_Registration_System.Models.Entities
{
    public class Timeline
    {
        public int Id { get; set; }

        public int? RegistrationPeriodId { get; set; }
        public RegistrationPeriod? RegistrationPeriod { get; set; }

        // Tiêu đề mốc tiến độ
        [Required]
        public string Title { get; set; } = string.Empty;

        // Mô tả
        public string? Description { get; set; }

        // Ngày hiển thị trên timeline
        public DateTime Date { get; set; }

        // Loại timeline
        // Proposal / Report / Final / Defense...
        public string? Type { get; set; }

        // Bật/tắt timeline
        public bool IsActive { get; set; } = true;

        // Có cho phép sinh viên nộp không
        public bool AllowSubmission { get; set; }

        // pdf/docx/zip...
        public string? SubmissionType { get; set; }

        // ═══════════════════════════════
        // DEADLINE CHO SINH VIÊN
        // ═══════════════════════════════

        // Hạn cuối sinh viên nộp bài
        public DateTime? SubmissionDeadline { get; set; }

        // ═══════════════════════════════
        // DEADLINE CHO GIẢNG VIÊN
        // ═══════════════════════════════

        // Hạn cuối giảng viên duyệt
        public DateTime? ReviewDeadline { get; set; }

        // ═══════════════════════════════
        // RELATIONSHIP
        // ═══════════════════════════════

        public virtual ICollection<TimelineSubmission> TimelineSubmissions
        { get; set; } = new List<TimelineSubmission>();
    }
}
