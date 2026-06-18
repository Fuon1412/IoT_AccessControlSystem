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

  MQTT transport:
    Broker an toàn = wss:// (WebSocket Secure, TLS, có auth). PubSubClient
    KHÔNG hỗ trợ WebSocket nên dùng client native của ESP-IDF (esp_mqtt_client):
    nó tự lo TLS (cert bundle), ALPN, WebSocket, auth user/pass và auto-reconnect
    trong task riêng. Không cần gọi loop() thủ công như PubSubClient.

  Device ID:
    KHÔNG hardcode nữa — sinh runtime từ MAC chip (esp32-door-aabbcc).
    Mỗi board tự định danh, không cần sửa code/reflash riêng. Topic build runtime.

  Thư viện cần cài trong Arduino IDE (Library Manager):
    - MFRC522v2            (by GithubCommunity)
    - Adafruit SH110X      (by Adafruit)
    - Adafruit GFX Library (by Adafruit)
    - ArduinoJson          (by Benoit Blanchon, v7+)
    - ESP32Servo           (by Kevin Harrington) — servo mô phỏng cửa
  (esp_mqtt_client + esp_crt_bundle có sẵn trong ESP32 core — không cần cài thêm)
  Board: "ESP32 Dev Module"
*/

#include <WiFi.h>
#include <ArduinoJson.h>
#include <SPIFFS.h>
#include <Wire.h>

#include "mqtt_client.h"
#include "esp_crt_bundle.h"

#include <MFRC522v2.h>
#include <MFRC522DriverSPI.h>
#include <MFRC522DriverPinSimple.h>
#include <MFRC522Debug.h>

#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>

#include <ESP32Servo.h>

// ── Config ─────────────────────────────────────────────────────────────────────
// WiFi
#define WIFI_SSID        "Hiep T2"
#define WIFI_PASS        "99999999"

// MQTT broker — HiveMQ Cloud, WebSocket Secure (TLS + auth). Client native ESP-IDF.
// TLS WebSocket URL từ HiveMQ console: {cluster}:8884/mqtt
#define MQTT_WS_URI      "wss://1b12128e05b64f6b8ec16de96fe975c9.s1.eu.hivemq.cloud:8884/mqtt"
#define MQTT_USER        "esp32-acs"
#define MQTT_PASS        "Phuong2004@o"

// Tiền tố tên thiết bị — phần sau lấy từ MAC chip lúc runtime.
// (Bỏ TOPIC_PREFIX: cluster HiveMQ riêng + auth, không cần namespace tránh đụng.)
#define DEVICE_PREFIX    "esp32-door-"

// Pins (theo RFID.ino thực tế)
#define PIN_RC522_SS     5      // MFRC522 SS/SDA — dùng SPI mặc định (SCK18/MISO19/MOSI23)
#define PIN_BUZZER       32     // active buzzer — digitalWrite HIGH = kêu
#define PIN_SERVO        15     // servo mô phỏng cửa — chân tín hiệu (PWM)
#define OLED_ADDR        0x3C
#define OLED_WIDTH       128
#define OLED_HEIGHT      64
#define OLED_RESET       -1

// Servo (mô phỏng cửa) — góc đóng/mở
#define SERVO_CLOSED_DEG 0      // cửa đóng
#define SERVO_OPEN_DEG   90     // cửa mở

// Timing
#define HEARTBEAT_INTERVAL_MS  60000   // 60s giữa các status publish
#define RESPONSE_TIMEOUT_MS     3000   // chờ response từ backend
#define RESULT_DISPLAY_MS       3000   // thời gian giữ kết quả trên OLED

// SPIFFS retry queue
#define QUEUE_FILE       "/queue.txt"
#define QUEUE_MAX        50            // số event buffer tối đa, vượt thì drop oldest

// Emergency command — giữ trạng thái cưỡng bức rồi tự reset về ban đầu
#define EMERGENCY_HOLD_MS  10000       // 10s active, sau đó cửa đóng + OLED idle

