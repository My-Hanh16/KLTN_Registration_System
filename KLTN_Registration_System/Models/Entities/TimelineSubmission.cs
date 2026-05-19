using System.ComponentModel.DataAnnotations.Schema;
using KLTN_Registration_System.Models.Enums;

namespace KLTN_Registration_System.Models.Entities
{
    public class TimelineSubmission
    {
        public int Id { get; set; }

        // Timeline
        public int TimelineId { get; set; }

        [ForeignKey("TimelineId")]
        public Timeline? Timeline { get; set; }

        // Student
        public string? StudentId { get; set; }

        [ForeignKey("StudentId")]
        public ApplicationUser? Student { get; set; }

        // Submission
        public string FilePath { get; set; } = string.Empty;

        public string? FileName { get; set; }

        // mô tả tiến độ
        public string? ProgressDescription { get; set; }

        // % hoàn thành
        public int ProgressPercent { get; set; } = 0;

        public DateTime SubmittedAt { get; set; }

        // Workflow
        public bool IsCompleted { get; set; } = false;

        public SubmissionStatus Status { get; set; }
            = SubmissionStatus.Pending;

        // Lecturer review
        public string? Comment { get; set; }

        public double? Score { get; set; }

        // Reviewer
        public string? ReviewedById { get; set; }

        [ForeignKey("ReviewedById")]
        public ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }
        public string? LecturerComment { get; set; }
        public virtual ICollection<TimelineSubmissionVersion> Versions { get; set; }
            = new List<TimelineSubmissionVersion>();
    }
}