using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? UserCode { get; set; }

        public string? Faculty { get; set; }    
        public string? Degree { get; set; }    
        public string? Position { get; set; }  
        public int? MajorId { get; set; }
        public Major? Major { get; set; }
        public bool HasCompletedThesis { get; set; } = false;
        public DateTime? ThesisCompletedAt { get; set; }

        public ICollection<UserMajor> UserMajors { get; set; } = new List<UserMajor>();
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<PeriodStudent> PeriodStudents { get; set; } = new List<PeriodStudent>();
    }
}
