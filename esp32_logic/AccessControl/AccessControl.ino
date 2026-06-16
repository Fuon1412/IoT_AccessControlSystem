/*
  IoT Access Control — ESP32 firmware (Arduino IDE / .ino format)
  -----------------------------------------------------------------
  Hardware (xác nhận từ RFID.ino thực tế):
    - RFID  : MFRC522v2 (driver-based API), SS = GPIO 5
    - OLED  : SH1106 0.96" 128x64 I2C @ 0x3C (Adafruit_SH110X)
    - Buzzer: active buzzer, GPIO 32, digitalWrite HIGH/LOW

  Flow:
    quét thẻ -> publish MQTT scan -> backend xác thực
      -> response {"access":true,"name":"..."}
      -> GRANTED: OLED hiện tên người dùng
      -> DENIED : buzzer kêu cảnh báo
    Offline / timeout -> buffer vào bộ nhớ, flush khi reconnect.

  Server layer (WiFi + MQTT + SPIFFS queue) giữ nguyên logic từ bản .cpp.

  Thư viện cần cài trong Arduino IDE (Library Manager):
    - MFRC522v2            (by GithubCommunity)
    - Adafruit SH110X      (by Adafruit)
    - Adafruit GFX Library (by Adafruit)
    - PubSubClient         (by Nick O'Leary)
    - ArduinoJson          (by Benoit Blanchon, v7+)
  Board: "ESP32 Dev Module"
*/

#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <SPIFFS.h>
#include <Wire.h>

#include <MFRC522v2.h>
#include <MFRC522DriverSPI.h>
#include <MFRC522DriverPinSimple.h>
#include <MFRC522Debug.h>

#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>

// ── Config ─────────────────────────────────────────────────────────────────────
// WiFi
#define WIFI_SSID        "Hust_B1_Staff"
#define WIFI_PASS        ""

// MQTT broker — public broker for college project (no auth).
// Local dev alt: IP máy chạy docker-compose, port 1883.
#define MQTT_HOST        "broker.hivemq.com"
#define MQTT_PORT        1883
#define MQTT_USER        ""          // public broker = anonymous
#define MQTT_PASS        ""
#define DEVICE_ID        "esp32-door-01"

// Unique project prefix — public broker is shared, prefix avoids topic
// collision with other students. Backend MUST use the SAME prefix.
#define TOPIC_PREFIX     "iot7f3a"

// MQTT topics — prefix/access/{device}/{verb}
#define TOPIC_SCAN       TOPIC_PREFIX "/access/" DEVICE_ID "/scan"
#define TOPIC_RESPONSE   TOPIC_PREFIX "/access/" DEVICE_ID "/response"
#define TOPIC_STATUS     TOPIC_PREFIX "/access/" DEVICE_ID "/status"
#define TOPIC_COMMAND    TOPIC_PREFIX "/devices/" DEVICE_ID "/command"  // backend → ESP32 (lock/unlock)

// Pins (theo RFID.ino thực tế)
#define PIN_RC522_SS     5      // MFRC522 SS/SDA — dùng SPI mặc định (SCK18/MISO19/MOSI23)
#define PIN_BUZZER       32     // active buzzer — digitalWrite HIGH = kêu
#define OLED_ADDR        0x3C
#define OLED_WIDTH       128
#define OLED_HEIGHT      64
#define OLED_RESET       -1

// Timing
#define HEARTBEAT_INTERVAL_MS  60000   // 60s giữa các status publish
#define MQTT_RECONNECT_MS       5000   // delay retry khi mất kết nối
#define RESPONSE_TIMEOUT_MS     3000   // chờ response từ backend
#define RESULT_DISPLAY_MS       3000   // thời gian giữ kết quả trên OLED

// SPIFFS retry queue
#define QUEUE_FILE       "/queue.txt"
#define QUEUE_MAX        50            // số event buffer tối đa, vượt thì drop oldest

// ── Hardware ─────────────────────────────────────────────────────────────────────
MFRC522DriverPinSimple ss_pin(PIN_RC522_SS);
MFRC522DriverSPI       driver{ss_pin};
MFRC522                rfid{driver};

