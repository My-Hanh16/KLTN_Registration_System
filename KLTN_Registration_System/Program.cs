// ============================================================
// FILE: Program.cs  —  HOÀN CHỈNH
// Thêm: Seed Admin, Settings, Timelines, HomeController route
// ============================================================
using KLTN_Registration_System.Models;
using KLTN_Registration_System.Models.Entities;
using KLTN_Registration_System.Services; // 👈 Thêm cái này
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Authorization;
using Hangfire;
using Hangfire.Dashboard;
using System.Text.Json.Serialization;
using KLTN_Registration_System.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ================= DATABASE =================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ================= IDENTITY =================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Cấu hình password tối thiểu cho tài khoản hệ thống.
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    // Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
// ================= HANGFIRE (BỔ SUNG) =================
// 👈 Cấu hình Hangfire sử dụng SQL Server làm nơi lưu trữ Job
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();
builder.Services.AddSignalR(); // Chạy bộ máy xử lý ngầm

// ================= CUSTOM SERVICES (BỔ SUNG) =================
// 👈 Đăng ký các dịch vụ bạn vừa tạo
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReminderWorker>();
builder.Services.AddMemoryCache();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ================= SESSION =================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ================= MVC =================

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters
        .Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// ================= MIDDLEWARE =================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/timeline")
        || context.Request.Path.StartsWithSegments("/uploads/chat"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthorizationFilter() }
});

// ================= ROUTE =================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<ChatHub>("/hubs/chat");

