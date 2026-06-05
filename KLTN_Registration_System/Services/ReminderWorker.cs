using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace KLTN_Registration_System.Services
{
    public class ReminderWorker
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notiService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReminderWorker(AppDbContext context, NotificationService notiService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notiService = notiService;
            _userManager = userManager;
        }
        public async Task CheckAndSendReminders()
        {
            Debug.WriteLine("===> BAT DAU QUET SINH VIEN");

            var now = DateTime.Now;

            var deadlineSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Name == "Registration_End");

            if (deadlineSetting == null || !DateTime.TryParse(deadlineSetting.Value, out DateTime deadline))
            {
                Debug.WriteLine("===> KHONG TIM THAY DEADLINE TRONG SETTINGS");
                return;
            }

            var registeredStudentIds = await _context.Registrations
                .Select(r => r.StudentId)
                .Distinct()
                .ToListAsync();

            var allStudents = await _userManager.GetUsersInRoleAsync("Student");

            var targetStudents = allStudents.Where(s => !registeredStudentIds.Contains(s.Id)).ToList();

            Console.WriteLine($"===> Tìm thấy {targetStudents.Count} sinh viên chưa đăng ký.");

            foreach (var student in targetStudents)
            {
                Console.WriteLine($"===> Đang thực hiện gửi mail tới: {student.Email}");

                string subject = "🔔 [Nhắc nhở] Hoàn thành đăng ký đề tài Khóa luận";
                string content = $@"
            <h3>Thông báo từ Hệ thống Quản lý Khóa luận</h3>
            <p>Chào bạn <b>{student.FullName}</b>,</p>
            <p>Hệ thống ghi nhận bạn hiện chưa thực hiện đăng ký đề tài khóa luận.</p>
            <p>Thời hạn cuối cùng để đăng ký là: <b style='color:red;'>{deadline:dd/MM/yyyy}</b>.</p>
            <p>Vui lòng đăng nhập vào website để thực hiện đăng ký sớm nhất có thể.</p>
            <br/>
            <p><i>Đây là email tự động, vui lòng không phản hồi lại email này.</i></p>";

                await _notiService.SendDualNotification(student.Id, subject, content);
            }

            Debug.WriteLine("===> KET THUC JOB");
        }
    }
}