// ── Hardware ─────────────────────────────────────────────────────────────────────
MFRC522DriverPinSimple ss_pin(PIN_RC522_SS);
MFRC522DriverSPI       driver{ss_pin};
MFRC522                rfid{driver};

Adafruit_SH1106G oled = Adafruit_SH1106G(OLED_WIDTH, OLED_HEIGHT, &Wire, OLED_RESET);

// Servo mô phỏng cửa
Servo doorServo;
bool doorOpen = false;   // trạng thái cửa hiện tại

// ── MQTT (esp_mqtt_client native) ──────────────────────────────────────────────
esp_mqtt_client_handle_t g_mqttClient = nullptr;
volatile bool g_mqttConnected = false;
volatile bool g_flushPending  = false;   // set khi vừa connect → flush queue trong loop()

// ── Identity + topics (build runtime, không hardcode) ───────────────────────────
String g_deviceId;        // esp32-door-<mac6>
String g_topicScan;       // access/{device}/scan
String g_topicResponse;   // access/{device}/response
String g_topicStatus;     // access/{device}/status
String g_topicCommand;    // devices/{device}/command

// ── State ──────────────────────────────────────────────────────────────────────
unsigned long lastHeartbeat = 0;
volatile bool responseReceived = false;
volatile bool accessGranted = false;
char responseName[64] = "";

// OLED idle-revert: sau khi hiện grant/deny, quay về màn chờ.
unsigned long resultShownAt = 0;
bool showingResult = false;

// Emergency latch timer — active 10s rồi reset về trạng thái ban đầu.
bool emergencyActive = false;
unsigned long emergencyStartAt = 0;

// ── Forward declarations ──────────────────────────────────────────────────────
void connectWifi();
void buildIdentity();
void initMqtt();
void mqttEventHandler(void* args, esp_event_base_t base, int32_t eventId, void* eventData);
void handleCommand(const String& cmd);
String readRfidUid();
void handleScan(const String& uid);
void actuate(bool granted, const String& name);
void oledShow(const String& line1, const String& line2 = "");
void oledIdle();
void buzzerGrant();
void buzzerDeny();
void doorOpenSim();
void doorCloseSim();
void publishDoorState(const char* state);
void flushSpiffsQueue();
void enqueueSpiffs(const String& uid);
int countQueueLines();

// ── Setup ──────────────────────────────────────────────────────────────────────
void setup() {
  Serial.begin(115200);

  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_BUZZER, LOW);   // còi tắt khi khởi động

  // Servo mô phỏng cửa — khởi động ở trạng thái đóng
  ESP32PWM::allocateTimer(0);
  doorServo.setPeriodHertz(50);             // servo chuẩn 50Hz
  doorServo.attach(PIN_SERVO, 500, 2400);   // pulse min/max (us)
  doorCloseSim();

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
  buildIdentity();   // cần MAC → gọi sau khi WiFi init
  initMqtt();        // esp_mqtt_client tự connect + reconnect trong task riêng

  oledIdle();
  Serial.println("[BOOT] Ready");
}

