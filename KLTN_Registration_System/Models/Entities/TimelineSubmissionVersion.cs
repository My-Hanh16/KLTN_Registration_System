using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KLTN_Registration_System.Models.Entities
{
    public class TimelineSubmissionVersion
    {
        public int Id { get; set; }

        public int TimelineSubmissionId { get; set; }

        [ForeignKey("TimelineSubmissionId")]
        public TimelineSubmission? TimelineSubmission { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public int VersionNumber { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public string? Note { get; set; }
    }
}