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

        // Hangfire sẽ gọi hàm này theo lịch
        public async Task CheckAndSendReminders()
        {
            Debug.WriteLine("===> BAT DAU QUET SINH VIEN");

            var now = DateTime.Now;

            // 1. Lấy ngày kết thúc từ bảng Settings
            var deadlineSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Name == "Registration_End");

            if (deadlineSetting == null || !DateTime.TryParse(deadlineSetting.Value, out DateTime deadline))
            {
                Debug.WriteLine("===> KHONG TIM THAY DEADLINE TRONG SETTINGS");
                return;
            }

            // 2. Lấy ID của tất cả sinh viên đã đăng ký đề tài (Distinct để tránh trùng)
            var registeredStudentIds = await _context.Registrations
                .Select(r => r.StudentId)
                .Distinct()
                .ToListAsync();

            // 3. Lấy danh sách tài khoản thuộc vai trò "Student"
            var allStudents = await _userManager.GetUsersInRoleAsync("Student");

            // 4. Lọc ra những sinh viên CHƯA đăng ký
            var targetStudents = allStudents.Where(s => !registeredStudentIds.Contains(s.Id)).ToList();

            Console.WriteLine($"===> Tìm thấy {targetStudents.Count} sinh viên chưa đăng ký.");

            // 5. Gửi thông báo cho danh sách mục tiêu
            foreach (var student in targetStudents)
            {
                Console.WriteLine($"===> Đang thực hiện gửi mail tới: {student.Email}");

                // Nội dung mail chuyên nghiệp hơn
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