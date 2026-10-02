using System.IO.Ports;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// ลงทะเบียน State Store และ Background Service อ่าน Serial Port
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddHostedService<SerialPortWorker>();

var app = builder.Build();

// เปิดใช้งาน Static File Server (เสิร์ฟ index.html ใน wwwroot อัตโนมัติ)[cite: 1]
app.UseFileServer();

// API Endpoint สำหรับ Web Dashboard ดึงข้อมูลไปแสดงผลแบบ Polling[cite: 1]
app.MapGet("/api/telemetry", (TelemetryStore store) => Results.Ok(store.GetLatest()));

app.Run();

// ====================================================
// Data Model: โครงสร้างข้อมูล Telemetry
// ====================================================
public class TelemetryData
{
    [JsonPropertyName("rawValue")]
    public int RawValue { get; set; }

    [JsonPropertyName("voltage")]
    public double Voltage { get; set; }

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [JsonPropertyName("dataSource")]
    public string DataSource { get; set; } = "ESP32 (Connecting...)";
}

// ====================================================
// TelemetryStore: จัดเก็บและเข้าถึงข้อมูลแบบ Thread-Safe
// ====================================================
public class TelemetryStore
{
    private TelemetryData _data = new();
    private readonly object _lock = new();

    public TelemetryData GetLatest()
    {
        lock (_lock)
        {
            return _data;
        }
    }

    public void Update(int raw, string source = "ESP32 USB Serial")
    {
        // บังคับจำกัดค่าให้อยู่ในช่วง ADC 12-Bit (0 - 4095)
        int clampedRaw = Math.Clamp(raw, 0, 4095);

        lock (_lock)
        {
            _data = new TelemetryData
            {
                RawValue = clampedRaw,
                Voltage = Math.Round(clampedRaw * 3.3 / 4095.0, 2),
                Percentage = Math.Round((clampedRaw / 4095.0) * 100.0, 1),
                DataSource = source
            };
        }
    }
}

// ====================================================
// SerialPortWorker: Background Service อ่านข้อมูล Serial Real-time
// ====================================================
public class SerialPortWorker : BackgroundService
{
    private readonly TelemetryStore _store;
    private readonly ILogger<SerialPortWorker> _logger;

    // ⚠️ เปลี่ยน COM Port ตรงนี้ให้ตรงกับ ESP32 ในเครื่องของคุณ (เช่น "COM3", "COM4", "COM5")
    private const string TargetPortName = "COM3";
    private const int BaudRate = 115200;

    public SerialPortWorker(TelemetryStore store, ILogger<SerialPortWorker> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ตรวจสอบและแสดงรายการ COM Port ที่พบในเครื่อง
        string[] availablePorts = SerialPort.GetPortNames();
        _logger.LogInformation($"[Port Check] COM Ports ที่พบในเครื่องขณะนี้: [{string.Join(", ", availablePorts)}]");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var serialPort = new SerialPort(TargetPortName, BaudRate)
                {
                    DtrEnable = true,   // เปิดสัญญาณ DTR ให้ชิป USB-Serial บน ESP32 ส่งข้อมูลออกมา
                    RtsEnable = true,   // เปิดสัญญาณ RTS
                    ReadTimeout = 500,  // ป้องกันคำสั่งค้างเมื่อไม่มีข้อมูลส่งมา
                    NewLine = "\n"      // สัญลักษณ์จบบรรทัด
                };

                serialPort.Open();
                _logger.LogInformation($"✅ เชื่อมต่อ Serial Port {TargetPortName} สำเร็จ (Baud Rate: {BaudRate})");

                while (!stoppingToken.IsCancellationRequested && serialPort.IsOpen)
                {
                    try
                    {
                        // 💡 [แก้ปัญหาเข็มข้ามเลขเร็วเกินไป]:
                        // หากมีข้อมูลสะสมค้างใน Memory Buffer มากเกินไป ให้ล้าง Buffer เก่าทิ้ง
                        // เพื่อให้ C# ดึงเฉพาะค่าล่าสุดแบบ Real-time มาแสดงผลเท่านั้น
                        if (serialPort.BytesToRead > 60)
                        {
                            serialPort.DiscardInBuffer();
                        }

                        string rawLine = serialPort.ReadLine().Trim();

                        if (!string.IsNullOrWhiteSpace(rawLine))
                        {
                            // ใช้ Regex สกัดเฉพาะตัวเลข ป้องกันตัวอักษรขยะรบกวน
                            var match = Regex.Match(rawLine, @"\d+");
                            if (match.Success && int.TryParse(match.Value, out int rawVal))
                            {
                                _store.Update(rawVal, $"ESP32 USB Serial ({TargetPortName})");
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        // ปล่อยผ่านเมื่ออ่านข้อมูลไม่ทันในรอบนั้น
                    }

                    // หน่วงเวลา 100ms ให้สัมพันธ์กับการจังหวะ Polling ของฝั่งหน้าเว็บ
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ ไม่สามารถเชื่อมต่อพอร์ต {TargetPortName} ได้ ({ex.Message}) ลองใหม่ใน 2 วินาที...");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}