var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 1. หน้าแรก (Home)
app.MapGet("/", () => "Welcome to IoT Edge Gateway by คุณากร มะซอ!");

// 2. เช็คสถานะระบบ
app.MapGet("/api/status", () => new {
    gateway = "ESP32-EdgeGateway",
    status = "Online",
    uptimeSeconds = Environment.TickCount64 / 1000,
    isHealthy = true
});

// 3. ควบคุมอุปกรณ์ LED (เพิ่ม Console.WriteLine ตามกิจกรรมที่ 4)
app.MapGet("/api/led/{state}", (string state) => {
    string action = state.ToLower() == "on" ? "TURN ON 💡" : "TURN OFF 🌑";
    
    // บันทึก Log ลง Terminal ตามคำสั่งกิจกรรมที่ 4
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] LED Control: {state}");

    return Results.Ok(new { 
        device = "LED_D2", 
        requestedState = state, 
        actionResult = action,
        serverTime = DateTime.Now.ToString("HH:mm:ss")
    });
});

// 4. ภารกิจท้าทาย (Micro-Challenge): Endpoint ข้อมูลนักศึกษา
app.MapGet("/api/student", () => new {
    studentId = "67030037",                                    // ใส่รหัสนักศึกษาจริง
    studentName = "คุณากร มะซอ",                               // ชื่อ-นามสกุลภาษาอังกฤษ
    faculty = "เทคโนโลยี คอมพิวเตอร์",   // คณะและสาขาวิชา
    targetSensor = "DHT22",                                       // ชื่อเซนเซอร์ที่สนใจ
    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")      // เวลาปัจจุบันของเซิร์ฟเวอร์
});

app.Run();