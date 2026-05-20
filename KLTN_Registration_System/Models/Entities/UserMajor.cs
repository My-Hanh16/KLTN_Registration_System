namespace KLTN_Registration_System.Models.Entities
{
    public class UserMajor
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int MajorId { get; set; }
        public Major Major { get; set; } = null!;
    }
}
