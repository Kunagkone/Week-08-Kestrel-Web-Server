using System.IO.Ports;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// ลงทะเบียน State Store และ Background Service สำหรับอ่าน Serial
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddHostedService<SerialPortWorker>();

var app = builder.Build();

// เปิดใช้งาน Static File Server (เสิร์ฟ index.html ใน wwwroot อัตโนมัติ)[cite: 1]
app.UseFileServer();

// API Endpoint สำหรับ Web Frontend ดึงข้อมูลไปแสดงผลแบบ Polling[cite: 1]
app.MapGet("/api/telemetry", (TelemetryStore store) => Results.Ok(store.GetLatest()));

app.Run();

// ----------------------------------------------------
// โมเดลข้อมูล Telemetry[cite: 1]
// ----------------------------------------------------
public class TelemetryData
{
    [JsonPropertyName("rawValue")]
    public int RawValue { get; set; }

    [JsonPropertyName("voltage")]
    public double Voltage { get; set; }

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [JsonPropertyName("dataSource")]
    public string DataSource { get; set; } = "ESP32 Waiting for Data...";
}

// ----------------------------------------------------
// คลาสเก็บและเข้าถึงสถานะข้อมูลล่าสุดแบบ Thread-Safe
// ----------------------------------------------------
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
        // บังคับจำกัดค่าให้อยู่ในสเกล ADC 12-Bit (0 - 4095)
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

// ----------------------------------------------------
// Background Worker สำหรับอ่านข้อมูลจาก Serial Port
// ----------------------------------------------------
public class SerialPortWorker : BackgroundService
{
    private readonly TelemetryStore _store;
    private readonly ILogger<SerialPortWorker> _logger;

    // ⚠️ ระบุ COM Port ของ ESP32 (ปรับเปลี่ยนเลข COM ตรงนี้ถ้าบอร์ดของคุณอยู่อย่างเช่น COM4, COM5)
    private const string TargetPortName = "COM3";
    private const int BaudRate = 115200;

    public SerialPortWorker(TelemetryStore store, ILogger<SerialPortWorker> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 🔍 แสดงรายชื่อ COM Port ทั้งหมดที่มีอยู่ในคอมพิวเตอร์ขณะนั้นลง Terminal
        string[] availablePorts = SerialPort.GetPortNames();
        _logger.LogInformation($"[Serial Check] COM Port ที่พบในเครื่องขณะนี้: [{string.Join(", ", availablePorts)}]");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var serialPort = new SerialPort(TargetPortName, BaudRate)
                {
                    DtrEnable = true,   // จำเป็นมากสำหรับชิป ESP32 เพื่อเริ่มส่งข้อมูล
                    RtsEnable = true,   // สัญญาณควบคุมการส่งข้อมูล
                    ReadTimeout = 1000, // กันคำสั่ง ReadLine ค้างค้างคืน
                    NewLine = "\n"      // กำหนดสัญลักษณ์จบบรรทัด
                };

                serialPort.Open();
                _logger.LogInformation($"✅ เชื่อมต่อ Serial Port {TargetPortName} (Baud Rate: {BaudRate}) สำเร็จ!");

                while (!stoppingToken.IsCancellationRequested && serialPort.IsOpen)
                {
                    try
                    {
                        string rawLine = serialPort.ReadLine().Trim();

                        if (string.IsNullOrWhiteSpace(rawLine))
                        {
                            continue;
                        }

                        // 💡 พิมพ์ข้อมูลที่อ่านได้สดๆ ออกทาง Terminal เพื่อตรวจเช็ค
                        _logger.LogInformation($"[Serial Receive]: {rawLine}");

                        // ใช้ Regex สกัดเฉพาะกลุ่มตัวเลขออกมา (กรองขยะหรือข้อความตัวอักษรออกให้อัตโนมัติ)
                        var match = Regex.Match(rawLine, @"\d+");
                        if (match.Success && int.TryParse(match.Value, out int rawVal))
                        {
                            _store.Update(rawVal, $"ESP32 USB Serial ({TargetPortName})");
                        }
                        else
                        {
                            _logger.LogWarning($"[Parse Warning] อ่านข้อความได้ แต่แปลงเป็นตัวเลขไม่ได้: '{rawLine}'");
                        }
                    }
                    catch (TimeoutException)
                    {
                        // กรณีไม่มีข้อมูลส่งมาจาก ESP32 ภายใน 1 วินาที ให้ข้ามไปรอบถัดไป
                    }

                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ ไม่สามารถเปิดพอร์ต {TargetPortName} ได้ ({ex.Message}) กำลังลองใหม่ใน 2 วินาที...");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}