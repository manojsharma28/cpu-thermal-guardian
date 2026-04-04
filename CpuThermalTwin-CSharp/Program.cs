using System;
using System.Threading;
using System.Threading.Tasks;
using CpuThermalTwin;

class Program
{
    private static ThermalMonitor? _monitor;

    static async Task Main(string[] args)
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
            string organization = "";

            // Initialize thermal monitor
            _monitor = new ThermalMonitor(influxUrl, bucket, organization);
            _monitor.Initialize();

            // Setup graceful shutdown
            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    _monitor.Stop();
                };

                // Start monitoring
                await _monitor.StartMonitoringAsync(cts.Token);
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
            _monitor?.Dispose();
            Console.WriteLine("\n✓ Cleanup complete. Goodbye!");
        }
    }
}
