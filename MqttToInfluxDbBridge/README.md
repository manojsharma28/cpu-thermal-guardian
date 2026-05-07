# MQTT to InfluxDB Bridge (C#)

A high-performance C# bridge that subscribes to MQTT topics and writes data to InfluxDB 3-core. This bridge is part of the CPU Thermal Guardian digital twin system.

## Features

- **MQTT Integration**: Subscribes to multiple MQTT topics for real-time data
- **InfluxDB 3-core**: Writes time-series data with proper tags and fields
- **JSON Processing**: Parses structured JSON payloads from MQTT messages
- **Error Handling**: Robust error handling with automatic reconnection
- **Docker Support**: Containerized deployment with multi-stage build
- **Async/Await**: Fully asynchronous for high performance

## Configuration

The bridge is configured with the following defaults (can be modified in `Program.cs`):

```csharp
private const string MQTT_BROKER = "localhost";
private const int MQTT_PORT = 1883;
private static readonly string[] MQTT_TOPICS = { "cpu/thermal/data", "cpu/system/data" };

private const string INFLUX_URL = "http://localhost:8181";
private const string INFLUX_TOKEN = "";
private const string INFLUX_ORG = "";
private const string INFLUX_BUCKET = "cpu_twin";
```

## Expected MQTT Payload Format

The bridge expects JSON payloads with the following structure:

```json
{
  "measurement": "thermal_stats",
  "timestamp": 1640995200000000000,
  "fields": {
    "cpu_load": 45.5,
    "actual_temp": 65.2,
    "predicted_temp": 68.1
  },
  "tags": {
    "source": "csharp_monitor",
    "host": "workstation"
  }
}
```

## Dependencies

- **MQTTnet**: MQTT client library
- **InfluxDB.Client**: InfluxDB client library
- **Newtonsoft.Json**: JSON parsing library

## Building and Running

### Local Development

```bash
# Build the project
dotnet build

# Run the bridge
dotnet run
```

### Docker

```bash
# Build the Docker image
docker build -t mqtt-influx-bridge .

# Run the container
docker run --network host mqtt-influx-bridge
```

### Docker Compose

The bridge is included in the main `docker-compose.yml` file in the parent directory.

## Integration

This C# bridge provides an alternative to the Python `mqtt_to_influxdb.py` bridge. Both can run simultaneously or as alternatives depending on your deployment needs.

The C# version offers:
- Better performance for high-throughput scenarios
- Native .NET integration
- Consistent technology stack with other C# components