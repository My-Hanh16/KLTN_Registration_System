namespace KLTN_Registration_System.Models.ViewModels.Admin
{
    public class StatisticsVM
    {
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalTopics { get; set; }

        public int ApprovedRegistrations { get; set; }
        public int PendingRegistrations { get; set; }
        public int RejectedRegistrations { get; set; }

        public double RegistrationRate { get; set; }

        public List<string> DepartmentLabels { get; set; } = new();
        public List<int> DepartmentValues { get; set; } = new();

        public List<string> StatusLabels { get; set; } = new();
        public List<int> StatusValues { get; set; } = new();

        public List<string> TimelineLabels { get; set; } = new();
        public List<int> TimelineValues { get; set; } = new();

        public List<TopLecturerVM> TopLecturers { get; set; } = new();
    }

    public class TopLecturerVM
    {
        public string LecturerName { get; set; } = string.Empty;
        public int TopicCount { get; set; }
        public int ApprovedCount { get; set; }
    }
}
