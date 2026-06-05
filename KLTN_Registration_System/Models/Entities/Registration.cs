using System;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Registration
    {
        public int Id { get; set; }

        public string StudentId { get; set; } = string.Empty;

        public int TopicId { get; set; }
        public int? RegistrationPeriodId { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser Student { get; set; } = null!;
        public Topic Topic { get; set; } = null!;
        public RegistrationPeriod? RegistrationPeriod { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public string? Feedback { get; set; }
        public int Priority { get; set; } = 1;
        public string? ApprovedBy { get; set; }
    }
}
