namespace KLTN_Registration_System.Models.ViewModels
{
    public class AdminDashboard
    {
        public int TotalTopics { get; set; }
        public int PendingApprovals { get; set; }
        public int TotalLecturers { get; set; }
        public double RegistrationRate { get; set; }

        public List<DepartmentStatVM> DepartmentStats { get; set; } = new();
        public List<ActivityLogVM> RecentActivities { get; set; } = new();
        public List<TopicItemVM> NewTopics { get; set; } = new();
    }

    public class DepartmentStatVM
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>Giá trị % tương đối (0-100) để vẽ thanh biểu đồ</summary>
        public int Value { get; set; }
        public int Count { get; set; }
    }

    public class ActivityLogVM
    {
        public string Message { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string Icon { get; set; } = "notifications";
        public string ColorClass { get; set; } = "bg-slate-100 text-slate-600";
    }

    public class TopicItemVM
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LecturerName { get; set; } = string.Empty;
        public string Faculty { get; set; } = string.Empty;
    }
    public class UpdateRegVM
    {
        public int Id { get; set; }
        public bool IsOpen { get; set; }
    }
}