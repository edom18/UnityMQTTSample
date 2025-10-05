#include <WiFi.h>
#include <M5StickCPlus.h>
#include <PubSubClient.h>

const char* BROKER_URL = "192.168.11.25";
const uint16_t BROKER_PORT = 1883;

const char* WIFI_SSID = "YOUR SSID";
const char* WIFI_PASSWORD = "YOUR PASSWORD";

// MQTT Settings
static const char* TOPIC = "audio/volume";
static constexpr uint16_t MQTT_BUF_SIZE = 1024;
static constexpr uint16_t MQTT_KEEPALIVE_SEC = 30;

WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);

String clientId;

///
/// MQTT ブローカーへの接続処理
///
bool mqttConnect() {
  mqtt.setServer(BROKER_URL, BROKER_PORT);
  mqtt.setCallback(onMqttMessage);
  mqtt.setBufferSize(MQTT_BUF_SIZE);
  mqtt.setKeepAlive(MQTT_KEEPALIVE_SEC);
  mqtt.setSocketTimeout(15);

  // Last Will and Testament（LWT）
  const char* willTopic = TOPIC;
  const char* willMsg   = "{\"status\":\"offline\"}";
  bool willRetain = false;

  Serial.printf("[MQTT] Connecting to %s:%d ...\n", BROKER_URL, BROKER_PORT);
  
  M5.Lcd.fillScreen(BLACK);
  M5.Lcd.setCursor(0, 0);
  M5.Lcd.println("Connecting to MQTT...");

  bool ok = mqtt.connect(clientId.c_str(), nullptr, nullptr, willTopic, 1, willRetain, willMsg, true);

  if (ok) {
    Serial.println("[MQTT] Connected.");
    // 購読
    if (mqtt.subscribe(TOPIC, 1)) {
      Serial.printf("[MQTT] Subscribed: %s\n", TOPIC);
      M5.Lcd.fillScreen(BLACK);
      M5.Lcd.setCursor(0, 0);
      M5.Lcd.println("Connected MQTT.");
    }
    else {
      Serial.println("[MQTT] Subscribe failed");
    }
  }
  else {
    Serial.printf("[MQTT] Connect failed. State=%d\n", mqtt.state());
  }
  return ok;
}

///
/// Wi-Fi の接続確認と接続
///
void ensureWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  Serial.printf("WiFi connecting to %s", WIFI_SSID);
  int tries = 0;
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
    if (++tries > 60) {
      ESP.restart();
    }
  }

  Serial.println("\nConnected to Wi-Fi");
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());
}

///
/// MQTT ブローカーへの接続確認と接続
///
void ensureMqtt() {
  if (mqtt.connected()) return;

  // 再接続リトライ：指数バックオフ風（最大5秒）
  static uint32_t lastAttempt = 0;
  static uint32_t backoff = 500; // ms
  uint32_t now = millis();
  if (now - lastAttempt < backoff) return;

  if (!mqttConnect()) {
    backoff = min<uint32_t>(backoff * 2, 5000);
  }
  else {
    backoff = 500;
  }
  lastAttempt = now;
}

///
/// MQTT の受信コールバック
///
void onMqttMessage(char* topic, byte* payload, unsigned int length) {
  Serial.printf("[MQTT] Message arrived: topic=%s, len=%u\n", topic, length);

  String msg;
  msg.reserve(length);
  for (unsigned int i = 0; i < length; i++) {
    msg += (char)payload[i];
  }
  Serial.printf("[MQTT] Payload: %s\n", msg.c_str());
  M5.Lcd.fillScreen(BLACK);
  M5.Lcd.setCursor(0, 0);
  M5.Lcd.printf("[MQTT] Payload: %s\n", msg.c_str());
}

///
/// 一意な ClientID（MAC末尾を付与）
///
void setClientId() {
  uint64_t mac = ESP.getEfuseMac();
  char buf[32];
  snprintf(buf, sizeof(buf), "m5stack-%02X%02X%02X", (uint8_t)(mac>>16), (uint8_t)(mac>>8), (uint8_t)mac);
  clientId = String(buf);
}

///
/// セットアップ
///
void setup() {
  Serial.begin(115200);

  M5.begin();
  M5.Lcd.begin();
  M5.Lcd.setTextSize(2);
  M5.Lcd.setRotation(1);

  delay(500);

  M5.Lcd.println("Connecting to Wi-Fi...");

  setClientId();

  // 接続
  ensureWifi();
  ensureMqtt();
}

///
/// ループ
///
void loop() {
  M5.update();

  // 接続維持
  ensureWifi();
  ensureMqtt();

  if (mqtt.connected()) {
    mqtt.loop();
  }
}