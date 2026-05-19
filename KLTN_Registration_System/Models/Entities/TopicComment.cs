using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KLTN_Registration_System.Models.Entities
{
    public class TopicComment
    {
        public int Id { get; set; }

        // =====================================================
        // TOPIC
        // =====================================================

        [Required]
        public int TopicId { get; set; }

        [ForeignKey(nameof(TopicId))]
        public virtual Topic Topic { get; set; } = null!;

        // =====================================================
        // SENDER
        // =====================================================

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [ForeignKey(nameof(SenderId))]
        public virtual ApplicationUser Sender { get; set; } = null!;

        // =====================================================
        // MESSAGE CONTENT
        // =====================================================

        [Required(ErrorMessage = "Nội dung không được để trống")]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        // =====================================================
        // ATTACHMENT
        // =====================================================

        // URL file lưu trong wwwroot/uploads/...
        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        [MaxLength(255)]
        public string? AttachmentName { get; set; }

        // =====================================================
        // METADATA
        // =====================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete
        public bool IsDeleted { get; set; } = false;

        // =====================================================
        // ROLE
        // =====================================================

        /// <summary>
        /// Lecturer | Student
        /// Dùng để render bubble trái/phải
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string SenderRole { get; set; } = string.Empty;

        // =====================================================
        // OPTIONAL FEATURES (nên có)
        // =====================================================

        // Đã đọc chưa
        public bool IsRead { get; set; } = false;

        // Tin nhắn chỉnh sửa
        public bool IsEdited { get; set; } = false;

        public DateTime? EditedAt { get; set; }

        // Reply message
        public int? ParentCommentId { get; set; }

        [ForeignKey(nameof(ParentCommentId))]
        public virtual TopicComment? ParentComment { get; set; }

        public virtual ICollection<TopicComment> Replies { get; set; }
            = new List<TopicComment>();
    }
}