Adafruit_SH1106G oled = Adafruit_SH1106G(OLED_WIDTH, OLED_HEIGHT, &Wire, OLED_RESET);

// ── MQTT ─────────────────────────────────────────────────────────────────────────
WiFiClient   wifiClient;
PubSubClient mqtt(wifiClient);

// ── State ──────────────────────────────────────────────────────────────────────
unsigned long lastHeartbeat = 0;
volatile bool responseReceived = false;
volatile bool accessGranted = false;
char responseName[64] = "";

// OLED idle-revert: sau khi hiện grant/deny, quay về màn chờ.
unsigned long resultShownAt = 0;
bool showingResult = false;

// ── Forward declarations ──────────────────────────────────────────────────────
void connectWifi();
void connectMqtt();
void onMqttMessage(char* topic, byte* payload, unsigned int length);
void handleCommand(const String& cmd);
String readRfidUid();
void handleScan(const String& uid);
void actuate(bool granted, const String& name);
void oledShow(const String& line1, const String& line2 = "");
void oledIdle();
void buzzerGrant();
void buzzerDeny();
void flushSpiffsQueue();
void enqueueSpiffs(const String& uid);
int countQueueLines();

// ── Setup ──────────────────────────────────────────────────────────────────────
void setup() {
  Serial.begin(115200);

  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_BUZZER, LOW);   // còi tắt khi khởi động

  // OLED (SH1106) — I2C mặc định ESP32: SDA=GPIO21, SCL=GPIO22
  Wire.begin();
  delay(250);
  if (!oled.begin(OLED_ADDR, true)) {
    Serial.println(F("[OLED] SH1106 init failed — check I2C wiring/addr"));
  } else {
    oledShow("Booting...");
  }

  // RFID (MFRC522v2) — SPI mặc định
  rfid.PCD_Init();
  MFRC522Debug::PCD_DumpVersionToSerial(rfid, Serial);

  if (!SPIFFS.begin(true)) {
    Serial.println("[SPIFFS] Mount failed");
  }

  connectWifi();

  mqtt.setServer(MQTT_HOST, MQTT_PORT);
  mqtt.setCallback(onMqttMessage);
  mqtt.setBufferSize(512);

  connectMqtt();
  flushSpiffsQueue();

  oledIdle();
  Serial.println("[BOOT] Ready");
}

// ── Loop ─────────────────────────────────────────────────────────────────────────
void loop() {
  // CRITICAL: gọi mỗi vòng lặp — keepalive + nhận message
  if (!mqtt.connected()) {
    connectMqtt();
  }
  mqtt.loop();

  // Heartbeat
  if (millis() - lastHeartbeat >= HEARTBEAT_INTERVAL_MS) {
    mqtt.publish(TOPIC_STATUS, "online");
    lastHeartbeat = millis();
  }

  // RFID scan
  if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial()) {
    String uid = readRfidUid();
    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
    handleScan(uid);
  }

  // Revert OLED về idle sau khi hết thời gian hiện kết quả
  if (showingResult && millis() - resultShownAt >= RESULT_DISPLAY_MS) {
    oledIdle();
  }
}

