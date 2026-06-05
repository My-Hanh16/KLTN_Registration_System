using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Major
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public List<ApplicationUser>? Users { get; set; }

        public List<Topic>? Topics { get; set; }
        public ICollection<UserMajor> UserMajors { get; set; } = new List<UserMajor>();

        public string? MajorCode { get; set; }

        public string? Description { get; set; }


        public bool IsActive { get; set; } = true;

        public string? FacultyName { get; set; }
    }
}
