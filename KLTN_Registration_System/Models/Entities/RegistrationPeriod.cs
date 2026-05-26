using System.ComponentModel.DataAnnotations;

namespace KLTN_Registration_System.Models.Entities
{
    public class RegistrationPeriod
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string AcademicYear { get; set; } = string.Empty;

        [MaxLength(20)]
        public string SemesterCode { get; set; } = "HK2";

        public DateTime SemesterStart { get; set; }
        public DateTime SemesterEnd { get; set; }
        public DateTime RegistrationOpenAt { get; set; }
        public DateTime RegistrationCloseAt { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Topic> Topics { get; set; } = new List<Topic>();
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<Timeline> Timelines { get; set; } = new List<Timeline>();
        public ICollection<PeriodStudent> PeriodStudents { get; set; } = new List<PeriodStudent>();
    }
}
