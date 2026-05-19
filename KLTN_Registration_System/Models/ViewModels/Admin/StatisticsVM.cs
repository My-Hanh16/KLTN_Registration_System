namespace KLTN_Registration_System.Models.ViewModels.Admin
{
    public class StatisticsVM
    {
        // Tổng quan
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalTopics { get; set; }

        // Đăng ký
        public int ApprovedRegistrations { get; set; }
        public int PendingRegistrations { get; set; }
        public int RejectedRegistrations { get; set; }

        // Tỷ lệ
        public double RegistrationRate { get; set; }

        // Chart
        public List<string> DepartmentLabels { get; set; } = new();
        public List<int> DepartmentValues { get; set; } = new();

        public List<string> StatusLabels { get; set; } = new();
        public List<int> StatusValues { get; set; } = new();

        // Timeline
        public List<string> TimelineLabels { get; set; } = new();
        public List<int> TimelineValues { get; set; } = new();

        // Top lecturer
        public List<TopLecturerVM> TopLecturers { get; set; } = new();
    }

    public class TopLecturerVM
    {
        public string LecturerName { get; set; }
        public int TopicCount { get; set; }
        public int ApprovedCount { get; set; }
    }
}