// ── WiFi ───────────────────────────────────────────────────────────────────────
void connectWifi() {
  Serial.printf("[WiFi] Connecting to %s\n", WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("\n[WiFi] Connected: %s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("\n[WiFi] Failed — running offline");
  }
}

// ── MQTT connect + subscribe ──────────────────────────────────────────────────
void connectMqtt() {
  if (mqtt.connected()) return;

  Serial.printf("[MQTT] Connecting to %s:%d\n", MQTT_HOST, MQTT_PORT);

  bool connected = (strlen(MQTT_USER) > 0)
    ? mqtt.connect(DEVICE_ID, MQTT_USER, MQTT_PASS)
    : mqtt.connect(DEVICE_ID);

  if (connected) {
    Serial.println("[MQTT] Connected");
    mqtt.subscribe(TOPIC_RESPONSE);
    mqtt.subscribe(TOPIC_COMMAND);   // emergency lock/unlock from backend
    flushSpiffsQueue();   // flush event đã queue khi reconnect
  } else {
    Serial.printf("[MQTT] Failed (rc=%d). Retry in %dms\n",
      mqtt.state(), MQTT_RECONNECT_MS);
    delay(MQTT_RECONNECT_MS);
  }
}

// ── MQTT message handler ──────────────────────────────────────────────────────
// /response : {"access":true,"name":"Nguyen Van A"}
// /command  : {"command":"lock"|"unlock", ...}
void onMqttMessage(char* topic, byte* payload, unsigned int length) {
  String t(topic);

  JsonDocument doc;
  if (deserializeJson(doc, payload, length) != DeserializationError::Ok) {
    Serial.println("[MQTT] Bad JSON");
    return;
  }

  if (t.endsWith("/command")) {
    const char* cmd = doc["command"] | "";
    handleCommand(String(cmd));
    return;
  }

  if (t.endsWith("/response")) {
    accessGranted = doc["access"].as<bool>();
    strlcpy(responseName, doc["name"] | "", sizeof(responseName));
    responseReceived = true;
  }
}

// ── Emergency command handler ───────────────────────────────────────────────────
// Simulate door actuator: alarm buzzer 5s, then OLED shows the forced state.
void handleCommand(const String& cmd) {
  bool lock = (cmd == "lock");
  bool unlock = (cmd == "unlock");
  if (!lock && !unlock) {
    Serial.printf("[CMD] Unknown command: %s\n", cmd.c_str());
    return;
  }

  Serial.printf("[CMD] EMERGENCY %s\n", cmd.c_str());
  oledShow("EMERGENCY", lock ? "Locking..." : "Opening...");

  // Alarm buzzer for 5s (non-blocking-ish: keep mqtt alive between beeps)
  unsigned long start = millis();
  while (millis() - start < 5000) {
    digitalWrite(PIN_BUZZER, HIGH);
    delay(200);
    digitalWrite(PIN_BUZZER, LOW);
    delay(200);
    mqtt.loop();
  }

  // Show forced state, latch it (no idle revert)
  oledShow(lock ? "FORCE LOCKED" : "FORCE OPENED", lock ? "Door secured" : "Door open");
  showingResult = false;   // latch — don't auto-revert
  resultShownAt = 0;
}

// ── RFID UID read ─────────────────────────────────────────────────────────────
// Trả hex thường, không separator — khớp RfidCard.Uid backend.
String readRfidUid() {
  String uid = "";
  for (byte i = 0; i < rfid.uid.size; i++) {
    if (rfid.uid.uidByte[i] < 0x10) uid += "0";
    uid += String(rfid.uid.uidByte[i], HEX);
  }
  return uid;
}

// ── Scan handler ──────────────────────────────────────────────────────────────
void handleScan(const String& uid) {
  Serial.printf("[SCAN] UID: %s\n", uid.c_str());
  oledShow("Scanning...", uid);

  // Offline: queue local, báo còi
  if (!mqtt.connected()) {
    Serial.println("[SCAN] Offline — queuing to SPIFFS");
    enqueueSpiffs(uid);
    oledShow("Offline", "Queued");
    buzzerDeny();
    resultShownAt = millis();
    showingResult = true;
    return;
  }

  // Publish scan
  JsonDocument doc;
  doc["device"] = DEVICE_ID;
  doc["uid"] = uid;
  char buf[128];
  serializeJson(doc, buf);
  mqtt.publish(TOPIC_SCAN, buf);

  // Chờ response từ backend
  responseReceived = false;
  unsigned long start = millis();
  while (!responseReceived && millis() - start < RESPONSE_TIMEOUT_MS) {
    mqtt.loop();
    delay(10);
  }

  if (!responseReceived) {
    Serial.println("[SCAN] Response timeout — queuing");
    enqueueSpiffs(uid);
    oledShow("Timeout", "Queued");
    buzzerDeny();
    resultShownAt = millis();
    showingResult = true;
    return;
  }

  actuate(accessGranted, String(responseName));
}

// ── Actuate: feedback theo kết quả ────────────────────────────────────────────
void actuate(bool granted, const String& name) {
  if (granted) {
    Serial.printf("[ACCESS] GRANTED — %s\n", name.c_str());
    // Hiện thông tin người dùng trên OLED (yêu cầu flow)
    oledShow("GRANTED", name.length() > 0 ? name : "Welcome");
    buzzerGrant();
    // TODO: relay mở cửa nếu có — digitalWrite(PIN_RELAY, HIGH); delay; LOW;
  } else {
    Serial.println("[ACCESS] DENIED");
    oledShow("DENIED!", "Card not valid");
    buzzerDeny();
  }
  resultShownAt = millis();
  showingResult = true;
}

// ── OLED ───────────────────────────────────────────────────────────────────────
void oledShow(const String& line1, const String& line2) {
  oled.clearDisplay();
  oled.setTextColor(SH110X_WHITE);
  oled.setTextSize(2);
  oled.setCursor(0, 0);
  oled.println(line1);
  if (line2.length() > 0) {
    oled.setTextSize(1);
    oled.setCursor(0, 32);
    oled.println(line2);
  }
  oled.display();
}

// Màn chờ: nhắc quét thẻ. Clear cờ result.
void oledIdle() {
  showingResult = false;
  resultShownAt = 0;
  oledShow("Scan card", DEVICE_ID);
}

// ── Buzzer (active buzzer — digitalWrite) ─────────────────────────────────────
// Grant: 1 bíp ngắn xác nhận. Deny: 3 bíp cảnh báo (theo RFID.ino).
void buzzerGrant() {
  digitalWrite(PIN_BUZZER, HIGH);
  delay(120);
  digitalWrite(PIN_BUZZER, LOW);
}

void buzzerDeny() {
  for (int i = 0; i < 3; i++) {
    digitalWrite(PIN_BUZZER, HIGH);
    delay(150);
    digitalWrite(PIN_BUZZER, LOW);
    delay(100);
  }
}

// ── SPIFFS retry queue ────────────────────────────────────────────────────────
// Format: 1 JSON / dòng — {"device":"...","uid":"..."}
// Flush khi MQTT reconnect. Cap QUEUE_MAX dòng.
void enqueueSpiffs(const String& uid) {
  if (countQueueLines() >= QUEUE_MAX) {
    // Drop oldest: đọc hết, bỏ dòng đầu, ghi lại
    File f = SPIFFS.open(QUEUE_FILE, "r");
    String contents = "";
    int skipped = 0;
    while (f.available()) {
      String line = f.readStringUntil('\n');
      if (skipped++ == 0) continue;   // bỏ dòng đầu (oldest)
      if (line.length() > 0) contents += line + "\n";
    }
    f.close();
    File w = SPIFFS.open(QUEUE_FILE, "w");
    w.print(contents);
    w.close();
    Serial.println("[QUEUE] Max reached — oldest dropped");
  }

  JsonDocument doc;
  doc["device"] = DEVICE_ID;
  doc["uid"] = uid;
  char line[128];
  serializeJson(doc, line);

  File f = SPIFFS.open(QUEUE_FILE, "a");
  if (f) {
    f.println(line);
    f.close();
    Serial.printf("[QUEUE] Enqueued: %s\n", line);
  }
}

void flushSpiffsQueue() {
  if (!mqtt.connected()) return;
  if (!SPIFFS.exists(QUEUE_FILE)) return;

  File f = SPIFFS.open(QUEUE_FILE, "r");
  if (!f) return;

  int flushed = 0;
  while (f.available()) {
    String line = f.readStringUntil('\n');
    line.trim();
    if (line.length() == 0) continue;

    if (mqtt.publish(TOPIC_SCAN, line.c_str())) {
      flushed++;
    } else {
      Serial.println("[QUEUE] Flush publish failed — stopping");
      break;
    }
    delay(50);
  }
  f.close();

  if (flushed > 0) {
    SPIFFS.remove(QUEUE_FILE);
    Serial.printf("[QUEUE] Flushed %d queued events\n", flushed);
  }
}

int countQueueLines() {
  if (!SPIFFS.exists(QUEUE_FILE)) return 0;
  File f = SPIFFS.open(QUEUE_FILE, "r");
  int count = 0;
  while (f.available()) {
    f.readStringUntil('\n');
    count++;
  }
  f.close();
  return count;
}
