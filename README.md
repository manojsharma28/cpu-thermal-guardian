# CPU Thermal Guardian: Digital Twin Project

## Overview
This project creates a **digital twin** for CPU thermal monitoring using Python, C#, MQTT, InfluxDB 3-core, and Grafana. The system collects real-time CPU load data, predicts temperatures using a thermal model, and visualizes the results in interactive dashboards. Data flows through an MQTT message broker as a middle layer between applications and the database.

## Architecture
- **Python App**: Monitors CPU usage and publishes data to MQTT
- **C# App**: Comprehensive system monitoring (thermal, OS, battery, disk, etc.) publishing to MQTT
- **C# MQTT-to-InfluxDB Bridge**: High-performance bridge that subscribes to MQTT topics and writes to InfluxDB
- **Python MQTT-to-InfluxDB Bridge**: Alternative bridge implementation in Python
- **MQTT Broker**: Message broker for decoupling producers and consumers
- **InfluxDB 3-core**: Time-series database storing thermal statistics
- **Grafana**: Visualization dashboard for real-time monitoring

## Components

### 1. Python Digital Twin (`cpu_twin.py`)
**Purpose**: Collects CPU load, calculates predicted temperature, publishes to MQTT

**Key Features**:
- Real-time CPU monitoring using `psutil`
- Digital twin logic: `predicted_temp = AMBIENT_TEMP + (cpu_load * THERMAL_CONSTANT)`
- Fallback temperature simulation for Windows
- MQTT client integration

**Configuration**:
```python
MQTT_BROKER = "localhost"
MQTT_PORT = 1883
MQTT_TOPIC = "cpu/thermal/data"
THERMAL_CONSTANT = 0.45  # Degrees per 1% CPU load
AMBIENT_TEMP = 35.0      # Base temperature
```

**Data Points**:
- `actual_temp`: Real/simulated CPU temperature
- `predicted_temp`: Digital twin prediction
- `cpu_load`: Current CPU utilization percentage

### 2. C# System Monitor (`CpuThermalTwin-CSharp/`)
**Purpose**: Comprehensive Windows system monitoring publishing to MQTT

**Components**:
- **ThermalMonitor**: CPU temperature, load, and fan monitoring
- **SystemMonitor**: OS info, performance, battery, power, events, disk, CPU sensors

**Key Features**:
- WMI queries for system information
- Performance counters for CPU monitoring
- MQTT publishing for all metrics
- Cross-platform compatibility (Windows-focused)

**Configuration**:
```csharp
string mqttBroker = "localhost";
int mqttPort = 1883;
string thermalTopic = "cpu/thermal/data";
string systemTopic = "cpu/system/data";
```

### 3. MQTT-to-InfluxDB Bridge (Python)
**Purpose**: Subscribes to MQTT topics and writes data to InfluxDB

**Key Features**:
- Subscribes to multiple MQTT topics
- JSON payload parsing
- InfluxDB point creation with proper tags and fields
- Error handling and reconnection

**Configuration**:
```python
MQTT_TOPICS = ["cpu/thermal/data", "cpu/system/data"]
INFLUX_BUCKET = "cpu_twin"
```

### 4. MQTT-to-InfluxDB Bridge (C#)
**Purpose**: High-performance C# implementation of the MQTT-to-InfluxDB bridge

**Key Features**:
- Asynchronous MQTT subscription and InfluxDB writing
- Robust error handling with automatic reconnection
- Docker containerization with multi-stage build
- Type-safe JSON processing with Newtonsoft.Json

**Configuration**:
```csharp
private static readonly string[] MQTT_TOPICS = { "cpu/thermal/data", "cpu/system/data" };
private const string INFLUX_BUCKET = "cpu_twin";
```

**Location**: `MqttToInfluxDbBridge/` directory

### 4. InfluxDB 3-core Setup
**Docker Command**:
```bash
docker run -d -p 8181:8181 --mount type=bind,source=C:\data,target=/data \
  <image> serve --node-id node1 --object-store file --data-dir /data --without-auth
```

**Features**:
- No authentication for development/testing
- SQL-based querying
- Persistent data storage

**Query Example**:
```sql
SELECT actual_temp, predicted_temp, cpu_load FROM thermal_stats
WHERE source = 'python_twin'
```

### 3. Grafana Integration
**Data Source Configuration**:
- Type: InfluxDB
- URL: `http://host.docker.internal:8181`
- Database: `cpu_twin`
- Query Language: InfluxQL

**Dashboard Panels**:
- **Time Series**: Temperature trends over time
- **Table**: Raw data display
- **Stat**: Current CPU load gauge

**Auto-refresh**: 5-second intervals for live monitoring

## Installation & Setup

### Prerequisites
- Python 3.8+
- .NET 8.0 SDK
- Docker
- Git

