using System.ComponentModel.DataAnnotations.Schema;

namespace KLTN_Registration_System.Models.Entities
{
    public class PeriodStudent
    {
        public int RegistrationPeriodId { get; set; }
        public RegistrationPeriod RegistrationPeriod { get; set; } = null!;

        public string StudentId { get; set; } = string.Empty;

        [ForeignKey(nameof(StudentId))]
        public ApplicationUser Student { get; set; } = null!;

        public DateTime ImportedAt { get; set; } = DateTime.Now;
        public bool IsEligible { get; set; } = true;
    }
}
