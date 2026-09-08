#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"

#if CONFIG_IDF_TARGET_ESP32C6
#define POT_ADC_CHANNEL    ADC_CHANNEL_4   // สำหรับ ESP32-C6 (GPIO 4)
#else
#define POT_ADC_CHANNEL    ADC_CHANNEL_6   // สำหรับ ESP32 Classic (GPIO 34)
#endif

void app_main(void)
{
    printf("\n[SYSTEM] ESP-IDF v6.x Potentiometer Stream Starting...\n");

    // 1. ตั้งค่า ADC Unit 1
    adc_oneshot_unit_handle_t adc1_handle;
    adc_oneshot_unit_init_cfg_t init_config1 = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config1, &adc1_handle));

    // 2. กำหนดความละเอียด 12-bit (0-4095) และ Attenuation 12dB (0-3.3V)
    adc_oneshot_chan_cfg_t config = {
        .bitwidth = ADC_BITWIDTH_12,
        .atten = ADC_ATTEN_DB_12,
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc1_handle, POT_ADC_CHANNEL, &config));

    int raw_val = 0;
    while (1) {
        // 3. อ่านค่า ADC
        ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &raw_val));

        // 4. สตรีมข้อมูลออกทาง Serial
        printf("%d\n", raw_val);

        vTaskDelay(pdMS_TO_TICKS(100)); // หน่วงเวลา 100ms
    }
}
```[cite: 2]

---

### Step 4: คอมไพล์ (Build) และแฟลชโปรแกรม (Flash & Monitor)

1. เข้าไปยังโฟลเดอร์โปรเจกต์[cite: 2]:
   ```powershell
   cd ESP32_ADC_Stream
   ```[cite: 2]

2. สั่ง **Build** โค้ดผ่าน Docker[cite: 2]:
   ```powershell
   docker run --rm --mount "type=bind,source=$((Get-Location).Path),target=/workspace" -w /workspace espressif/idf:release-v6.1 idf.py build
   ```[cite: 2]

3. สั่ง **Flash และเปิด Monitor** *(เปลี่ยน `COM24` ให้ตรงกับพอร์ตจริงของบอร์ดในเครื่องคุณ)*[cite: 2]:
   ```powershell
   python -m esptool -p COM24 --chip esp32 -b 460800 --before default_reset --after hard_reset write_flash --flash_mode dio --flash_size 2MB --flash_freq 40m 0x1000 build\bootloader\bootloader.bin 0x8000 build\partition_table\partition-table.bin 0x10000 build\esp32_adc_stream.bin && idf -p COM24 monitor
   ```[cite: 2]

---

### Step 5: ทดสอบและออกจากหน้าจอ Monitor

1. ลองหมุน **Potentiometer**:
   * หมุนซ้ายสุด ➔ ค่าเข้าใกล้ `0`[cite: 2]
   * หมุนขวาสุด ➔ ค่าเข้าใกล้ `4095`[cite: 2]
2. **กดปุ่ม `Ctrl + ]`** เพื่อออกจาก Monitor และคืนพอร์ต COM[cite: 2] *(สำคัญมาก หากไม่ปิด จะรันโปรแกรมในใบงานถัดไปไม่ได้)*[cite: 2]

---

### Step 6: ปรับปรุงการกรองสัญญาณ (EMA Filter - เพิ่มเติม)[cite: 2]

แก้ไขลูป `while (1)` ใน `main/main.c` เพื่อลดสัญญาณรบกวน (Noise)[cite: 2]:

```c
    int raw_val = 0;
    float filtered_val = 0.0f;
    const float alpha = 0.25f; // ค่าสัมประสิทธิ์การกรอง

    while (1) {
        ESP_ERROR_CHECK(adc_oneshot_read(adc1_handle, POT_ADC_CHANNEL, &raw_val));

        // สมการกรองแบบ Exponential Moving Average (EMA)
        filtered_val = (alpha * raw_val) + ((1.0f - alpha) * filtered_val);

        printf("%d\n", (int)filtered_val);

        vTaskDelay(pdMS_TO_TICKS(50)); // สตรีม 20 ครั้ง/วินาที
    }
```[cite: 2]

---

### Step 7: ภารกิจท้าทาย (Micro-Challenge)[cite: 2]
1. เปลี่ยน Potentiometer เป็น **LDR (ตัวต้านทานไวแสง)** ร่วมกับ R $10\text{ k}\Omega$[cite: 2]
2. ทดลองใช้ไฟฉายส่องและเอามือปิด สังเกตการเปลี่ยนแปลงตัวเลขบนหน้าจอ[cite: 2]
3. แคปหน้าจอ Monitor และถ่ายรูปการต่อวงจรเก็บไว้ประกอบรายงาน[cite: 2]