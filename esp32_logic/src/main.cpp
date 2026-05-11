#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <MFRC522.h>
#include <Adafruit_SSD1306.h>
#include <SPIFFS.h>
#include <SPI.h>
#include <Wire.h>
#include "config.h"

// ── Hardware ─────────────────────────────────────────────────────────────────
MFRC522 rfid(PIN_RC522_SDA, PIN_RC522_RST);
Adafruit_SSD1306 oled(128, 64, &Wire, -1);

// ── MQTT ─────────────────────────────────────────────────────────────────────
WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);

// ── State ─────────────────────────────────────────────────────────────────────
unsigned long lastHeartbeat = 0;
volatile bool responseReceived = false;
volatile bool accessGranted = false;
char responseName[64] = "";

// ── Forward declarations ──────────────────────────────────────────────────────
void connectWifi();
void connectMqtt();
void onMqttMessage(char* topic, byte* payload, unsigned int length);
String readRfidUid();
void handleScan(const String& uid);
void actuate(bool granted, const String& name);
void oledShow(const String& line1, const String& line2 = "");
void buzzer(bool success);
void flushSpiffsQueue();
void enqueueSpiffs(const String& uid);
int countQueueLines();

// ── Setup ─────────────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);
    SPI.begin(PIN_RC522_SCK, PIN_RC522_MISO, PIN_RC522_MOSI, PIN_RC522_SDA);
    rfid.PCD_Init();

    Wire.begin(PIN_OLED_SDA, PIN_OLED_SCL);
    if (oled.begin(SSD1306_SWITCHCAPVCC, 0x3C)) {
        oledShow("Booting...");
    }

    if (!SPIFFS.begin(true)) {
        Serial.println("[SPIFFS] Mount failed");
    }

    pinMode(PIN_BUZZER, OUTPUT);

    connectWifi();

    mqtt.setServer(MQTT_HOST, MQTT_PORT);
    mqtt.setCallback(onMqttMessage);
    mqtt.setBufferSize(512);

    connectMqtt();
    flushSpiffsQueue();

    oledShow("Ready", DEVICE_ID);
    Serial.println("[BOOT] Ready");
}

// ── Loop ──────────────────────────────────────────────────────────────────────
void loop() {
    // CRITICAL: must call every loop — handles keepalive + incoming messages
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
}

// ── WiFi ──────────────────────────────────────────────────────────────────────
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
        flushSpiffsQueue();  // flush any queued events on reconnect
    } else {
        Serial.printf("[MQTT] Failed (rc=%d). Retry in %dms\n",
            mqtt.state(), MQTT_RECONNECT_MS);
        delay(MQTT_RECONNECT_MS);
    }
}

// ── MQTT message handler ──────────────────────────────────────────────────────
// Payload: {"access":true,"name":"Nguyen Van A"}
void onMqttMessage(char* topic, byte* payload, unsigned int length) {
    String t(topic);
    if (!t.endsWith("/response")) return;

    JsonDocument doc;
    if (deserializeJson(doc, payload, length) != DeserializationError::Ok) {
        Serial.println("[MQTT] Bad response JSON");
        return;
    }

    accessGranted = doc["access"].as<bool>();
    strlcpy(responseName, doc["name"] | "", sizeof(responseName));
    responseReceived = true;
}

// ── RFID UID read ─────────────────────────────────────────────────────────────
String readRfidUid() {
    String uid = "";
    for (byte i = 0; i < rfid.uid.size; i++) {
        if (i > 0) uid += "";
        if (rfid.uid.uidByte[i] < 0x10) uid += "0";
        uid += String(rfid.uid.uidByte[i], HEX);
    }
    uid.toUpperCase();
    return uid;
}

// ── Scan handler ──────────────────────────────────────────────────────────────
void handleScan(const String& uid) {
    Serial.printf("[SCAN] UID: %s\n", uid.c_str());
    oledShow("Scanning...", uid);

    if (!mqtt.connected()) {
        Serial.println("[SCAN] Offline — queuing to SPIFFS");
        enqueueSpiffs(uid);
        oledShow("Offline", "Queued");
        buzzer(false);
        return;
    }

    // Publish scan
    JsonDocument doc;
    doc["device"] = DEVICE_ID;
    doc["uid"] = uid;
    char buf[128];
    serializeJson(doc, buf);
    mqtt.publish(TOPIC_SCAN, buf);

    // Wait for response
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
        buzzer(false);
        return;
    }

    actuate(accessGranted, String(responseName));
}

// ── Actuate door + feedback ───────────────────────────────────────────────────
void actuate(bool granted, const String& name) {
    if (granted) {
        Serial.printf("[ACCESS] GRANTED — %s\n", name.c_str());
        oledShow("GRANTED", name);
        buzzer(true);
        // TODO: digitalWrite(PIN_RELAY, HIGH); delay(3000); digitalWrite(PIN_RELAY, LOW);
    } else {
        Serial.println("[ACCESS] DENIED");
        oledShow("DENIED", "");
        buzzer(false);
    }
}

void oledShow(const String& line1, const String& line2) {
    oled.clearDisplay();
    oled.setTextSize(2);
    oled.setTextColor(SSD1306_WHITE);
    oled.setCursor(0, 0);
    oled.println(line1);
    if (line2.length() > 0) {
        oled.setTextSize(1);
        oled.setCursor(0, 32);
        oled.println(line2);
    }
    oled.display();
}

void buzzer(bool success) {
    if (success) {
        tone(PIN_BUZZER, 1000, 200);
        delay(250);
        tone(PIN_BUZZER, 1500, 200);
    } else {
        tone(PIN_BUZZER, 300, 500);
    }
}

// ── SPIFFS retry queue ────────────────────────────────────────────────────────
// Format: one JSON per line — {"device":"...","uid":"..."}
// Flush on MQTT reconnect. Cap at QUEUE_MAX lines.

void enqueueSpiffs(const String& uid) {
    if (countQueueLines() >= QUEUE_MAX) {
        // Drop oldest: read all, skip first, rewrite
        File f = SPIFFS.open(QUEUE_FILE, "r");
        String contents = "";
        int skipped = 0;
        while (f.available()) {
            String line = f.readStringUntil('\n');
            if (skipped++ == 0) continue;  // drop first (oldest)
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
        delay(50);  // small gap between publishes
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
