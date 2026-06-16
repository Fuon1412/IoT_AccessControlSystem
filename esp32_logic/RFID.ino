/*
  Rui Santos & Sara Santos - Random Nerd Tutorials
  (Bản vá bảo mật: Tích hợp OLED 1.3" I2C, Hardcode UID và Local Buzzer Alert)
*/

#include <MFRC522v2.h>
#include <MFRC522DriverSPI.h>
#include <MFRC522DriverPinSimple.h>
#include <MFRC522Debug.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>

// --- CẤU HÌNH OLED ---
#define i2c_Address 0x3c 
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET -1
Adafruit_SH1106G display = Adafruit_SH1106G(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

// --- CẤU HÌNH MFRC522 ---
MFRC522DriverPinSimple ss_pin(5);
MFRC522DriverSPI driver{ss_pin}; 
MFRC522 mfrc522{driver};         

// =========================================================
// [SECURITY LOGIC]: THÔNG TIN XÁC THỰC VÀ PHẦN CỨNG
// =========================================================
const String AUTHORIZED_UID = "339c8f34"; 

// Thay đổi sang GPIO 32 (hoặc các chân output khác như 25, 26, 27). 
// KHÔNG dùng GPIO 34, 35, 36, 39.
#define BUZZER_PIN 32 

void setup() {
  Serial.begin(115200);  
  while (!Serial);       
  
  // Khởi tạo Buzzer
  pinMode(BUZZER_PIN, OUTPUT);
  digitalWrite(BUZZER_PIN, LOW); // Đảm bảo còi tắt khi khởi động

  // Khởi tạo giao tiếp I2C và OLED
  delay(250); 
  if(!display.begin(i2c_Address, true)) {
    Serial.println(F("[!] SH1106 allocation failed. Check I2C wiring."));
    for(;;);
  }
  
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SH110X_WHITE);
  display.setCursor(0, 10);
  display.println("System Booting...");
  display.display();

  // Khởi tạo MFRC522
  mfrc522.PCD_Init();    
  MFRC522Debug::PCD_DumpVersionToSerial(mfrc522, Serial); 
  
  display.clearDisplay();
  display.setCursor(0, 10);
  display.println("Ready to Scan");
  display.display();
  Serial.println(F("[*] System initialized. Waiting for badge..."));
}

// Hàm kích hoạt còi báo động (Alarm Trigger)
void triggerAlert() {
  // Phát ra âm thanh bíp liên tục 3 lần để cảnh báo Unauthorized Access
  for(int i = 0; i < 3; i++) {
    digitalWrite(BUZZER_PIN, HIGH);
    delay(150);
    digitalWrite(BUZZER_PIN, LOW);
    delay(100);
  }
}

void loop() {
  if (!mfrc522.PICC_IsNewCardPresent()) {
    return;
  }
  if (!mfrc522.PICC_ReadCardSerial()) {
    return;
  }

  String uidString = "";
  for (byte i = 0; i < mfrc522.uid.size; i++) {
    if (mfrc522.uid.uidByte[i] < 0x10) {
      uidString += "0"; 
    }
    uidString += String(mfrc522.uid.uidByte[i], HEX);
  }
  
  Serial.print("Target UID Detected: ");
  Serial.println(uidString);

  display.clearDisplay();
  display.setCursor(0, 0);
  display.setTextSize(1);
  display.println("Scanned UID:");
  
  display.setCursor(0, 15);
  display.setTextSize(2);
  display.println(uidString);
  display.setCursor(0, 40);

  // Authorization Check
  if (uidString == AUTHORIZED_UID) {
    Serial.println("[+] ACCESS GRANTED");
    display.println("GRANTED");
    display.display();
    
    // Tùy chọn: Bạn có thể thêm 1 tiếng bíp ngắn để xác nhận thẻ đúng
    // digitalWrite(BUZZER_PIN, HIGH); delay(100); digitalWrite(BUZZER_PIN, LOW);
    
    delay(2000); // Mở cửa/Hiển thị trạng thái trong 2 giây
  } else {
    // Nếu sai UID, ghi log và kích hoạt cảnh báo
    Serial.println("[-] SECURITY ALERT: ACCESS DENIED");
    display.println("DENIED!");
    display.display();
    
    // Kích hoạt còi báo động Local Alert
    triggerAlert(); 
    
    delay(1500); // Đợi phần thời gian còn lại trước khi reset
  }

  // Reset màn hình về trạng thái chờ
  display.clearDisplay();
  display.setTextSize(1);
  display.setCursor(0, 10);
  display.println("Ready to Scan");
  display.display();
}