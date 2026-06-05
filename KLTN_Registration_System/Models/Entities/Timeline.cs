using System.ComponentModel.DataAnnotations;

namespace KLTN_Registration_System.Models.Entities
{
    public class Timeline
    {
        public int Id { get; set; }

        public int? RegistrationPeriodId { get; set; }
        public RegistrationPeriod? RegistrationPeriod { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime Date { get; set; }

        public string? Type { get; set; }

        public bool IsActive { get; set; } = true;

        public bool AllowSubmission { get; set; }

        public string? SubmissionType { get; set; }

        public DateTime? SubmissionDeadline { get; set; }

        public DateTime? ReviewDeadline { get; set; }

        public virtual ICollection<TimelineSubmission> TimelineSubmissions
        { get; set; } = new List<TimelineSubmission>();
    }
}
