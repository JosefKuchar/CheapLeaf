#include <ESP8266WiFi.h>
#include <WiFiUDP.h>
#include <FastLED.h>
#include <DNSServer.h>
#include <ESP8266WebServer.h>
#include <WiFiManager.h>

#define SEGMENTS 14 // Number of segments (triangles for example)
#define PER_SEGMENT 2 // Number of LEDs per segment
#define OFFSET 1 // LED offset (if you want to have notification LED for example)
#define DATA_PIN D3 // Data pin for LED strip
#define COLOR_ORDER GRB // Order of colors, see https://github.com/FastLED/FastLED/wiki/Rgb-calibration for more information
#define LED_TYPE WS2812B // Type of LEDs
#define PORT 4489 // UDP port that will be used for listening

WiFiUDP Udp;
CRGB leds[SEGMENTS * PER_SEGMENT];
std::unique_ptr<ESP8266WebServer> webserver;
char packet[255];

void setup() {
  Serial.begin(115200);

  // Setup leds
  FastLED.addLeds<LED_TYPE, DATA_PIN, COLOR_ORDER>(leds, SEGMENTS * PER_SEGMENT);
  
  Serial.print("Connecting to WiFi");
  WiFiManager wifiManager;
  wifiManager.autoConnect();
  Serial.println("Connected");
  Serial.println(WiFi.localIP());

  // Reset webserver after WiFi Manager magic
  webserver.reset(new ESP8266WebServer(WiFi.localIP(), 80));
  webserver->begin();
  
  Serial.println("Starting UDP server");
  Udp.begin(PORT);
  Serial.println("UDP server started, port 4489");
}

void loop() {
  int packetSize = Udp.parsePacket();
  if (packetSize) {
    int len = Udp.read(packet, 255);
    if (len > 0)
    {
      packet[len] = 0;
    }

    // Update colors based on packet
    for (int i = 0; i < SEGMENTS; i++) {
      for (int j = 0; j < PER_SEGMENT; j++) {
        leds[OFFSET + i * PER_SEGMENT + j].r = packet[i * 3];
        leds[OFFSET + i * PER_SEGMENT + j].g = packet[i * 3 + 1];
        leds[OFFSET + i * PER_SEGMENT + j].b = packet[i * 3 + 2];
      }
    }

    // Send data to the LED strip
    FastLED.show();
  }
}
