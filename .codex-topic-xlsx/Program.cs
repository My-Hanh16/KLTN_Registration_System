using ClosedXML.Excel;

var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KLTN_Registration_System", "wwwroot", "templates", "import-de-tai-theo-giang-vien-khoa-v3.xlsx"));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);

var headers = new[] { "TopicCode", "Title", "Description", "Semester", "LecturerEmail", "MajorCode", "MajorName", "Faculty", "MaxStudents", "Level", "Category", "Deadline" };
var rows = new List<string[]>
{
    new[] { "CNPM-101", "Xây dựng hệ thống quản lý khóa luận tốt nghiệp", "Phân tích, thiết kế và xây dựng hệ thống đăng ký đề tài, duyệt đăng ký, theo dõi tiến độ và nộp báo cáo khóa luận.", "HK2-2025-2026", "gv101@kltn.local", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "2", "Medium", "Ứng dụng", "2026-08-30" },
    new[] { "CNPM-102", "Website thương mại điện tử tích hợp thanh toán", "Xây dựng hệ thống bán hàng trực tuyến bằng ASP.NET Core MVC, quản lý sản phẩm, đơn hàng và thanh toán trực tuyến.", "HK2-2025-2026", "phamminhdung@uni.edu.vn", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "2", "Medium", "Web", "2026-08-30" },
    new[] { "CNPM-103", "Hệ thống quản lý thực tập doanh nghiệp", "Quản lý hồ sơ thực tập, doanh nghiệp tiếp nhận, giảng viên hướng dẫn và đánh giá kết quả thực tập của sinh viên.", "HK2-2025-2026", "phamminhdung@uni.edu.vn", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "2", "Medium", "Ứng dụng", "2026-08-30" },
    new[] { "CNPM-104", "Ứng dụng quản lý lịch học và nhắc việc sinh viên", "Xây dựng ứng dụng web/mobile hỗ trợ sinh viên quản lý thời khóa biểu, deadline môn học và thông báo nhắc việc.", "HK2-2025-2026", "nguyenvanan@uni.edu.vn", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "1", "Easy", "Ứng dụng", "2026-08-30" },
    new[] { "CNPM-105", "Cổng hỗ trợ hỏi đáp học vụ trực tuyến", "Xây dựng hệ thống ticket hỗ trợ sinh viên, phân loại yêu cầu và quản lý phản hồi từ phòng/khoa.", "HK2-2025-2026", "gv102@kltn.local", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "1", "Medium", "Web", "2026-08-30" },
    new[] { "HTTT-101", "Hệ thống quản lý thư viện số", "Xây dựng nền tảng quản lý tài liệu, mượn trả sách, phân quyền người dùng và thống kê lượt truy cập tài liệu số.", "HK2-2025-2026", "nguyenvanan@uni.edu.vn", "HTTT", "Hệ thống thông tin", "Công nghệ thông tin", "2", "Medium", "Hệ thống thông tin", "2026-08-30" },
    new[] { "HTTT-102", "Dashboard phân tích kết quả học tập sinh viên", "Thiết kế dashboard trực quan hóa điểm học phần, cảnh báo rủi ro học vụ và hỗ trợ cố vấn học tập theo dõi sinh viên.", "HK2-2025-2026", "tranthanhhieu@uni.edu.vn", "HTTT", "Hệ thống thông tin", "Công nghệ thông tin", "2", "Medium", "Phân tích dữ liệu", "2026-08-30" },
    new[] { "HTTT-103", "Kho dữ liệu mini cho báo cáo đào tạo", "Thiết kế mô hình dữ liệu, ETL và báo cáo phân tích số liệu đào tạo theo khoa, lớp và học kỳ.", "HK2-2025-2026", "tranthanhhieu@uni.edu.vn", "HTTT", "Hệ thống thông tin", "Công nghệ thông tin", "3", "Hard", "Data Warehouse", "2026-08-30" },
    new[] { "KHMT-101", "Chatbot tư vấn chọn đề tài khóa luận", "Ứng dụng xử lý ngôn ngữ tự nhiên để gợi ý đề tài phù hợp với năng lực, chuyên ngành và định hướng nghề nghiệp của sinh viên.", "HK2-2025-2026", "gv101@kltn.local", "KHMT", "Khoa học máy tính", "Công nghệ thông tin", "1", "Hard", "AI", "2026-08-30" },
    new[] { "KHMT-102", "Ứng dụng nhận diện khuôn mặt điểm danh lớp học", "Xây dựng mô hình nhận diện khuôn mặt và tích hợp vào hệ thống điểm danh lớp học có lưu lịch sử.", "HK2-2025-2026", "phantuanhai@uni.edu.vn", "KHMT", "Khoa học máy tính", "Công nghệ thông tin", "2", "Hard", "AI", "2026-08-30" },
    new[] { "KHMT-103", "Phân loại văn bản tiếng Việt bằng học máy", "Thu thập dữ liệu, tiền xử lý tiếng Việt và xây dựng mô hình phân loại chủ đề văn bản.", "HK2-2025-2026", "phantuanhai@uni.edu.vn", "KHMT", "Khoa học máy tính", "Công nghệ thông tin", "1", "Hard", "Machine Learning", "2026-08-30" },
    new[] { "KHMT-104", "Hệ thống phát hiện bình luận độc hại", "Ứng dụng NLP để phát hiện nội dung tiêu cực trên diễn đàn sinh viên và cung cấp báo cáo thống kê.", "HK2-2025-2026", "hoangvanminh@uni.edu.vn", "KHMT", "Khoa học máy tính", "Công nghệ thông tin", "2", "Hard", "NLP", "2026-08-30" },
    new[] { "CNPM-106", "Ứng dụng quản lý sự kiện khoa CNTT", "Xây dựng hệ thống đăng ký, check-in QR, quản lý người tham gia và thống kê sự kiện của khoa.", "HK2-2025-2026", "hoangvanminh@uni.edu.vn", "CNPM", "Công nghệ phần mềm", "Công nghệ thông tin", "2", "Medium", "Ứng dụng", "2026-08-30" },
    new[] { "KTDT-101", "Giám sát nhiệt độ phòng lab bằng IoT", "Thiết kế thiết bị đo nhiệt độ, độ ẩm và dashboard giám sát theo thời gian thực cho phòng thí nghiệm.", "HK2-2025-2026", "gv103@kltn.local", "KTDT", "Kỹ thuật điện tử", "Điện tử viễn thông", "2", "Medium", "IoT", "2026-08-30" },
    new[] { "KTDT-102", "Hệ thống cảnh báo cháy dùng cảm biến", "Xây dựng mô hình cảnh báo khói, nhiệt độ và gửi thông báo qua web/mobile khi phát hiện bất thường.", "HK2-2025-2026", "gv103@kltn.local", "KTDT", "Kỹ thuật điện tử", "Điện tử viễn thông", "2", "Medium", "IoT", "2026-08-30" },
    new[] { "KTDT-103", "Điều khiển thiết bị điện qua Internet", "Thiết kế hệ thống điều khiển relay qua giao diện web và ghi nhận lịch sử thao tác thiết bị.", "HK2-2025-2026", "gv103@kltn.local", "KTDT", "Kỹ thuật điện tử", "Điện tử viễn thông", "1", "Easy", "IoT", "2026-08-30" },
    new[] { "KTDT-104", "Mô hình nhà thông minh tiết kiệm năng lượng", "Điều khiển đèn, quạt và cảm biến chuyển động nhằm tối ưu tiêu thụ điện trong mô hình nhà thông minh.", "HK2-2025-2026", "gv103@kltn.local", "KTDT", "Kỹ thuật điện tử", "Điện tử viễn thông", "3", "Hard", "IoT", "2026-08-30" },
    new[] { "QTKD-101", "Phân tích hành vi mua hàng trên sàn thương mại điện tử", "Khảo sát, phân tích dữ liệu mua hàng và đề xuất giải pháp tăng tỷ lệ chuyển đổi cho doanh nghiệp.", "HK2-2025-2026", "gv104@kltn.local", "QTKD", "Quản trị kinh doanh", "Kinh tế", "1", "Medium", "Nghiên cứu", "2026-08-30" },
    new[] { "QTKD-102", "Xây dựng kế hoạch marketing số cho doanh nghiệp nhỏ", "Thiết kế chiến lược nội dung, quảng cáo, ngân sách và chỉ số đo lường hiệu quả chiến dịch marketing số.", "HK2-2025-2026", "gv104@kltn.local", "QTKD", "Quản trị kinh doanh", "Kinh tế", "1", "Easy", "Marketing", "2026-08-30" },
    new[] { "QTKD-103", "Đánh giá sự hài lòng của sinh viên với dịch vụ đào tạo", "Xây dựng bảng hỏi, thu thập dữ liệu và phân tích các yếu tố ảnh hưởng đến mức độ hài lòng của sinh viên.", "HK2-2025-2026", "gv104@kltn.local", "QTKD", "Quản trị kinh doanh", "Kinh tế", "2", "Medium", "Khảo sát", "2026-08-30" },
    new[] { "QTKD-104", "Ứng dụng CRM trong chăm sóc khách hàng", "Đề xuất quy trình CRM và bộ chỉ số đo lường chất lượng chăm sóc khách hàng trong doanh nghiệp dịch vụ.", "HK2-2025-2026", "gv104@kltn.local", "QTKD", "Quản trị kinh doanh", "Kinh tế", "2", "Medium", "Quản trị", "2026-08-30" }
};
var lecturers = new List<string[]>
{
    new[] { "FullName", "UserCode", "LecturerEmail", "Faculty", "Degree", "Position" },
    new[] { "TS. Bui Thanh Long", "GV104", "gv104@kltn.local", "Kinh tế", "", "Giảng viên" },
    new[] { "Phạm Minh Dũng", "GV004", "phamminhdung@uni.edu.vn", "Công nghệ thông tin", "Tiến sĩ", "Giảng viên" },
    new[] { "TS. Nguyen Minh Duc", "GV101", "gv101@kltn.local", "Công nghệ thông tin", "", "Giảng viên" },
    new[] { "TS. Hoang Van Son", "GV103", "gv103@kltn.local", "Điện tử viễn thông", "", "Giảng viên" },
    new[] { "Nguyễn Văn An", "GV001", "nguyenvanan@uni.edu.vn", "Công nghệ thông tin", "Tiến sĩ", "Giảng viên" },
    new[] { "Trần Thanh Hiếu", "GV006", "tranthanhhieu@uni.edu.vn", "Công nghệ thông tin", "Thạc sĩ", "Giảng viên" },
    new[] { "Trần Thị Bích", "GV002", "tranthibich@uni.edu.vn", "Ngoại ngữ", "PGS.Tiến sĩ", "Giảng viên" },
    new[] { "Phan Tuấn Anh", "GV007", "phantuanhai@uni.edu.vn", "Công nghệ thông tin", "Thạc sĩ", "Trưởng khoa" },
    new[] { "Lê Văn Công", "GV003", "levancong@uni.edu.vn", "Báo chí", "PGS.Tiến sĩ", "Giảng viên" },
    new[] { "TS. Le Quoc Bao", "GV102", "gv102@kltn.local", "Công nghệ thông tin", "", "Giảng viên" },
    new[] { "Hoàng Văn Minh", "GV005", "hoangvanminh@uni.edu.vn", "Công nghệ thông tin", "Tiến sĩ", "Phó khoa" }
};

using var wb = new XLWorkbook();
var ws = wb.Worksheets.Add("Topics");
for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
for (var r = 0; r < rows.Count; r++)
{
    for (var c = 0; c < headers.Length; c++) ws.Cell(r + 2, c + 1).Value = rows[r][c];
}
ws.Row(1).Style.Font.Bold = true;
ws.Columns().AdjustToContents();
ws.SheetView.FreezeRows(1);

var refWs = wb.Worksheets.Add("GiangVienThamChieu");
for (var r = 0; r < lecturers.Count; r++)
{
    for (var c = 0; c < lecturers[r].Length; c++) refWs.Cell(r + 1, c + 1).Value = lecturers[r][c];
}
refWs.Row(1).Style.Font.Bold = true;
refWs.Columns().AdjustToContents();
refWs.SheetView.FreezeRows(1);

if (File.Exists(output)) File.Delete(output);
wb.SaveAs(output);
Console.WriteLine(output);
