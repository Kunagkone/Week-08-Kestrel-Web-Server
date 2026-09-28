#include <Arduino.h>
#include <math.h>

float angle = 0.0;

void setup() {
    // เริ่มต้น Serial Communication ที่ Baud rate 115200
    Serial.begin(115200);
}

void loop() {
    // จำลองค่า ADC (12-bit: 0 - 4095) ด้วยฟังก์ชัน Sine Wave
    // ค่าจะค่อยๆ กวาดขึ้นจาก 0 ไปถึง 4095 แล้วกวาดลงอย่างนุ่มนวล
    int simulatedAdc = (int)((sin(angle) + 1.0) * 2047.5);

    // ส่งค่าไปยัง คอมพิวเตอร์ผ่าน USB Serial
    Serial.println(simulatedAdc);

    // เพิ่มมุมเพื่อเปลี่ยนค่าคลื่น
    angle += 0.05;
    if (angle >= 3.14159 * 2) {
        angle = 0.0;
    }

    delay(50); // ส่งข้อมูลทุกๆ 50ms
}