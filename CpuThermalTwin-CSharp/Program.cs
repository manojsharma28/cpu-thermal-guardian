using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using CpuThermalTwin;

[SupportedOSPlatform("windows")]
class Program
{
    private static ThermalMonitor? _thermalMonitor;
    private static SystemMonitor? _systemMonitor;

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔═══════════════════════════════════════════╗");
        Console.WriteLine("║   CPU Thermal Guardian - C# Edition       ║");
        Console.WriteLine("║   Digital Twin for Thermal Monitoring     ║");
        Console.WriteLine("╚═══════════════════════════════════════════╝\n");

        try
        {
            // MQTT Configuration
            string mqttBroker = "localhost";
            int mqttPort = 1883;
            string thermalTopic = "cpu/thermal/data";
            string systemTopic = "cpu/system/data";

            // Initialize thermal monitor
            _thermalMonitor = new ThermalMonitor(mqttBroker, mqttPort, thermalTopic);
            _thermalMonitor.Initialize();

            // Load metric settings and initialize system monitor
            var metricsConfig = LoadMetricsConfig(Path.Combine(AppContext.BaseDirectory, "metric-config.json"));
            _systemMonitor = new SystemMonitor(mqttBroker, mqttPort, systemTopic, metricsConfig);
            _systemMonitor.Initialize();

            // Setup graceful shutdown
            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    _thermalMonitor.Stop();
                };

                // Start monitoring tasks
                var thermalTask = Task.Run(() => RunThermalMonitoring(cts.Token));
                var systemTask = Task.Run(() => RunSystemMonitoring(cts.Token));

                // Wait for both tasks
                Task.WaitAll( thermalTask, systemTask);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
        finally
        {
            _thermalMonitor?.Dispose();
            _systemMonitor?.Dispose();
            Console.WriteLine("\n✓ Cleanup complete. Goodbye!");
        }
    }

    private static void RunThermalMonitoring(CancellationToken cancellationToken)
    {
        Console.WriteLine("🚀 Starting thermal monitoring...");
        _thermalMonitor?.StartMonitoring(cancellationToken);
    }

    private static void RunSystemMonitoring(CancellationToken cancellationToken)
    {
        Console.WriteLine("🚀 Starting system monitoring...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Task.Run(() => _systemMonitor?.MonitorAll()).Wait(cancellationToken);
                Task.Delay(5000, cancellationToken).Wait(cancellationToken); // Monitor every 5 seconds
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ System monitoring error: {ex.Message}");
        }
    }

    private static MetricsConfig LoadMetricsConfig(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"⚠️ Metric config not found at {path}; using default metric settings.");
                return new MetricsConfig();
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<MetricsConfig>(json, options);
            if (config == null)
            {
                Console.WriteLine("⚠️ Metric config could not be parsed; using default metric settings.");
                return new MetricsConfig();
            }

            Console.WriteLine($"✓ Loaded metric config from {path}");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error loading metric config: {ex.Message}. Using default metric settings.");
            return new MetricsConfig();
        }
    }
}
