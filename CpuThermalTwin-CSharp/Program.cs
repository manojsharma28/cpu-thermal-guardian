using System;
using System.Threading;
using System.Threading.Tasks;
using CpuThermalTwin;

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
            // Configuration
            string influxUrl = "http://localhost:8181";
            string bucket = "cpu_twin";
            string organization = "my-org";  // InfluxDB requires non-empty org

            // Initialize thermal monitor
            _thermalMonitor = new ThermalMonitor(influxUrl, bucket, organization);
            _thermalMonitor.Initialize();

            // Initialize system monitor
            _systemMonitor = new SystemMonitor(influxUrl, bucket, organization);
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
             //   var thermalTask = Task.Run(() => RunThermalMonitoring(cts.Token));
                var systemTask = Task.Run(() => RunSystemMonitoring(cts.Token));

                // Wait for both tasks
                Task.WaitAll( systemTask);
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
                _systemMonitor?.MonitorAll();
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
}
