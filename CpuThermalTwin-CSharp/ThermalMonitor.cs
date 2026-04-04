using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace CpuThermalTwin;

public class ThermalMonitor
{
    // Digital Twin Constants
    private const float THERMAL_CONSTANT = 0.45f;  // Degrees per 1% CPU load
    private const float AMBIENT_TEMP = 35.0f;      // Base temperature (Celsius)
    private const float FALLBACK_MULTIPLIER = 0.42f;

    // InfluxDB Configuration
    private readonly string _influxUrl;
    private readonly string _bucket;
    private readonly string _organization;

    private InfluxDBClient? _client;
    private WriteApi? _writeApi;
    private PerformanceCounter? _cpuCounter;
    private bool _isRunning;

    public ThermalMonitor(string influxUrl = "http://localhost:8181", 
                          string bucket = "cpu_twin", 
                          string organization = "")
    {
        _influxUrl = influxUrl;
        _bucket = bucket;
        _organization = organization;
        _isRunning = false;
    }

    /// <summary>
    /// Initializes the InfluxDB client and performance counters
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Initialize InfluxDB client (no auth for dev mode)
            var builder = InfluxDBClientFactory.Create(_influxUrl);
            _client = builder;
            _writeApi = _client.GetWriteApi();

            // Initialize CPU performance counter
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _cpuCounter.NextValue(); // Warm up the counter

            Console.WriteLine("✓ ThermalMonitor initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error initializing ThermalMonitor: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets current CPU load percentage
    /// </summary>
    private float GetCpuLoad()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Gets simulated CPU temperature (Windows doesn't expose raw temp via psutil equivalent)
    /// For production, integrate with OpenHardwareMonitor or WMI
    /// </summary>
    private float GetCpuTemp(float cpuLoad)
    {
        // Fallback: Simulate temperature based on CPU load
        return AMBIENT_TEMP + (cpuLoad * FALLBACK_MULTIPLIER);
    }

    /// <summary>
    /// Calculates predicted temperature using the digital twin model
    /// </summary>
    private float GetPredictedTemp(float cpuLoad)
    {
        return AMBIENT_TEMP + (cpuLoad * THERMAL_CONSTANT);
    }

    /// <summary>
    /// Sends thermal data to InfluxDB
    /// </summary>
    private void SendThermalData(float cpuLoad, float actualTemp, float predictedTemp)
    {
        try
        {
            var point = PointData.Measurement("thermal_stats")
                .Field("actual_temp", actualTemp)
                .Field("predicted_temp", predictedTemp)
                .Field("cpu_load", cpuLoad);

            _writeApi?.WritePoint(_bucket, _organization, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error sending data to InfluxDB: {ex.Message}");
        }
    }

    /// <summary>
    /// Main monitoring loop - runs continuously until stopped
    /// </summary>
    public void StartMonitoring(CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        Console.WriteLine("🚀 Digital Twin active... Press Ctrl+C to stop.\n");

        try
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                // Collect metrics
                float cpuLoad = GetCpuLoad();
                float actualTemp = GetCpuTemp(cpuLoad);
                float predictedTemp = GetPredictedTemp(cpuLoad);

                // Send to InfluxDB
                SendThermalData(cpuLoad, actualTemp, predictedTemp);

                // Display output
                Console.WriteLine(
                    $"Load: {cpuLoad:F1}% | Actual: {actualTemp:F2}°C | Predicted: {predictedTemp:F2}°C");

                // Wait before next collection (1 second)
                try
                {
                    Task.Delay(1000, cancellationToken).Wait(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error during monitoring: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Stops the monitoring loop
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        Console.WriteLine("\n🔌 Twin disconnected.");
    }

    /// <summary>
    /// Cleans up resources
    /// </summary>
    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _client?.Dispose();
    }
}
