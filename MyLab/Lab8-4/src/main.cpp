#include <Arduino.h>

int simulatedAdc = 0;
int stepAmount = 10; // 💡 ปรับลดเหลือ 10 (ยิ่งน้อย ยิ่งเคลื่อนที่ช้าและนุ่มนวล)

void setup() {
    Serial.begin(115200);
}

void loop() {
    simulatedAdc += stepAmount;

    if (simulatedAdc >= 4095) {
        simulatedAdc = 4095;
        stepAmount = -stepAmount; 
    } 
    else if (simulatedAdc <= 0) {
        simulatedAdc = 0;
        stepAmount = -stepAmount; 
    }

    Serial.println(simulatedAdc);

    delay(50); // 💡 ส่งข้อมูลทุกๆ 50ms
}