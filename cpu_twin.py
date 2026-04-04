import psutil
import time
from influxdb_client import InfluxDBClient, Point, WritePrecision
from influxdb_client.client.write_api import SYNCHRONOUS

# --- CONFIGURATION ---
token = ""
org = ""
bucket = "cpu_twin"
url = "http://localhost:8181"

client = InfluxDBClient(url=url, token=token, org=org)
write_api = client.write_api(write_options=SYNCHRONOUS)

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
        
        # Create Data Points
        point = Point("thermal_stats") \
            .field("actual_temp", float(actual_temp)) \
            .field("predicted_temp", float(predicted_temp)) \
            .field("cpu_load", float(cpu_load)) \
            .time(time.time_ns(), WritePrecision.NS)
        
        write_api.write(bucket=bucket, org=org, record=point)
        print(f"Load: {cpu_load}% | Actual: {actual_temp}C | Predicted: {predicted_temp}C")
        
except KeyboardInterrupt:
    print("Twin disconnected.")