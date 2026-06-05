using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KLTN_Registration_System.Models.Entities
{
    public class TopicComment
    {
        public int Id { get; set; }


        [Required]
        public int TopicId { get; set; }

        [ForeignKey(nameof(TopicId))]
        public virtual Topic Topic { get; set; } = null!;


        [Required]
        public string SenderId { get; set; } = string.Empty;

        [ForeignKey(nameof(SenderId))]
        public virtual ApplicationUser Sender { get; set; } = null!;


        [Required(ErrorMessage = "Nội dung không được để trống")]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        [MaxLength(255)]
        public string? AttachmentName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public bool IsDeleted { get; set; } = false;

        [Required]
        [MaxLength(20)]
        public string SenderRole { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public bool IsEdited { get; set; } = false;

        public DateTime? EditedAt { get; set; }

        public int? ParentCommentId { get; set; }

        [ForeignKey(nameof(ParentCommentId))]
        public virtual TopicComment? ParentComment { get; set; }

        public virtual ICollection<TopicComment> Replies { get; set; }
            = new List<TopicComment>();
    }
}