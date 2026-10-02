using System.IO.Ports;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ลงทะเบียน State Store และ Background Service สำหรับอ่าน Serial
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddHostedService<SerialPortWorker>();

var app = builder.Build();

// เสิร์ฟไฟล์ static (index.html) จาก wwwroot อัตโนมัติ
app.UseFileServer();

// API Endpoint สำหรับให้ Web Frontend ดึงข้อมูลไปแสดงผล
app.MapGet("/api/telemetry", (TelemetryStore store) => Results.Ok(store.GetLatest()));

app.Run();

// โมเดลข้อมูล
public class TelemetryData
{
    [JsonPropertyName("rawValue")]
    public int RawValue { get; set; }

    [JsonPropertyName("voltage")]
    public double Voltage { get; set; }

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [JsonPropertyName("dataSource")]
    public string DataSource { get; set; } = "ESP32 (Simulated Stream)";
}

// คลาสเก็บสถานะข้อมูลล่าสุด
public class TelemetryStore
{
    private TelemetryData _data = new();
    private readonly object _lock = new();

    public TelemetryData GetLatest()
    {
        lock (_lock) { return _data; }
    }

    public void Update(int raw)
    {
        lock (_lock)
        {
            _data = new TelemetryData
            {
                RawValue = raw,
                Voltage = Math.Round(raw * 3.3 / 4095.0, 2),
                Percentage = Math.Round((raw / 4095.0) * 100.0, 1),
                DataSource = "ESP32 USB Serial (Simulated)"
            };
        }
    }
}

// Background Worker ทำหน้าที่อ่าน Serial Port
public class SerialPortWorker : BackgroundService
{
    private readonly TelemetryStore _store;
    private readonly ILogger<SerialPortWorker> _logger;

    public SerialPortWorker(TelemetryStore store, ILogger<SerialPortWorker> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ⚠️ เปลี่ยน COM Port ให้ตรงกับพอร์ตของ ESP32 (เช่น "COM3", "COM4")
        string portName = "COM3"; 

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var serialPort = new SerialPort(portName, 115200);

                // 💡 [แก้จุดที่ 1]: เปิดสัญญาณ DTR/RTS เพื่อให้ชิป USB-Serial บน ESP32 ส่งข้อมูลออกมาได้
                serialPort.DtrEnable = true;
                serialPort.RtsEnable = true;

                // 💡 [แก้จุดที่ 2]: กำหนด ReadTimeout กันไม่ให้โปรแกรมค้างเวลาอ่านข้อมูล
                serialPort.ReadTimeout = 1000;

                serialPort.Open();
                _logger.LogInformation($"เชื่อมต่อ Serial Port {portName} สำเร็จ");

                while (!stoppingToken.IsCancellationRequested && serialPort.IsOpen)
                {
                    try
                    {
                        // 💡 [แก้จุดที่ 3]: อ่านข้อมูลเป็นบรรทัดตรงๆ
                        string line = serialPort.ReadLine().Trim();

                        if (int.TryParse(line, out int rawVal))
                        {
                            _store.Update(rawVal);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // หากช่วงเวลานั้นไม่มีข้อมูลส่งมา ให้ข้ามรอบไป ไม่ให้โปรแกรมแครช
                    }

                    await Task.Yield(); // คืนระบบให้ Process อื่นทำงานได้อย่างราบรื่น
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"กำลังรอการเชื่อมต่อ Serial ({portName}): {ex.Message}");
                await Task.Delay(2000, stoppingToken); // ลองใหม่ทุก 2 วินาที
            }
        }
    }
}