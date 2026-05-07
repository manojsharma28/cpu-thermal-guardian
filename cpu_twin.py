import psutil
import time
import json
import paho.mqtt.client as mqtt

# --- CONFIGURATION ---
MQTT_BROKER = "localhost"
MQTT_PORT = 1883
MQTT_TOPIC = "cpu/thermal/data"

# Digital Twin Constant: How many degrees we expect per 1% of CPU load
# This is our "Virtual Model" of the CPU's thermal efficiency
THERMAL_CONSTANT = 0.45
AMBIENT_TEMP = 35.0

# MQTT Client setup
client = mqtt.Client()
client.connect(MQTT_BROKER, MQTT_PORT, 60)

# Digital Twin Constant: How many degrees we expect per 1% of CPU load
# This is our "Virtual Model" of the CPU's thermal efficiency
THERMAL_CONSTANT = 0.45 
AMBIENT_TEMP = 35.0

def get_cpu_temp():
    # Note: temp support varies by OS. On Windows, psutil might need help.
    # If this returns None, we will use a fallback dummy for testing.
    if hasattr(psutil, 'sensors_temperatures'):
        temps = psutil.sensors_temperatures()
        if 'coretemp' in temps:
            return temps['coretemp'][0].current
    return None

print("Digital Twin active... Press Ctrl+C to stop.")

try:
    while True:
        cpu_load = psutil.cpu_percent(interval=1)
        actual_temp = get_cpu_temp() or (AMBIENT_TEMP + (cpu_load * 0.42)) # Fallback

        # --- THE DIGITAL TWIN LOGIC ---
        # We calculate the "Predicted" temp based on our idealized model
        predicted_temp = AMBIENT_TEMP + (cpu_load * THERMAL_CONSTANT)

        # Create data payload
        data = {
            "measurement": "thermal_stats",
            "timestamp": time.time_ns(),
            "fields": {
                "actual_temp": float(actual_temp),
                "predicted_temp": float(predicted_temp),
                "cpu_load": float(cpu_load)
            },
            "tags": {
                "source": "python_twin"
            }
        }

        # Publish to MQTT
        client.publish(MQTT_TOPIC, json.dumps(data))
        print(f"Load: {cpu_load}% | Actual: {actual_temp}C | Predicted: {predicted_temp}C")

except KeyboardInterrupt:
    print("Twin disconnected.")
    client.disconnect()