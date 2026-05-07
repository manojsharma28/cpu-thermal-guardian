import paho.mqtt.client as mqtt
import json
from influxdb_client import InfluxDBClient, Point, WritePrecision
from influxdb_client.client.write_api import SYNCHRONOUS

# --- CONFIGURATION ---
MQTT_BROKER = "localhost"
MQTT_PORT = 1883
MQTT_TOPICS = ["cpu/thermal/data", "cpu/system/data"]

INFLUX_URL = "http://localhost:8181"
INFLUX_TOKEN = ""
INFLUX_ORG = ""
INFLUX_BUCKET = "cpu_twin"

# MQTT Client
mqtt_client = mqtt.Client()

# InfluxDB Client
influx_client = InfluxDBClient(url=INFLUX_URL, token=INFLUX_TOKEN, org=INFLUX_ORG)
write_api = influx_client.write_api(write_options=SYNCHRONOUS)

def on_connect(client, userdata, flags, rc):
    print(f"Connected to MQTT broker with result code {rc}")
    for topic in MQTT_TOPICS:
        client.subscribe(topic)
        print(f"Subscribed to topic: {topic}")

def on_message(client, userdata, msg):
    try:
        # Parse the JSON payload
        payload = json.loads(msg.payload.decode())

        measurement = payload.get("measurement")
        timestamp = payload.get("timestamp")
        fields = payload.get("fields", {})
        tags = payload.get("tags", {})

        # Create InfluxDB point
        point = Point(measurement)

        # Add tags
        for tag_key, tag_value in tags.items():
            point = point.tag(tag_key, str(tag_value))

        # Add fields
        for field_key, field_value in fields.items():
            point = point.field(field_key, field_value)

        # Set timestamp (convert from milliseconds to nanoseconds if needed)
        if timestamp:
            point = point.time(timestamp, WritePrecision.NS)

        # Write to InfluxDB
        write_api.write(bucket=INFLUX_BUCKET, org=INFLUX_ORG, record=point)
        print(f"Written {measurement} data to InfluxDB")

    except Exception as e:
        print(f"Error processing message: {e}")

def main():
    # Set up MQTT callbacks
    mqtt_client.on_connect = on_connect
    mqtt_client.on_message = on_message

    # Connect to MQTT broker
    mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)

    print("MQTT to InfluxDB bridge started...")
    print("Press Ctrl+C to stop.")

    try:
        mqtt_client.loop_forever()
    except KeyboardInterrupt:
        print("Stopping bridge...")
        mqtt_client.disconnect()
        influx_client.close()

if __name__ == "__main__":
    main()