# CPU Thermal Guardian: Digital Twin Project

## Overview
This project creates a **digital twin** for CPU thermal monitoring using Python, InfluxDB 3-core, and Grafana. The system collects real-time CPU load data, predicts temperatures using a thermal model, and visualizes the results in interactive dashboards.

## Architecture
- **Python App**: Monitors CPU usage and sends data to InfluxDB
- **InfluxDB 3-core**: Time-series database storing thermal statistics
- **Grafana**: Visualization dashboard for real-time monitoring

## Components

### 1. Python Digital Twin (`cpu_twin.py`)
**Purpose**: Collects CPU load, calculates predicted temperature, stores in InfluxDB

**Key Features**:
- Real-time CPU monitoring using `psutil`
- Digital twin logic: `predicted_temp = AMBIENT_TEMP + (cpu_load * THERMAL_CONSTANT)`
- Fallback temperature simulation for Windows
- InfluxDB client integration

**Configuration**:
```python
THERMAL_CONSTANT = 0.45  # Degrees per 1% CPU load
AMBIENT_TEMP = 35.0      # Base temperature
bucket = "cpu_twin"
```

**Data Points**:
- `actual_temp`: Real/simulated CPU temperature
- `predicted_temp`: Digital twin prediction
- `cpu_load`: Current CPU utilization percentage

### 2. InfluxDB 3-core Setup
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
- Docker
- Git

### 1. Environment Setup
```bash
# Create virtual environment
python -m venv .venv
.venv\Scripts\activate  # Windows

# Install dependencies
pip install psutil influxdb-client
```

### 2. InfluxDB Setup
```bash
# Run InfluxDB 3-core (adjust image hash)
docker run -d -p 8181:8181 --mount type=bind,source=C:\data,target=/data \
  sha256:193f629c9869b8dfa05aee3b374baab8c44a87acc297e732f097bc8be2f69976 \
  serve --node-id node1 --object-store file --data-dir /data --without-auth
```

### 3. Grafana Setup
```bash
# Run Grafana in Docker
docker run -d -p 3000:3000 --name grafana \
  -e "GF_SECURITY_ADMIN_PASSWORD=admin" \
  grafana/grafana:latest
```

**Note**: For Docker networking, use `host.docker.internal` in Grafana data source URL to connect to InfluxDB.
```bash
python .venv\cpu_twin.py
# Ctrl+C to stop
```

### 4. Grafana Setup
1. Access Grafana at `http://localhost:3000` (admin/admin)
2. Add InfluxDB data source
3. Create dashboard with panels
4. Set auto-refresh to 5s

## Usage

### Running the System
1. Start InfluxDB container
2. Run `python .venv\cpu_twin.py`
3. Open Grafana dashboard
4. Watch live thermal data

### Data Flow
1. **Collect**: CPU load via psutil
2. **Predict**: Temperature using thermal model
3. **Store**: Data points in InfluxDB
4. **Visualize**: Real-time charts in Grafana

## Key Achievements

### ✅ Digital Twin Implementation
- Thermal prediction model based on CPU load
- Real-time data collection and storage
- Predictive analytics for thermal management

### ✅ Modern Tech Stack
- InfluxDB 3-core for time-series data
- Grafana for professional dashboards
- Python for data processing

### ✅ Live Monitoring
- 5-second refresh cycles
- Multi-panel visualization
- Historical data analysis

## Future Enhancements

### Hardware Integration
- Real temperature sensors (Linux/Mac support)
- GPU thermal monitoring
- Fan speed control

### Advanced Analytics
- Machine learning for better predictions
- Anomaly detection
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