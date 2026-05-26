using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace KLTN_Registration_System.Models.Entities
{
    public enum TopicLevel
    {
        Easy,
        Medium,
        Hard
    }

    public enum TopicStatus
    {
        Pending,
        Available,
        Full,
        Closed,
        Rejected
    }

    public class Topic
    {
        public int Id { get; set; }

        public string? TopicCode { get; set; }
        public string? Semester { get; set; }
        public int? RegistrationPeriodId { get; set; }
        public RegistrationPeriod? RegistrationPeriod { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string? LecturerId { get; set; }
        public ApplicationUser? Lecturer { get; set; }

        public int? MajorId { get; set; }
        public Major? Major { get; set; }

        public DateTime Deadline { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Faculty { get; set; }

        // ── FIX: Lưu enum dưới dạng string cho khớp với SQL nvarchar(max) ──
        // Không đổi kiểu property (vẫn là enum), chỉ thêm [Column] để EF hiểu
        [Column(TypeName = "nvarchar(max)")]
        public TopicLevel Level { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public TopicStatus Status { get; set; }

        public bool IsApproved { get; set; }
        public bool IsRegistrationOpen { get; set; } = true;

        public int MaxStudents { get; set; } = 1;

        [NotMapped]
        public int CurrentStudents => Registrations?.Count(r => r.Status == "Approved") ?? 0;

        public string? DepartmentName { get; set; }
        public string? Note { get; set; }
        public string? Category { get; set; }
        public string? CreatedByStudentId { get; set; }
        public bool IsStudentProposed { get; set; } = false;

        [ForeignKey("CreatedByStudentId")]
        public virtual ApplicationUser? Student { get; set; }

        public List<Registration>? Registrations { get; set; }
        // =====================================================
        // COMMENTS / CHAT
        // =====================================================

        public ICollection<TopicComment> Comments { get; set; }
            = new List<TopicComment>();
    }
}
