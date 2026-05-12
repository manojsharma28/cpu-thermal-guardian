using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Connecting;

namespace CpuThermalTwin;

/// <summary>
/// Comprehensive system monitoring class using WMI queries
/// Monitors OS, Power Supply, Log Events, Disk, and CPU sensors
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemMonitor
{
    private readonly string _mqttBroker;
    private readonly int _mqttPort;
    private readonly string _mqttTopic;
    private readonly MetricsConfig _metricsConfig;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _mqttOptions;

    public SystemMonitor(string mqttBroker = "localhost",
                        int mqttPort = 1883,
                        string mqttTopic = "cpu/system/data",
                        MetricsConfig? metricsConfig = null)
    {
        _mqttBroker = mqttBroker;
        _mqttPort = mqttPort;
        _mqttTopic = mqttTopic;
        _metricsConfig = metricsConfig ?? new MetricsConfig();
    }

    /// <summary>
    /// Initialize MQTT client
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Initialize MQTT client
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttOptions = (MqttClientOptions?)new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttBroker, _mqttPort)
                .Build();

            var connectResult = _mqttClient.ConnectAsync(_mqttOptions).Result;
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new Exception($"Failed to connect to MQTT broker: {connectResult.ResultCode}");
            }
            Console.WriteLine("✓ SystemMonitor initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error initializing SystemMonitor: {ex.Message}");
            throw;
        }
    }

    #region OS System Monitoring

    /// <summary>
    /// Monitors operating system information and performance
    /// </summary>
    public async void MonitorOperatingSystem()
    {
        try
        {
            // Get OS information
            var osInfo = GetOperatingSystemInfo();
            await Task.Run(() => SendOsMetrics(osInfo));

            // Get system performance
            var perfInfo = GetSystemPerformance();
            await Task.Run(() => SendSystemPerformanceMetrics(perfInfo));

            // Get process information
            var processInfo = GetProcessInformation();
            await Task.Run(() => SendProcessMetrics(processInfo));

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring OS: {ex.Message}");
        }
    }

    private async void SendProcessMetrics(ProcessInfo processInfo)
    {
        try
        {
            var fields = new Dictionary<string, object?>();

            if (IsEnabled(_metricsConfig.SystemMetrics, "total_processes"))
                fields["total_processes"] = processInfo.TotalProcesses;
            if (IsEnabled(_metricsConfig.SystemMetrics, "total_threads"))
                fields["total_threads"] = processInfo.Threads;

            if (fields.Count == 0 && _metricsConfig.SystemMetrics.Count > 0)
                return;

            if (fields.Count == 0)
            {
                fields["total_processes"] = processInfo.TotalProcesses;
                fields["total_threads"] = processInfo.Threads;
            }

            await PublishToMqtt("process_info", fields);

            // Send top CPU consuming processes as separate messages
            foreach (var process in processInfo.TopCpuProcesses)
            {
                await PublishToMqtt("top_processes", new
                {
                    cpu_time_ms = process.CpuTime,
                    memory_usage_bytes = process.MemoryUsage
                }, new Dictionary<string, string>
                {
                    { "source", "csharp_system" },
                    { "process_name", process.Name },
                    { "process_id", process.Id.ToString() }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending process metrics: {ex.Message}");
        }
    }

    private async void SendSystemPerformanceMetrics(SystemPerformanceInfo perfInfo)
    {
        try
        {
            var fields = new Dictionary<string, object?>();

            if (IsEnabled(_metricsConfig.SystemMetrics, "cpu_usage_percent"))
                fields["cpu_usage_percent"] = perfInfo.CpuUsagePercent;
            if (IsEnabled(_metricsConfig.SystemMetrics, "memory_usage_percent"))
                fields["memory_usage_percent"] = perfInfo.MemoryUsagePercent;
            if (IsEnabled(_metricsConfig.SystemMetrics, "disk_read_bytes_per_sec"))
                fields["disk_read_bytes_per_sec"] = perfInfo.DiskReadBytesPerSec;
            if (IsEnabled(_metricsConfig.SystemMetrics, "disk_write_bytes_per_sec"))
                fields["disk_write_bytes_per_sec"] = perfInfo.DiskWriteBytesPerSec;
            if (IsEnabled(_metricsConfig.SystemMetrics, "network_bytes_per_sec"))
                fields["network_bytes_per_sec"] = perfInfo.NetworkBytesPerSec;

            if (fields.Count == 0 && _metricsConfig.SystemMetrics.Count > 0)
                return;

            if (fields.Count == 0)
            {
                fields = new Dictionary<string, object?>
                {
                    { "cpu_usage_percent", perfInfo.CpuUsagePercent },
                    { "memory_usage_percent", perfInfo.MemoryUsagePercent },
                    { "disk_read_bytes_per_sec", perfInfo.DiskReadBytesPerSec },
                    { "disk_write_bytes_per_sec", perfInfo.DiskWriteBytesPerSec },
                    { "network_bytes_per_sec", perfInfo.NetworkBytesPerSec }
                };
            }

            await PublishToMqtt("system_performance", fields);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending system performance metrics: {ex.Message}");
        }
    }

    private async void SendOsMetrics(OperatingSystemInfo osInfo)
    {
        try
        {
            var fields = new Dictionary<string, object?>();
            var tags = new Dictionary<string, string> { { "source", "csharp_system" } };

            if (IsEnabled(_metricsConfig.OsMetrics, "os_total_memory_mb"))
                fields["os_total_memory_mb"] = osInfo.TotalVisibleMemorySize / 1024.0;

            if (IsEnabled(_metricsConfig.OsMetrics, "os_free_memory_mb"))
                fields["os_free_memory_mb"] = osInfo.FreePhysicalMemory / 1024.0;

            if (IsEnabled(_metricsConfig.OsMetrics, "os_memory_usage_percent") && osInfo.TotalVisibleMemorySize > 0)
                fields["os_memory_usage_percent"] = (osInfo.TotalVisibleMemorySize - osInfo.FreePhysicalMemory) * 100.0 / osInfo.TotalVisibleMemorySize;

            if (IsEnabled(_metricsConfig.OsMetrics, "total_virtual_memory_mb"))
                fields["total_virtual_memory_mb"] = osInfo.TotalVirtualMemorySize / 1024.0;

            if (IsEnabled(_metricsConfig.OsMetrics, "free_virtual_memory_mb"))
                fields["free_virtual_memory_mb"] = osInfo.FreeVirtualMemory / 1024.0;

            if (IsEnabled(_metricsConfig.OsMetrics, "boot_uptime_seconds"))
            {
                if (DateTime.TryParse(osInfo.LastBootUpTime, out DateTime bootTime))
                {
                    fields["boot_uptime_seconds"] = (DateTime.Now - bootTime).TotalSeconds;
                }
            }

            if (IsEnabled(_metricsConfig.OsMetrics, "os_version"))
                tags["os_version"] = osInfo.Version;
            if (IsEnabled(_metricsConfig.OsMetrics, "os_build"))
                tags["os_build"] = osInfo.BuildNumber;
            if (IsEnabled(_metricsConfig.OsMetrics, "os_architecture"))
                tags["os_architecture"] = osInfo.Architecture;

            if (fields.Count == 0 && _metricsConfig.OsMetrics.Count > 0)
                return;

            if (fields.Count == 0)
            {
                fields = new Dictionary<string, object?>
                {
                    { "total_memory_mb", osInfo.TotalVisibleMemorySize / 1024.0 },
                    { "free_memory_mb", osInfo.FreePhysicalMemory / 1024.0 },
                    { "memory_usage_percent", osInfo.TotalVisibleMemorySize > 0 ? (osInfo.TotalVisibleMemorySize - osInfo.FreePhysicalMemory) * 100.0 / osInfo.TotalVisibleMemorySize : 0 },
                    { "total_virtual_memory_mb", osInfo.TotalVirtualMemorySize / 1024.0 },
                    { "free_virtual_memory_mb", osInfo.FreeVirtualMemory / 1024.0 }
                };
                tags["os_version"] = osInfo.Version;
                tags["os_build"] = osInfo.BuildNumber;
                tags["os_architecture"] = osInfo.Architecture;
            }

            await PublishToMqtt("os_info", fields, tags);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending OS metrics: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets comprehensive operating system information
    /// </summary>
    private OperatingSystemInfo GetOperatingSystemInfo()
    {
        var info = new OperatingSystemInfo();

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                info.Caption = os["Caption"]?.ToString() ?? "Unknown";
                info.Version = os["Version"]?.ToString() ?? "Unknown";
                info.BuildNumber = os["BuildNumber"]?.ToString() ?? "Unknown";
                info.Architecture = os["OSArchitecture"]?.ToString() ?? "Unknown";
                info.LastBootUpTime = os["LastBootUpTime"]?.ToString() ?? "Unknown";
                info.TotalVisibleMemorySize = Convert.ToUInt64(os["TotalVisibleMemorySize"] ?? 0);
                info.FreePhysicalMemory = Convert.ToUInt64(os["FreePhysicalMemory"] ?? 0);
                info.TotalVirtualMemorySize = Convert.ToUInt64(os["TotalVirtualMemorySize"] ?? 0);
                info.FreeVirtualMemory = Convert.ToUInt64(os["FreeVirtualMemory"] ?? 0);
                break; // Only need first result
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting OS info: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Gets system performance metrics
    /// </summary>
    private SystemPerformanceInfo GetSystemPerformance()
    {
        var info = new SystemPerformanceInfo();

        try
        {
            // CPU usage
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            {
                cpuCounter.NextValue(); // First call returns 0
                System.Threading.Thread.Sleep(100);
                info.CpuUsagePercent = cpuCounter.NextValue();
            }

            // Memory usage
            using (var memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use"))
            {
                info.MemoryUsagePercent = memCounter.NextValue();
            }

            // Disk I/O
            using (var diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total"))
            using (var diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total"))
            {
                info.DiskReadBytesPerSec = diskReadCounter.NextValue();
                info.DiskWriteBytesPerSec = diskWriteCounter.NextValue();
            }

            // Network I/O - with fallback if adapter not available
            try
            {
                var networkInterface = GetNetworkInterface();
                if (!string.IsNullOrEmpty(networkInterface))
                {
                    using (var networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", networkInterface))
                    {
                        info.NetworkBytesPerSec = networkCounter.NextValue();
                    }
                }
                else
                {
                    // Use _Total if specific interface not found
                    using (var networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", "_Total", true))
                    {
                        info.NetworkBytesPerSec = networkCounter.NextValue();
                    }
                }
            }
            catch
            {
                // If network counter fails, set to 0 but don't fail the entire method
                info.NetworkBytesPerSec = 0;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting system performance: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Gets process information
    /// </summary>
    private ProcessInfo GetProcessInformation()
    {
        var info = new ProcessInfo();

        try
        {
            var processes = Process.GetProcesses();
            info.TotalProcesses = processes.Length;
            info.Threads = processes.Sum(p => p.Threads.Count);

            // Get top CPU consuming processes
            info.TopCpuProcesses = processes
                .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                .OrderByDescending(p =>
                {
                    try { return p.TotalProcessorTime.TotalMilliseconds; }
                    catch { return 0; }
                })
                .Take(5)
                .Select(p => new ProcessData
                {
                    Name = p.ProcessName,
                    Id = p.Id,
                    CpuTime = p.TotalProcessorTime.TotalMilliseconds,
                    MemoryUsage = p.WorkingSet64
                })
                .ToList();

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting process info: {ex.Message}");
        }

        return info;
    }

    #endregion

    #region Power Supply Monitoring

    /// <summary>
    /// Monitors power supply and battery information
    /// </summary>
    public void MonitorPowerSupply()
    {
        try
        {
            var batteryInfo = GetBatteryInformation();
            SendBatteryMetrics(batteryInfo);

            var powerInfo = GetPowerInformation();
            SendPowerMetrics(powerInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring power supply: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets battery information
    /// </summary>
    private BatteryInfo GetBatteryInformation()
    {
        var info = new BatteryInfo();

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (ManagementObject battery in searcher.Get())
            {
                info.Name = battery["Name"]?.ToString() ?? "Unknown";
                info.Status = Convert.ToUInt16(battery["BatteryStatus"] ?? 0);
                info.RemainingCapacity = Convert.ToUInt32(battery["EstimatedChargeRemaining"] ?? 0);
                info.FullChargeCapacity = Convert.ToUInt32(battery["FullChargeCapacity"] ?? 0);
                info.Voltage = Convert.ToUInt32(battery["DesignVoltage"] ?? 0);
                info.Chemistry = battery["Chemistry"]?.ToString() ?? "Unknown";
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting battery info: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Gets power supply information
    /// </summary>
    private PowerInfo GetPowerInformation()
    {
        var info = new PowerInfo();

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_UninterruptiblePowerSupply");
            foreach (ManagementObject power in searcher.Get())
            {
                info.Name = power["Name"]?.ToString() ?? "Unknown";
                info.Status = power["Status"]?.ToString() ?? "Unknown";
                info.IsSwitchingSupply = Convert.ToBoolean(power["IsSwitchingSupply"] ?? false);
                info.TotalPower = Convert.ToUInt32(power["TotalPower"] ?? 0);
                return info;
            }

            // Fallback for systems where UPS class is not available
            searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (ManagementObject battery in searcher.Get())
            {
                info.Name = battery["Name"]?.ToString() ?? "Unknown";
                info.Status = battery["BatteryStatus"]?.ToString() ?? "Unknown";
                info.IsSwitchingSupply = false;
                info.TotalPower = 0;
                return info;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting power info: {ex.Message}");
        }

        return info;
    }

    #endregion

    #region Log Events Monitoring

    /// <summary>
    /// Monitors system event logs
    /// </summary>
    public void MonitorLogEvents()
    {
        try
        {
            var eventInfo = GetRecentSystemEvents();
            SendEventLogMetrics(eventInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring log events: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets recent system events
    /// </summary>
    private EventLogInfo GetRecentSystemEvents()
    {
        var info = new EventLogInfo();

        try
        {
            // Get system event log
            var eventLog = new EventLog("System");
            var entries = eventLog.Entries
                .Cast<EventLogEntry>()
                .Where(e => e.TimeGenerated > DateTime.Now.AddMinutes(-5)) // Last 5 minutes
                .ToList();

            info.TotalEvents = entries.Count;
            info.ErrorEvents = entries.Count(e => e.EntryType == EventLogEntryType.Error);
            info.WarningEvents = entries.Count(e => e.EntryType == EventLogEntryType.Warning);
            info.InformationEvents = entries.Count(e => e.EntryType == EventLogEntryType.Information);

            // Get most recent critical events
            info.RecentCriticalEvents = entries
                .Where(e => e.EntryType == EventLogEntryType.Error)
                .OrderByDescending(e => e.TimeGenerated)
                .Take(3)
                .Select(e => new EventData
                {
                    Source = e.Source,
                    Message = e.Message.Length > 100 ? e.Message.Substring(0, 100) + "..." : e.Message,
                    TimeGenerated = e.TimeGenerated
                })
                .ToList();

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting event logs: {ex.Message}");
        }

        return info;
    }

    #endregion

    #region Disk Monitoring

    /// <summary>
    /// Monitors disk drives and storage
    /// </summary>
    public void MonitorDisk()
    {
        try
        {
            var diskInfo = GetDiskInformation();
            SendDiskMetrics(diskInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring disk: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets disk drive information
    /// </summary>
    private DiskInfo GetDiskInformation()
    {
        var info = new DiskInfo();

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3"); // Fixed drives
            foreach (ManagementObject disk in searcher.Get())
            {
                var drive = new DiskDrive
                {
                    Name = disk["Name"]?.ToString() ?? "Unknown",
                    VolumeName = disk["VolumeName"]?.ToString() ?? "",
                    FileSystem = disk["FileSystem"]?.ToString() ?? "Unknown",
                    Size = Convert.ToUInt64(disk["Size"] ?? 0),
                    FreeSpace = Convert.ToUInt64(disk["FreeSpace"] ?? 0)
                };

                drive.UsedSpace = drive.Size - drive.FreeSpace;
                drive.UsagePercent = drive.Size > 0 ? (drive.UsedSpace * 100.0) / drive.Size : 0;

                info.Drives.Add(drive);
            }

            // Calculate totals
            info.TotalSize = (ulong)info.Drives.Sum(d => (decimal)d.Size);
            info.TotalFreeSpace = (ulong)info.Drives.Sum(d => (decimal)d.FreeSpace);
            info.TotalUsedSpace = info.TotalSize - info.TotalFreeSpace;
            info.OverallUsagePercent = info.TotalSize > 0 ? (info.TotalUsedSpace * 100.0) / info.TotalSize : 0;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting disk info: {ex.Message}");
        }

        return info;
    }

    #endregion

    #region CPU Sensor Monitoring

    /// <summary>
    /// Monitors CPU sensors and thermal information
    /// </summary>
    public void MonitorCpuSensors()
    {
        try
        {
            var cpuInfo = GetCpuSensorInformation();
            SendCpuSensorMetrics(cpuInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring CPU sensors: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets CPU sensor information
    /// </summary>
    private CpuSensorInfo GetCpuSensorInformation()
    {
        var info = new CpuSensorInfo();

        try
        {
            // Get CPU temperature from thermal zones
            var tempSearcher = new ManagementObjectSearcher("SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject temp in tempSearcher.Get())
            {
                var tempValue = Convert.ToInt32(temp["CurrentTemperature"] ?? 0);
                if (tempValue > 0)
                {
                    // Convert from tenths of Kelvin to Celsius
                    info.CpuTemperature = (tempValue / 10.0) - 273.15;
                    break;
                }
            }

            // Get CPU information
            var cpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject cpu in cpuSearcher.Get())
            {
                info.CpuName = cpu["Name"]?.ToString() ?? "Unknown";
                info.NumberOfCores = Convert.ToInt32(cpu["NumberOfCores"] ?? 1);
                info.NumberOfLogicalProcessors = Convert.ToInt32(cpu["NumberOfLogicalProcessors"] ?? 1);
                info.MaxClockSpeed = Convert.ToUInt32(cpu["MaxClockSpeed"] ?? 0);
                info.CurrentClockSpeed = Convert.ToUInt32(cpu["CurrentClockSpeed"] ?? 0);
                info.LoadPercentage = Convert.ToUInt16(cpu["LoadPercentage"] ?? 0);
                break;
            }

            // Get fan speed
            var fanSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
            foreach (ManagementObject fan in fanSearcher.Get())
            {
                var speed = fan["DesiredSpeed"];
                if (speed != null && uint.TryParse(speed.ToString(), out uint fanSpeed))
                {
                    info.FanSpeed = fanSpeed;
                    break;
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error getting CPU sensor info: {ex.Message}");
        }

        return info;
    }

    #endregion

    #region Helper Methods

    private string GetNetworkInterface()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus=2");
            foreach (ManagementObject adapter in searcher.Get())
            {
                return adapter["Name"]?.ToString() ?? "";
            }
        }
        catch
        {
            // Ignore errors
        }
        return "";
    }

    private bool IsEnabled(Dictionary<string, bool> metricSettings, string metricKey)
    {
        return metricSettings.Count == 0 || (metricSettings.TryGetValue(metricKey, out bool enabled) && enabled);
    }

    private bool HasAnySelection(Dictionary<string, bool> metricSettings)
    {
        return metricSettings.Count == 0 || metricSettings.Values.Any(v => v);
    }

    #endregion

    #region InfluxDB Sending Methods

    /// <summary>
    /// Sends comprehensive system data to InfluxDB in a single measurement (following ThermalMonitor pattern)
    /// </summary>
    private async void SendSystemData(OperatingSystemInfo osInfo,
                               SystemPerformanceInfo perfInfo,
                               ProcessInfo processInfo,
                               BatteryInfo batteryInfo,
                               PowerInfo powerInfo,
                               EventLogInfo eventInfo,
                               DiskInfo diskInfo,
                               CpuSensorInfo cpuInfo)
    {
        try
        {
            // Calculate uptime from LastBootUpTime
            TimeSpan uptime = TimeSpan.Zero;
            if (DateTime.TryParse(osInfo.LastBootUpTime, out DateTime bootTime))
            {
                uptime = DateTime.Now - bootTime;
            }

            // Calculate memory values in GB
            double totalMemoryGB = osInfo.TotalVisibleMemorySize / (1024.0 * 1024.0);
            double availableMemoryGB = osInfo.FreePhysicalMemory / (1024.0 * 1024.0);
            double memoryUsagePercent = osInfo.TotalVisibleMemorySize > 0 ?
                (osInfo.TotalVisibleMemorySize - osInfo.FreePhysicalMemory) * 100.0 / osInfo.TotalVisibleMemorySize : 0;

            var data = new
            {
                os_uptime_seconds = uptime.TotalSeconds,
                os_total_memory_gb = totalMemoryGB,
                os_available_memory_gb = availableMemoryGB,
                os_memory_usage_percent = memoryUsagePercent,
                cpu_load_percent = perfInfo.CpuUsagePercent,
                memory_usage_percent = perfInfo.MemoryUsagePercent,
                disk_read_bytes_per_sec = perfInfo.DiskReadBytesPerSec,
                disk_write_bytes_per_sec = perfInfo.DiskWriteBytesPerSec,
                network_bytes_per_sec = perfInfo.NetworkBytesPerSec,
                total_processes = processInfo.TotalProcesses,
                running_processes = processInfo.TotalProcesses,
                threads_count = processInfo.Threads,
                battery_remaining_capacity_percent = batteryInfo.RemainingCapacity,
                battery_voltage_mv = batteryInfo.Voltage,
                battery_status = batteryInfo.Status,
                power_supply_total_watts = powerInfo.TotalPower,
                power_supply_is_switching = powerInfo.IsSwitchingSupply ? 1 : 0,
                system_events_last_hour = eventInfo.TotalEvents,
                error_events_last_hour = eventInfo.ErrorEvents,
                warning_events_last_hour = eventInfo.WarningEvents,
                disk_total_size_gb = diskInfo.TotalSize / (1024.0 * 1024.0 * 1024.0),
                disk_total_free_gb = diskInfo.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0),
                disk_overall_usage_percent = diskInfo.OverallUsagePercent,
                cpu_temperature_celsius = cpuInfo.CpuTemperature,
                cpu_fan_speed_rpm = cpuInfo.FanSpeed,
                cpu_load_percentage = cpuInfo.LoadPercentage,
                cpu_current_clock_mhz = cpuInfo.CurrentClockSpeed
            };

            var tags = new Dictionary<string, string>
            {
                { "source", "csharp_system" },
                { "os_version", osInfo.Version },
                { "os_build", osInfo.BuildNumber },
                { "os_architecture", osInfo.Architecture },
                { "cpu_name", cpuInfo.CpuName },
                { "battery_status", batteryInfo.Status.ToString() },
                { "power_supply_type", powerInfo.IsSwitchingSupply ? "switching" : "linear" }
            };

            await PublishToMqtt("system_monitor", data, tags);

            // Also send the cupthermal metrics for backward compatibility
            await Task.Run(() => SendCupThermalMetrics(osInfo, perfInfo, batteryInfo, powerInfo, diskInfo, cpuInfo));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending system data: {ex.Message}");
        }
    }

    private async void SendBatteryMetrics(BatteryInfo info)
    {
        try
        {
            await PublishToMqtt("battery_info", new
            {
                remaining_capacity_percent = info.RemainingCapacity,
                voltage_mv = info.Voltage,
                status = info.Status
            }, new Dictionary<string, string>
            {
                { "source", "csharp_system" },
                { "battery_name", info.Name },
                { "chemistry", info.Chemistry },
                { "battery_status", info.Status.ToString() }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending battery metrics: {ex.Message}");
        }
    }

    private async void SendPowerMetrics(PowerInfo info)
    {
        try
        {
            await PublishToMqtt("power_info", new
            {
                total_power_watts = info.TotalPower,
                is_switching_supply = info.IsSwitchingSupply ? 1 : 0
            }, new Dictionary<string, string>
            {
                { "source", "csharp_system" },
                { "power_supply_name", info.Name },
                { "status", info.Status },
                { "power_supply_type", info.IsSwitchingSupply ? "switching" : "linear" }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending power metrics: {ex.Message}");
        }
    }

    private async void SendEventLogMetrics(EventLogInfo info)
    {
        try
        {
            // Send overall event log metrics
            await PublishToMqtt("event_logs", new
            {
                total_events = info.TotalEvents,
                error_events = info.ErrorEvents,
                warning_events = info.WarningEvents,
                information_events = info.InformationEvents
            });

            // Send critical events
            foreach (var evt in info.RecentCriticalEvents)
            {
                await PublishToMqtt("critical_events", new
                {
                    event_time = (long)(evt.TimeGenerated - new DateTime(1970, 1, 1)).TotalSeconds
                }, new Dictionary<string, string>
                {
                    { "source", "csharp_system" },
                    { "event_source", evt.Source },
                    { "message", evt.Message.Length > 100 ? evt.Message.Substring(0, 100) + "..." : evt.Message }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending event log metrics: {ex.Message}");
        }
    }

    private async void SendDiskMetrics(DiskInfo info)
    {
        try
        {
            // Send overall disk metrics
            await PublishToMqtt("disk_overall", new
            {
                total_size_gb = info.TotalSize / (1024.0 * 1024.0 * 1024.0),
                used_space_gb = info.TotalUsedSpace / (1024.0 * 1024.0 * 1024.0),
                free_space_gb = info.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0),
                usage_percent = info.OverallUsagePercent
            });

            // Send individual drive metrics
            foreach (var drive in info.Drives)
            {
                await PublishToMqtt("disk_drives", new
                {
                    size_gb = drive.Size / (1024.0 * 1024.0 * 1024.0),
                    used_space_gb = drive.UsedSpace / (1024.0 * 1024.0 * 1024.0),
                    free_space_gb = drive.FreeSpace / (1024.0 * 1024.0 * 1024.0),
                    usage_percent = drive.UsagePercent
                }, new Dictionary<string, string>
                {
                    { "source", "csharp_system" },
                    { "drive_name", drive.Name },
                    { "volume_name", drive.VolumeName },
                    { "file_system", drive.FileSystem }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending disk metrics: {ex.Message}");
        }
    }

    private async void SendCpuSensorMetrics(CpuSensorInfo info)
    {
        try
        {
            var fields = new Dictionary<string, object?>();
            var tags = new Dictionary<string, string>
            {
                { "source", "csharp_system" },
                { "cpu_name", info.CpuName },
                { "cores", info.NumberOfCores.ToString() },
                { "logical_processors", info.NumberOfLogicalProcessors.ToString() }
            };

            if (IsEnabled(_metricsConfig.CpuMetrics, "cpu_temperature_celsius"))
                fields["cpu_temperature_celsius"] = info.CpuTemperature;
            if (IsEnabled(_metricsConfig.CpuMetrics, "cpu_fan_speed_rpm"))
                fields["cpu_fan_speed_rpm"] = info.FanSpeed;
            if (IsEnabled(_metricsConfig.CpuMetrics, "load_percentage"))
                fields["load_percentage"] = info.LoadPercentage;
            if (IsEnabled(_metricsConfig.CpuMetrics, "current_clock_mhz"))
                fields["current_clock_mhz"] = info.CurrentClockSpeed;

            if (fields.Count == 0 && _metricsConfig.CpuMetrics.Count > 0)
                return;

            if (fields.Count == 0)
            {
                fields["cpu_temperature_celsius"] = info.CpuTemperature;
                fields["cpu_fan_speed_rpm"] = info.FanSpeed;
                fields["load_percentage"] = info.LoadPercentage;
                fields["current_clock_mhz"] = info.CurrentClockSpeed;
            }

            await PublishToMqtt("cpu_sensors", fields, tags);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending CPU sensor metrics: {ex.Message}");
        }
    }

    private async void SendCupThermalMetrics(OperatingSystemInfo osInfo,
                                       SystemPerformanceInfo perfInfo,
                                       BatteryInfo batteryInfo,
                                       PowerInfo powerInfo,
                                       DiskInfo diskInfo,
                                       CpuSensorInfo cpuInfo)
    {
        try
        {
            await PublishToMqtt("cupthermal", new
            {
                cpu_temperature_celsius = cpuInfo.CpuTemperature,
                fan_speed_rpm = cpuInfo.FanSpeed,
                cpu_load_percentage = cpuInfo.LoadPercentage,
                current_clock_mhz = cpuInfo.CurrentClockSpeed,
                memory_usage_percent = perfInfo.MemoryUsagePercent,
                disk_usage_percent = diskInfo.OverallUsagePercent,
                disk_read_bytes_per_sec = perfInfo.DiskReadBytesPerSec,
                disk_write_bytes_per_sec = perfInfo.DiskWriteBytesPerSec,
                network_bytes_per_sec = perfInfo.NetworkBytesPerSec,
                battery_remaining_capacity_percent = batteryInfo.RemainingCapacity,
                battery_voltage_mv = batteryInfo.Voltage,
                battery_status = batteryInfo.Status,
                power_supply_total_watts = powerInfo.TotalPower,
                power_supply_is_switching = powerInfo.IsSwitchingSupply ? 1 : 0,
                total_disk_size_gb = diskInfo.TotalSize / (1024.0 * 1024.0 * 1024.0),
                total_disk_free_gb = diskInfo.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0),
                total_processes = GetProcessInformation().TotalProcesses
            }, new Dictionary<string, string>
            {
                { "source", "csharp_system" },
                { "os_version", osInfo.Version },
                { "os_build", osInfo.BuildNumber },
                { "cpu_name", cpuInfo.CpuName },
                { "os_architecture", osInfo.Architecture }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending cupthermal metrics: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// Comprehensive system monitoring - monitors all components
    /// </summary>
    public async void MonitorAll()
    {
        var osInfo = GetOperatingSystemInfo();
        var perfInfo = GetSystemPerformance();
        var processInfo = GetProcessInformation();
        var batteryInfo = GetBatteryInformation();
        var powerInfo = GetPowerInformation();
        var eventInfo = GetRecentSystemEvents();
        var diskInfo = GetDiskInformation();
        var cpuInfo = GetCpuSensorInformation();

        // Send OS and system metrics based on config
        if (HasAnySelection(_metricsConfig.OsMetrics))
            await Task.Run(() => SendOsMetrics(osInfo));

        if (HasAnySelection(_metricsConfig.SystemMetrics))
        {
            await Task.Run(() => SendSystemPerformanceMetrics(perfInfo));
            await Task.Run(() => SendProcessMetrics(processInfo));
        }

        if (HasAnySelection(_metricsConfig.CpuMetrics))
            await Task.Run(() => SendCpuSensorMetrics(cpuInfo));

        // Send combined measurement only when any configured category is enabled
        if (HasAnySelection(_metricsConfig.OsMetrics) || HasAnySelection(_metricsConfig.SystemMetrics) || HasAnySelection(_metricsConfig.CpuMetrics))
        {
            await Task.Run(() => SendSystemData(osInfo, perfInfo, processInfo, batteryInfo, powerInfo, eventInfo, diskInfo, cpuInfo));
        }
    }

    /// <summary>
    /// Helper method to publish data to MQTT
    /// </summary>
    private async Task PublishToMqtt(string measurement, object data, Dictionary<string, string>? tags = null)
    {
        try
        {
            var payload = new
            {
                measurement = measurement,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000,
                fields = data,
                tags = tags ?? new Dictionary<string, string> { { "source", "csharp_system" } }
            };

            var json = JsonSerializer.Serialize(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_mqttTopic)
                .WithPayload(json)
                .Build();

            await _mqttClient.PublishAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error publishing {measurement} to MQTT: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        if (_mqttClient != null)
        {
            _mqttClient.DisconnectAsync().Wait();
        }
    }
}