// ── Loop ─────────────────────────────────────────────────────────────────────────
void loop() {
  // esp_mqtt_client chạy task riêng — không cần gọi loop() như PubSubClient.

  // Flush SPIFFS queue ngay sau khi (re)connect
  if (g_flushPending && g_mqttConnected) {
    g_flushPending = false;
    flushSpiffsQueue();
  }

  // Heartbeat
  if (g_mqttConnected && millis() - lastHeartbeat >= HEARTBEAT_INTERVAL_MS) {
    esp_mqtt_client_publish(g_mqttClient, g_topicStatus.c_str(), "online", 0, 0, 0);
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

  // Emergency lock/open: active 10s rồi tự reset về ban đầu (cửa đóng + idle)
  if (emergencyActive && millis() - emergencyStartAt >= EMERGENCY_HOLD_MS) {
    emergencyActive = false;
    Serial.println("[CMD] Emergency expired — reset to idle");
    oledIdle();   // oledIdle() đóng cửa nếu đang mở
  }
}

// ── WiFi ───────────────────────────────────────────────────────────────────────
void connectWifi() {
  Serial.printf("[WiFi] Connecting to %s\n", WIFI_SSID);
  WiFi.mode(WIFI_STA);
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

// ── Identity + topics ───────────────────────────────────────────────────────────
// DEVICE_ID = DEVICE_PREFIX + 3 byte cuối MAC (hex). Topic build từ device id.
void buildIdentity() {
  uint8_t mac[6];
  WiFi.macAddress(mac);
  char sfx[7];
  snprintf(sfx, sizeof(sfx), "%02x%02x%02x", mac[3], mac[4], mac[5]);

  g_deviceId      = String(DEVICE_PREFIX) + sfx;
  g_topicScan     = String("access/")  + g_deviceId + "/scan";
  g_topicResponse = String("access/")  + g_deviceId + "/response";
  g_topicStatus   = String("access/")  + g_deviceId + "/status";
  g_topicCommand  = String("devices/") + g_deviceId + "/command";

  Serial.printf("[ID] device=%s\n", g_deviceId.c_str());
}

// ── MQTT init (esp_mqtt_client) ─────────────────────────────────────────────────
void initMqtt() {
  const char* alpn[] = { "http/1.1", NULL };   // wss handshake ALPN

  esp_mqtt_client_config_t cfg = {};
#if ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
  cfg.broker.address.uri                    = MQTT_WS_URI;
  cfg.broker.verification.alpn_protos       = alpn;
  cfg.broker.verification.crt_bundle_attach = esp_crt_bundle_attach;
  cfg.credentials.username                  = MQTT_USER;
  cfg.credentials.authentication.password   = MQTT_PASS;
  cfg.credentials.client_id                 = g_deviceId.c_str();
#else
  cfg.uri               = MQTT_WS_URI;
  cfg.alpn_protos       = alpn;
  cfg.crt_bundle_attach = esp_crt_bundle_attach;
  cfg.username          = MQTT_USER;
  cfg.password          = MQTT_PASS;
  cfg.client_id         = g_deviceId.c_str();
#endif

  g_mqttClient = esp_mqtt_client_init(&cfg);
  esp_mqtt_client_register_event(g_mqttClient, MQTT_EVENT_ANY, mqttEventHandler, NULL);
  esp_mqtt_client_start(g_mqttClient);
  Serial.println("[MQTT] client started (wss)");
}

// ── MQTT event handler ──────────────────────────────────────────────────────────
// /response : {"access":true,"name":"Nguyen Van A"}
// /command  : {"command":"lock"|"unlock", ...}
void mqttEventHandler(void* args, esp_event_base_t base, int32_t eventId, void* eventData) {
  esp_mqtt_event_handle_t event = (esp_mqtt_event_handle_t)eventData;

  switch (eventId) {
    case MQTT_EVENT_CONNECTED:
      g_mqttConnected = true;
      esp_mqtt_client_subscribe(g_mqttClient, g_topicResponse.c_str(), 0);
      esp_mqtt_client_subscribe(g_mqttClient, g_topicCommand.c_str(), 0);   // emergency lock/unlock
      g_flushPending = true;   // flush queue trong loop() (tránh việc nặng trong task MQTT)
      Serial.println("[MQTT] Connected & subscribed");
      break;

    case MQTT_EVENT_DISCONNECTED:
      g_mqttConnected = false;
      Serial.println("[MQTT] Disconnected");
      break;

    case MQTT_EVENT_DATA: {
      // Dữ liệu không null-terminated → copy an toàn
      char topicBuf[event->topic_len + 1];
      memcpy(topicBuf, event->topic, event->topic_len);
      topicBuf[event->topic_len] = '\0';
      String t(topicBuf);

      char payloadBuf[event->data_len + 1];
      memcpy(payloadBuf, event->data, event->data_len);
      payloadBuf[event->data_len] = '\0';

      JsonDocument doc;
      if (deserializeJson(doc, payloadBuf) != DeserializationError::Ok) {
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
      break;
    }

    default: break;
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

  // Servo mô phỏng cửa theo lệnh — latch (giữ trạng thái, không auto-close)
  if (lock) doorCloseSim();
  else      doorOpenSim();

  // Alarm buzzer for 5s (esp_mqtt_client chạy task riêng nên delay ở đây không mất kết nối)
  unsigned long start = millis();
  while (millis() - start < 5000) {
    digitalWrite(PIN_BUZZER, HIGH);
    delay(200);
    digitalWrite(PIN_BUZZER, LOW);
    delay(200);
  }

  // Show forced state — giữ active EMERGENCY_HOLD_MS rồi tự reset (loop xử lý)
  oledShow(lock ? "FORCE LOCKED" : "FORCE OPENED", lock ? "Door secured" : "Door open");
  showingResult = false;       // không dùng idle-revert thường
  resultShownAt = 0;
  emergencyActive = true;
  emergencyStartAt = millis(); // 10s tính từ lúc nhận lệnh (gồm 5s còi)
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
  if (!g_mqttConnected) {
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
  doc["device"] = g_deviceId;
  doc["uid"] = uid;
  char buf[128];
  serializeJson(doc, buf);
  esp_mqtt_client_publish(g_mqttClient, g_topicScan.c_str(), buf, 0, 0, 0);

  // Chờ response từ backend (esp_mqtt_client set cờ trong task riêng)
  responseReceived = false;
  unsigned long start = millis();
  while (!responseReceived && millis() - start < RESPONSE_TIMEOUT_MS) {
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
    doorOpenSim();   // servo mở cửa — tự đóng khi OLED revert idle (RESULT_DISPLAY_MS)
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
  if (doorOpen) doorCloseSim();   // cửa tự đóng khi quay về màn chờ
  oledShow("Scan card", g_deviceId);
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

// ── Servo (mô phỏng cửa) ──────────────────────────────────────────────────────
// Mở: quay tới SERVO_OPEN_DEG. Đóng: về SERVO_CLOSED_DEG.
void doorOpenSim() {
  doorServo.write(SERVO_OPEN_DEG);
  doorOpen = true;
  Serial.println("[DOOR] OPEN (servo)");
  publishDoorState("open");
}

void doorCloseSim() {
  doorServo.write(SERVO_CLOSED_DEG);
  doorOpen = false;
  Serial.println("[DOOR] CLOSED (servo)");
  publishDoorState("closed");
}

// Publish trạng thái cửa lên status topic → backend/dashboard log được.
// Guard: chỉ publish khi state thực sự đổi (giảm spam broker/DB).
// lastDoorPublished chỉ cập nhật sau khi publish thành công → reconnect vẫn báo lại.
char lastDoorPublished[8] = "";   // "", "open", "closed"

void publishDoorState(const char* state) {
  if (strcmp(lastDoorPublished, state) == 0) return;   // không đổi — bỏ qua
  if (!g_mqttConnected) return;                         // chưa kết nối — giữ nguyên để báo lại sau

  JsonDocument doc;
  doc["device"] = g_deviceId;
  doc["door"] = state;
  char buf[96];
  serializeJson(doc, buf);
  if (esp_mqtt_client_publish(g_mqttClient, g_topicStatus.c_str(), buf, 0, 0, 0) >= 0) {
    strlcpy(lastDoorPublished, state, sizeof(lastDoorPublished));
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
  doc["device"] = g_deviceId;
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
  if (!g_mqttConnected) return;
  if (!SPIFFS.exists(QUEUE_FILE)) return;

  File f = SPIFFS.open(QUEUE_FILE, "r");
  if (!f) return;

  int flushed = 0;
  while (f.available()) {
    String line = f.readStringUntil('\n');
    line.trim();
    if (line.length() == 0) continue;

    if (esp_mqtt_client_publish(g_mqttClient, g_topicScan.c_str(), line.c_str(), 0, 0, 0) >= 0) {
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
