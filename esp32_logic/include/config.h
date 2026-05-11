#pragma once

// ── WiFi ────────────────────────────────────────────────────────────────────
#define WIFI_SSID        "your-ssid"
#define WIFI_PASS        "your-password"

// ── MQTT broker ──────────────────────────────────────────────────────────────
// Dev:  IP of machine running docker-compose (e.g. "192.168.1.100")
// Prod: hostname with broker auth enabled
#define MQTT_HOST        "192.168.1.100"
#define MQTT_PORT        1883
#define MQTT_USER        ""          // set for prod broker auth
#define MQTT_PASS        ""
#define DEVICE_ID        "esp32-door-01"

// ── MQTT topics ───────────────────────────────────────────────────────────────
#define TOPIC_SCAN       "access/" DEVICE_ID "/scan"
#define TOPIC_RESPONSE   "access/" DEVICE_ID "/response"
#define TOPIC_STATUS     "access/" DEVICE_ID "/status"

// ── Pins ──────────────────────────────────────────────────────────────────────
// NOTE: RC522 SDA and OLED SDA both listed as GPIO 21 in schematic.
//       SPI (RC522) and I2C (OLED) are separate buses, but review wiring
//       if display conflicts with RFID reads.
#define PIN_RC522_SDA    21   // RC522 SPI chip-select
#define PIN_RC522_SCK    18
#define PIN_RC522_MOSI   23
#define PIN_RC522_MISO   19
#define PIN_RC522_RST    22
#define PIN_OLED_SDA     21   // I2C SDA (shared bus)
#define PIN_OLED_SCL     22   // I2C SCL
#define PIN_SD_CS         5
#define PIN_BUZZER        4

// ── Timing ────────────────────────────────────────────────────────────────────
#define HEARTBEAT_INTERVAL_MS  60000   // 60s between status publishes
#define MQTT_RECONNECT_MS       5000   // retry delay on disconnect
#define RESPONSE_TIMEOUT_MS     3000   // wait for backend response

// ── SPIFFS retry queue ────────────────────────────────────────────────────────
#define QUEUE_FILE       "/queue.txt"
#define QUEUE_MAX        50            // max buffered events before oldest dropped
