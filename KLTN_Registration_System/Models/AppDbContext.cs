// ============================================================
// FILE: Models/AppDbContext.cs
// ============================================================
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KLTN_Registration_System.Models
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Topic> Topics { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Major> Majors { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<Timeline> Timelines { get; set; }
        public DbSet<TimelineSubmission> TimelineSubmissions { get; set; }
        public DbSet<TopicComment> TopicComments { get; set; }
        public DbSet<TimelineSubmissionVersion> TimelineSubmissionVersions { get; set; }
        public DbSet<UserMajor> UserMajors { get; set; }
        public DbSet<RegistrationPeriod> RegistrationPeriods { get; set; }
        public DbSet<PeriodStudent> PeriodStudents { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RegistrationPeriod>(e =>
            {
                e.Property(p => p.Name).HasMaxLength(80);
                e.Property(p => p.AcademicYear).HasMaxLength(20);
                e.Property(p => p.SemesterCode).HasMaxLength(20);
                e.Property(p => p.IsActive).HasDefaultValue(false);
                e.Property(p => p.CreatedAt).HasDefaultValueSql("GETDATE()");
                e.HasIndex(p => p.Name).IsUnique();
                e.HasIndex(p => p.IsActive);
            });

            builder.Entity<TopicComment>(e =>
            {
                e.HasOne(c => c.Topic)
                 .WithMany(t => t.Comments)
                 .HasForeignKey(c => c.TopicId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(c => c.Sender)
                 .WithMany()
                 .HasForeignKey(c => c.SenderId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Registration>(e =>
            {
                e.Property(r => r.Status).HasDefaultValue("Pending");
                e.Property(r => r.Priority).HasDefaultValue(1);

                e.HasOne(r => r.Student)
                 .WithMany(u => u.Registrations)
                 .HasForeignKey(r => r.StudentId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(r => r.Topic)
                 .WithMany(t => t.Registrations)
                 .HasForeignKey(r => r.TopicId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(r => r.RegistrationPeriod)
                 .WithMany(p => p.Registrations)
                 .HasForeignKey(r => r.RegistrationPeriodId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Topic>(e =>
            {
                e.Property(t => t.IsApproved).HasDefaultValue(false);
                e.Property(t => t.IsRegistrationOpen).HasDefaultValue(true);
                e.Property(t => t.IsStudentProposed).HasDefaultValue(false);

                e.HasOne(t => t.Lecturer)
                 .WithMany()
                 .HasForeignKey(t => t.LecturerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(t => t.Student)
                 .WithMany()
                 .HasForeignKey(t => t.CreatedByStudentId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(t => t.Major)
                 .WithMany(m => m.Topics)
                 .HasForeignKey(t => t.MajorId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(t => t.RegistrationPeriod)
                 .WithMany(p => p.Topics)
                 .HasForeignKey(t => t.RegistrationPeriodId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.Property(t => t.Status)
                 .HasConversion<string>();

                e.Property(t => t.Level)
                 .HasConversion<string>();
            });

            builder.Entity<Notification>(e =>
            {
                e.Property(n => n.Priority).HasDefaultValue(0);
                e.Property(n => n.Title).HasMaxLength(200);
                e.Property(n => n.Type).HasMaxLength(50);
                e.Property(n => n.RedirectUrl).HasMaxLength(300);

                e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
                e.HasIndex(n => new { n.Type, n.CreatedAt });

                e.HasOne(n => n.User)
                 .WithMany(u => u.Notifications)
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ApplicationUser>(e =>
            {
                e.HasOne(u => u.Major)
                 .WithMany(m => m.Users)
                 .HasForeignKey(u => u.MajorId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Major>(e =>
            {
                e.Property(m => m.IsActive).HasDefaultValue(true);
            });

            builder.Entity<UserMajor>(e =>
            {
                e.HasKey(um => new { um.UserId, um.MajorId });

                e.HasOne(um => um.User)
                 .WithMany(u => u.UserMajors)
                 .HasForeignKey(um => um.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(um => um.Major)
                 .WithMany(m => m.UserMajors)
                 .HasForeignKey(um => um.MajorId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PeriodStudent>(e =>
            {
                e.HasKey(ps => new { ps.RegistrationPeriodId, ps.StudentId });
                e.Property(ps => ps.IsEligible).HasDefaultValue(true);
                e.Property(ps => ps.ImportedAt).HasDefaultValueSql("GETDATE()");

                e.HasOne(ps => ps.RegistrationPeriod)
                 .WithMany(p => p.PeriodStudents)
                 .HasForeignKey(ps => ps.RegistrationPeriodId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(ps => ps.Student)
                 .WithMany(u => u.PeriodStudents)
                 .HasForeignKey(ps => ps.StudentId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(ps => ps.StudentId);
                e.HasIndex(ps => ps.IsEligible);
            });
            builder.Entity<TimelineSubmission>(e =>
            {
                e.HasOne(ts => ts.Timeline)
                 .WithMany(t => t.TimelineSubmissions)
                 .HasForeignKey(ts => ts.TimelineId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(ts => ts.Student)
                 .WithMany()
                 .HasForeignKey(ts => ts.StudentId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(ts => ts.ReviewedBy)
                 .WithMany()
                 .HasForeignKey(ts => ts.ReviewedById)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TimelineSubmissionVersion>(e =>
            {
                e.HasOne(v => v.TimelineSubmission)
                 .WithMany(s => s.Versions)
                 .HasForeignKey(v => v.TimelineSubmissionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Timeline>(e =>
            {
                e.HasOne(t => t.RegistrationPeriod)
                 .WithMany(p => p.Timelines)
                 .HasForeignKey(t => t.RegistrationPeriodId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
