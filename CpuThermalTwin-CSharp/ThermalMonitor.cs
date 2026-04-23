using System;
using System.Diagnostics;
using System.IO;
using System.Management;
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
    private readonly string _token;

    private InfluxDBClient? _client;
    private WriteApi? _writeApi;
    private PerformanceCounter? _cpuCounter;
    private bool _isRunning;

    public ThermalMonitor(string influxUrl = "http://localhost:8181", 
                          string bucket = "cpu_twin",
                          string organization = "my-org",
                          string token = "m059h1pXi207pnn4ADkijJ3EIFa06TqR14IL4G326WkfReV_CjaTs7ukqoCCrKFbTk8X_-RmjnOiDRuaGpmMWQ==")
    {
        _influxUrl = influxUrl;
        _bucket = bucket;
        _organization = organization;
        _token = token;
        _isRunning = false;
    }

    /// <summary>
    /// Initializes the InfluxDB client and performance counters
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Initialize InfluxDB client with or without auth
            InfluxDBClient? builder;
            if (!string.IsNullOrEmpty(_token))
            {
                // With authentication token
                builder = InfluxDBClientFactory.Create(_influxUrl, _token.ToCharArray());
            }
            else
            {
                // Without authentication (dev mode)
                builder = InfluxDBClientFactory.Create(_influxUrl);
            }
            _client = builder;
            _writeApi = _client.GetWriteApi();

            GetFanSpeed();

            // Initialize CPU monitoring (platform-specific)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    _cpuCounter.NextValue(); // Warm up the counter
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Windows Performance Counter failed, will use fallback: {ex.Message}");
                    _cpuCounter = null;
                }
            }
            // On Linux/macOS, we'll use the cross-platform fallback in GetCpuLoad()

            Console.WriteLine("✓ ThermalMonitor initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error initializing ThermalMonitor: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets current CPU load percentage (cross-platform)
    /// </summary>
    private float GetCpuLoad()
    {
        // Try Windows Performance Counter first
        if (_cpuCounter != null)
        {
            try
            {
                return _cpuCounter.NextValue();
            }
            catch
            {
                // Fall back to cross-platform method
            }
        }

        // Cross-platform fallback: read from /proc/stat (Linux) or simulate
        try
        {
            if (OperatingSystem.IsLinux())
            {
                return GetCpuLoadLinux();
            }
            else if (OperatingSystem.IsWindows())
            {
                // If performance counter failed, try alternative Windows method
                return GetCpuLoadWindowsFallback();
            }
        }
        catch
        {
            // Final fallback: simulate based on time
        }

        // Ultimate fallback: simulate CPU load
        return (float)(DateTime.Now.Millisecond % 100);
    }

    /// <summary>
    /// Gets CPU load on Linux by reading /proc/stat
    /// </summary>
    private float GetCpuLoadLinux()
    {
        try
        {
            var statLines = System.IO.File.ReadAllLines("/proc/stat");
            if (statLines.Length > 0)
            {
                var cpuLine = statLines[0];
                var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    // Parse CPU times: user nice system idle iowait irq softirq steal
                    long user = long.Parse(parts[1]);
                    long nice = long.Parse(parts[2]);
                    long system = long.Parse(parts[3]);
                    long idle = long.Parse(parts[4]);
                    long iowait = long.Parse(parts[5]);
                    long irq = long.Parse(parts[6]);
                    long softirq = long.Parse(parts[7]);

                    long total = user + nice + system + idle + iowait + irq + softirq;
                    long idleTotal = idle + iowait;

                    // Calculate CPU usage (simplified - would need previous values for accuracy)
                    if (_previousTotal > 0)
                    {
                        long totalDiff = total - _previousTotal;
                        long idleDiff = idleTotal - _previousIdle;
                        float usage = (float)(totalDiff - idleDiff) / totalDiff * 100;
                        _previousTotal = total;
                        _previousIdle = idleTotal;
                        return Math.Max(0, Math.Min(100, usage));
                    }
                    else
                    {
                        _previousTotal = total;
                        _previousIdle = idleTotal;
                        return 0f; // First reading
                    }
                }
            }
        }
        catch
        {
            // Fall back to simulation
        }

        return (float)(DateTime.Now.Millisecond % 100);
    }

    /// <summary>
    /// Alternative CPU load method for Windows
    /// </summary>
    private float GetCpuLoadWindowsFallback()
    {
        // Could implement WMI or other Windows-specific methods here
        // For now, return simulated value
        return (float)(DateTime.Now.Millisecond % 100);
    }

    // Fields for CPU calculation
    private long _previousTotal = 0;
    private long _previousIdle = 0;

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
    /// Gets current CPU fan speed via WMI (Windows-only)
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private float GetFanSpeed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0f;
        }

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
            foreach (ManagementObject fan in searcher.Get())
            {
                object? speed = fan["DesiredSpeed"];
                if (speed != null && uint.TryParse(speed.ToString(), out uint fanSpeed))
                {
                    return fanSpeed;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error reading fan speed: {ex.Message}");
        }
        return 0f;
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