// ================= SET HANGFIRE JOBS (BỔ SUNG) =================
// 👈 Thiết lập nhắc nhở tự động chạy lúc 8 giờ sáng mỗi ngày
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

    // Khai báo múi giờ Việt Nam
    var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    recurringJobManager.AddOrUpdate<ReminderWorker>(
        "Auto-Remind-Students",
        worker => worker.CheckAndSendReminders(),
        "0 8 * * *", // 8 giờ sáng
        new RecurringJobOptions
        {
            TimeZone = vietnamTimeZone // Chốt cứng múi giờ Việt Nam
        }
    );
}
// ================= SEED =================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<AppDbContext>();

    // ── 1. Seed Roles ──────────────────────────────────────────
    string[] roles = { "Admin", "Student", "Lecturer" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ── 2. Seed Admin mặc định ─────────────────────────────────
    // Cấu hình qua biến môi trường hoặc user-secrets:
    // SeedAdmin__Email và SeedAdmin__Password
    var adminEmail = builder.Configuration["SeedAdmin:Email"];
    var adminPassword = builder.Configuration["SeedAdmin:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail)
        && !string.IsNullOrWhiteSpace(adminPassword)
        && await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Quản trị viên",
            UserCode = "ADMIN001",
            EmailConfirmed = true,
            Faculty = "Ban quản lý"
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // ── 3. Seed Majors ─────────────────────────────────────────
    if (!context.Majors.Any())
    {
        context.Majors.AddRange(
            new Major { Name = "Công nghệ phần mềm", MajorCode = "CNPM", FacultyName = "Công nghệ thông tin", IsActive = true },
            new Major { Name = "Hệ thống thông tin", MajorCode = "HTTT", FacultyName = "Công nghệ thông tin", IsActive = true },
            new Major { Name = "Khoa học máy tính", MajorCode = "KHMT", FacultyName = "Công nghệ thông tin", IsActive = true },
            new Major { Name = "Kỹ thuật điện tử", MajorCode = "KTDT", FacultyName = "Điện tử viễn thông", IsActive = true },
            new Major { Name = "Quản trị kinh doanh", MajorCode = "QTKD", FacultyName = "Kinh tế", IsActive = true }
        );
        context.SaveChanges();
    }

    // Tự vá DB cũ chưa chạy migration AddUserMajorAssignments.
    context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[UserMajors]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserMajors](
        [UserId] nvarchar(450) NOT NULL,
        [MajorId] int NOT NULL,
        CONSTRAINT [PK_UserMajors] PRIMARY KEY ([UserId], [MajorId]),
        CONSTRAINT [FK_UserMajors_AspNetUsers_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserMajors_Majors_MajorId] FOREIGN KEY ([MajorId])
            REFERENCES [dbo].[Majors]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_UserMajors_MajorId] ON [dbo].[UserMajors]([MajorId]);

    INSERT INTO [dbo].[UserMajors] ([UserId], [MajorId])
    SELECT [Id], [MajorId]
    FROM [dbo].[AspNetUsers]
    WHERE [MajorId] IS NOT NULL;
END

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260520090000_AddUserMajorAssignments'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520090000_AddUserMajorAssignments', N'8.0.0');
END
");

    // ── 4. Seed Settings (thời gian đăng ký) ──────────────────
    var defaultSettings = new Dictionary<string, string>
    {
        ["IsPortalOpen"] = "true",
        ["Registration_Start"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
        ["Registration_End"] = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-ddTHH:mm:ss"),
        ["Semester_Start"] = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
        ["Semester_End"] = DateTime.UtcNow.Date.AddMonths(4).ToString("yyyy-MM-dd"),
        ["Max_Student_Per_Topic"] = "3",
        ["Min_GPA"] = "0"
    };

    foreach (var setting in defaultSettings)
    {
        if (!context.Settings.Any(s => s.Name == setting.Key))
        {
            context.Settings.Add(new Setting
            {
                Name = setting.Key,
                Value = setting.Value
            });
        }
    }

    context.SaveChanges();

    // ── 5. Seed Timelines mẫu ─────────────────────────────────
    if (!context.Timelines.Any())
    {
        context.Timelines.AddRange(
            new Timeline
            {
                Title = "Hạn đăng ký đề tài",
                Description = "Sinh viên hoàn tất đăng ký đề tài trực tuyến trước ngày này",
                Date = DateTime.UtcNow.AddDays(30),
                IsActive = true
            },
            new Timeline
            {
                Title = "Nộp đề cương chi tiết",
                Description = "Nộp bản in có chữ ký GVHD cho Văn phòng Khoa",
                Date = DateTime.UtcNow.AddDays(60),
                IsActive = true
            },
            new Timeline
            {
                Title = "Báo cáo tiến độ lần 1",
                Description = "Hoàn thành 30% khối lượng công việc",
                Date = DateTime.UtcNow.AddDays(90),
                IsActive = true
            }
        );
        context.SaveChanges();
    }

    // ── 6. Seed Topics mẫu (không cần GV) ─────────────────────
    if (!context.Topics.Any())
    {
        var majorCnpm = context.Majors.FirstOrDefault(m => m.MajorCode == "CNPM");

        context.Topics.AddRange(
            new Topic
            {
                Title = "AI nhận diện khuôn mặt",
                Description = "Sử dụng Python, OpenCV và Deep Learning để nhận diện khuôn mặt theo thời gian thực",
                Level = TopicLevel.Medium,
                Status = TopicStatus.Available,
                MaxStudents = 2,
                Deadline = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                IsApproved = true,
                IsRegistrationOpen = true,
                LecturerId = null,
                DepartmentName = "Bộ môn Trí tuệ nhân tạo",
                MajorId = majorCnpm?.Id,
                TopicCode = "TOPIC-AI-001",
                Semester = "HK2 - 2025-2026",
                Category = "Nghiên cứu"
            },
            new Topic
            {
                Title = "Web bán hàng ASP.NET MVC",
                Description = "Hệ thống thương mại điện tử với đầy đủ chức năng quản lý đơn hàng, thanh toán",
                Level = TopicLevel.Easy,
                Status = TopicStatus.Available,
                MaxStudents = 2,
                Deadline = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                IsApproved = true,
                IsRegistrationOpen = true,
                LecturerId = null,
                DepartmentName = "Bộ môn Công nghệ phần mềm",
                MajorId = majorCnpm?.Id,
                TopicCode = "TOPIC-WEB-001",
                Semester = "HK2 - 2025-2026",
                Category = "Ứng dụng"
            },
            new Topic
            {
                Title = "Ứng dụng mobile quản lý chi tiêu",
                Description = "Xây dựng app Flutter theo dõi thu chi cá nhân, phân tích xu hướng chi tiêu",
                Level = TopicLevel.Medium,
                Status = TopicStatus.Available,
                MaxStudents = 1,
                Deadline = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                IsApproved = true,
                IsRegistrationOpen = true,
                LecturerId = null,
                DepartmentName = "Bộ môn Công nghệ phần mềm",
                MajorId = majorCnpm?.Id,
                TopicCode = "TOPIC-MOB-001",
                Semester = "HK2 - 2025-2026",
                Category = "Ứng dụng"
            }
        );
        context.SaveChanges();
    }
}

app.Run();

public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