### 1. Environment Setup
```bash
# Create Python virtual environment
python -m venv .venv
.venv\Scripts\activate  # Windows

# Install Python dependencies
pip install -r requirements.txt
```

### 2. MQTT Broker Setup
```bash
# Run Eclipse Mosquitto MQTT broker
docker run -d -p 1883:1883 --name mosquitto eclipse-mosquitto:latest
```

### 3. InfluxDB Setup
```bash
# Run InfluxDB 3-core (adjust image hash)
docker run -d -p 8181:8181 --mount type=bind,source=C:\data,target=/data \
  sha256:193f629c9869b8dfa05aee3b374baab8c44a87acc297e732f097bc8be2f69976 \
  serve --node-id node1 --object-store file --data-dir /data --without-auth
```

### 4. Build C# Applications
```bash
# Build C# system monitor
cd CpuThermalTwin-CSharp
dotnet build

# Build C# MQTT-to-InfluxDB bridge
cd ../MqttToInfluxDbBridge
dotnet build
cd ..
```

### 5. Grafana Setup
```bash
# Run Grafana in Docker
docker run -d -p 3000:3000 --name grafana \
  -e "GF_SECURITY_ADMIN_PASSWORD=admin" \
  grafana/grafana:latest
```

**Note**: For Docker networking, use `host.docker.internal` in Grafana data source URL to connect to InfluxDB.

### 4. Grafana Setup
1. Access Grafana at `http://localhost:3000` (admin/admin)
2. Add InfluxDB data source
3. Create dashboard with panels
4. Set auto-refresh to 5s

## Usage

### Running the System
1. Start MQTT broker: `docker start mosquitto`
2. Start InfluxDB container
3. **Choose one bridge option:**
   - Python bridge: `python mqtt_to_influxdb.py`
   - C# bridge: `cd MqttToInfluxDbBridge && dotnet run`
4. Run Python monitor: `python cpu_twin.py`
5. Run C# monitor: `cd CpuThermalTwin-CSharp && dotnet run`
6. Open Grafana dashboard at `http://localhost:3000`
7. Watch live thermal and system data

### Data Flow
1. **Collect**: CPU load and system metrics via psutil/WMI
2. **Predict**: Temperature using thermal model
3. **Publish**: Data to MQTT topics as JSON
4. **Subscribe**: Bridge receives MQTT messages
5. **Store**: Data points in InfluxDB with proper tags
6. **Visualize**: Real-time charts in Grafana

### MQTT Topics
- `cpu/thermal/data`: Thermal statistics from Python and C# apps
- `cpu/system/data`: Comprehensive system monitoring from C# app

### Data Sources
- **python_twin**: Python application thermal data
- **csharp_twin**: C# thermal monitor data
- **csharp_system**: C# system monitor data

## Key Achievements

### ✅ Digital Twin Implementation
- Thermal prediction model based on CPU load
- Real-time data collection and storage
- Predictive analytics for thermal management

### ✅ Multi-Language Architecture
- Python for lightweight monitoring
- C# for comprehensive Windows system monitoring
- MQTT for language-agnostic communication

### ✅ Decoupled Architecture
- MQTT message broker for loose coupling
- Asynchronous data processing
- Scalable producer-consumer pattern

### ✅ Modern Tech Stack
- MQTT for IoT-style messaging
- InfluxDB 3-core for time-series data
- Grafana for professional dashboards
- Docker for containerized services

### ✅ Live Monitoring
- Multi-source data aggregation
- 5-second refresh cycles
- Multi-panel visualization
- Historical data analysis

## Future Enhancements

### Hardware Integration
- Real temperature sensors (Linux/Mac support)
- GPU thermal monitoring
- Fan speed control
- Hardware health monitoring

### Advanced Analytics
- Machine learning for better predictions
- Anomaly detection
- Predictive maintenance alerts

### MQTT Enhancements
- QoS levels for reliable delivery
- Retained messages for last known state
- Topic-based access control
- MQTT over WebSockets for web clients

### Multi-Platform Support
- Linux thermal monitoring
- macOS support
- Cross-platform system monitoring
- Container orchestration
- Predictive maintenance alerts

### Production Features
- Authentication and security
- High availability setup
- Alerting system

## Troubleshooting

### Common Issues
- **psutil sensors error**: Windows fallback handles this
- **InfluxDB connection**: Check container status and ports
- **Grafana data source**: Use `host.docker.internal` for Docker networking

### Data Verification
```bash
# Check InfluxDB data
docker exec -it <container> influxdb3 query --database cpu_twin \
  "SELECT * FROM thermal_stats ORDER BY time DESC LIMIT 5"
```

## Conclusion
This project demonstrates a complete IoT/digital twin pipeline for CPU thermal monitoring, from data collection to visualization. The system provides real-time insights into CPU performance and thermal behavior, with extensible architecture for additional sensors and analytics.

**Status**: ✅ Fully functional digital twin with live monitoring