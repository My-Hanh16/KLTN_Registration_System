using System;
using Microsoft.AspNetCore.Identity;

namespace KLTN_Registration_System.Models.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public ApplicationUser User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? TargetUrl { get; set; }

        public string? RedirectUrl { get; set; }

        public string? Type { get; set; }

        public int Priority { get; set; } = 0;

        public int? RelatedId { get; set; }
    }
}
