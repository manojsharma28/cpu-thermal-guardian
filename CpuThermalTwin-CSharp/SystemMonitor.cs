using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace CpuThermalTwin;

/// <summary>
/// Comprehensive system monitoring class using WMI queries
/// Monitors OS, Power Supply, Log Events, Disk, and CPU sensors
/// </summary>
[SupportedOSPlatform("windows")]
public class SystemMonitor
{
    private readonly string _influxUrl;
    private readonly string _bucket;
    private readonly string _organization;
    private InfluxDBClient? _client;
    private WriteApi? _writeApi;

    public SystemMonitor(string influxUrl = "http://localhost:8181",
                        string bucket = "system_monitor",
                        string organization = "my-org")
    {
        _influxUrl = influxUrl;
        _bucket = bucket;
        _organization = organization;
    }

    /// <summary>
    /// Initialize InfluxDB client
    /// </summary>
    public void Initialize()
    {
        try
        {
            var builder = InfluxDBClientFactory.Create(_influxUrl);
            _client = builder;
            _writeApi = _client.GetWriteApi();
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
    public void MonitorOperatingSystem()
    {
        try
        {
            // Get OS information
            var osInfo = GetOperatingSystemInfo();
            SendOsMetrics(osInfo);

            // Get system performance
            var perfInfo = GetSystemPerformance();
            SendSystemPerformanceMetrics(perfInfo);

            // Get process information
            var processInfo = GetProcessInformation();
            SendProcessMetrics(processInfo);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error monitoring OS: {ex.Message}");
        }
    }

    private void SendProcessMetrics(ProcessInfo processInfo)
    {
        try
        {
            var point = PointData.Measurement("process_info")
                .Field("total_processes", processInfo.TotalProcesses)
                .Field("total_threads", processInfo.Threads);

            _writeApi?.WritePoint(_bucket, _organization, point);

            // Send top CPU consuming processes as separate points
            foreach (var process in processInfo.TopCpuProcesses)
            {
                var processPoint = PointData.Measurement("top_processes")
                    .Field("cpu_time_ms", process.CpuTime)
                    .Field("memory_usage_bytes", process.MemoryUsage)
                    .Tag("process_name", process.Name)
                    .Tag("process_id", process.Id.ToString());

                _writeApi?.WritePoint(_bucket, _organization, processPoint);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending process metrics: {ex.Message}");
        }
    }

    private void SendSystemPerformanceMetrics(SystemPerformanceInfo perfInfo)
    {
        try
        {
            var point = PointData.Measurement("system_performance")
                .Field("cpu_usage_percent", perfInfo.CpuUsagePercent)
                .Field("memory_usage_percent", perfInfo.MemoryUsagePercent)
                .Field("disk_read_bytes_per_sec", perfInfo.DiskReadBytesPerSec)
                .Field("disk_write_bytes_per_sec", perfInfo.DiskWriteBytesPerSec)
                .Field("network_bytes_per_sec", perfInfo.NetworkBytesPerSec);

            _writeApi?.WritePoint(_bucket, _organization, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending system performance metrics: {ex.Message}");
        }
    }

    private void SendOsMetrics(OperatingSystemInfo osInfo)
    {
        try
        {
            var point = PointData.Measurement("os_info")
                .Field("total_memory_mb", osInfo.TotalVisibleMemorySize / 1024.0)
                .Field("free_memory_mb", osInfo.FreePhysicalMemory / 1024.0)
                .Field("memory_usage_percent", (osInfo.TotalVisibleMemorySize - osInfo.FreePhysicalMemory) * 100.0 / osInfo.TotalVisibleMemorySize)
                .Field("total_virtual_memory_mb", osInfo.TotalVirtualMemorySize / 1024.0)
                .Field("free_virtual_memory_mb", osInfo.FreeVirtualMemory / 1024.0)
                .Tag("os_version", osInfo.Version)
                .Tag("os_build", osInfo.BuildNumber)
                .Tag("os_architecture", osInfo.Architecture);

            _writeApi?.WritePoint(_bucket, _organization, point);
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

    #endregion

    #region InfluxDB Sending Methods

    /// <summary>
    /// Sends comprehensive system data to InfluxDB in a single measurement (following ThermalMonitor pattern)
    /// </summary>
    private void SendSystemData(OperatingSystemInfo osInfo,
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

            var point = PointData.Measurement("system_monitor")
                // OS Information
                .Field("os_uptime_seconds", uptime.TotalSeconds)
                .Field("os_total_memory_gb", totalMemoryGB)
                .Field("os_available_memory_gb", availableMemoryGB)
                .Field("os_memory_usage_percent", memoryUsagePercent)

                // System Performance
                .Field("cpu_load_percent", perfInfo.CpuUsagePercent)
                .Field("memory_usage_percent", perfInfo.MemoryUsagePercent)
                .Field("disk_read_bytes_per_sec", perfInfo.DiskReadBytesPerSec)
                .Field("disk_write_bytes_per_sec", perfInfo.DiskWriteBytesPerSec)
                .Field("network_bytes_per_sec", perfInfo.NetworkBytesPerSec)

                // Process Information
                .Field("total_processes", processInfo.TotalProcesses)
                .Field("running_processes", processInfo.TotalProcesses) // Using total as running
                .Field("threads_count", processInfo.Threads)

                // Battery Information
                .Field("battery_remaining_capacity_percent", batteryInfo.RemainingCapacity)
                .Field("battery_voltage_mv", batteryInfo.Voltage)
                .Field("battery_status", batteryInfo.Status)

                // Power Information
                .Field("power_supply_total_watts", powerInfo.TotalPower)
                .Field("power_supply_is_switching", powerInfo.IsSwitchingSupply ? 1 : 0)

                // Event Log Information
                .Field("system_events_last_hour", eventInfo.TotalEvents)
                .Field("error_events_last_hour", eventInfo.ErrorEvents)
                .Field("warning_events_last_hour", eventInfo.WarningEvents)

                // Disk Information
                .Field("disk_total_size_gb", diskInfo.TotalSize / (1024.0 * 1024.0 * 1024.0))
                .Field("disk_total_free_gb", diskInfo.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0))
                .Field("disk_overall_usage_percent", diskInfo.OverallUsagePercent)

                // CPU Sensor Information
                .Field("cpu_temperature_celsius", cpuInfo.CpuTemperature)
                .Field("cpu_fan_speed_rpm", cpuInfo.FanSpeed)
                .Field("cpu_load_percentage", cpuInfo.LoadPercentage)
                .Field("cpu_current_clock_mhz", cpuInfo.CurrentClockSpeed)

                // Tags for categorization
                .Tag("os_version", osInfo.Version)
                .Tag("os_build", osInfo.BuildNumber)
                .Tag("os_architecture", osInfo.Architecture)
                .Tag("cpu_name", cpuInfo.CpuName)
                .Tag("battery_status", batteryInfo.Status.ToString())
                .Tag("power_supply_type", powerInfo.IsSwitchingSupply ? "switching" : "linear");

            _writeApi?.WritePoint(_bucket, _organization, point);

            // Also send the cupthermal metrics for backward compatibility
            SendCupThermalMetrics(osInfo, perfInfo, batteryInfo, powerInfo, diskInfo, cpuInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending system data: {ex.Message}");
        }
    }

    private void SendBatteryMetrics(BatteryInfo info)
    {
        try
        {
            var point = PointData.Measurement("battery_info")
                .Field("remaining_capacity_percent", info.RemainingCapacity)
                .Field("voltage_mv", info.Voltage)
                .Field("status", info.Status)
                .Tag("battery_name", info.Name)
                .Tag("chemistry", info.Chemistry)
                .Tag("battery_status", info.Status.ToString());

            _writeApi?.WritePoint(_bucket, _organization, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending battery metrics: {ex.Message}");
        }
    }

    private void SendPowerMetrics(PowerInfo info)
    {
        try
        {
            var point = PointData.Measurement("power_info")
                .Field("total_power_watts", info.TotalPower)
                .Field("is_switching_supply", info.IsSwitchingSupply ? 1 : 0)
                .Tag("power_supply_name", info.Name)
                .Tag("status", info.Status)
                .Tag("power_supply_type", info.IsSwitchingSupply ? "switching" : "linear");

            _writeApi?.WritePoint(_bucket, _organization, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending power metrics: {ex.Message}");
        }
    }

    private void SendEventLogMetrics(EventLogInfo info)
    {
        try
        {
            var point = PointData.Measurement("event_logs")
                .Field("total_events", info.TotalEvents)
                .Field("error_events", info.ErrorEvents)
                .Field("warning_events", info.WarningEvents)
                .Field("information_events", info.InformationEvents);

            _writeApi?.WritePoint(_bucket, _organization, point);

            // Send critical events
            foreach (var evt in info.RecentCriticalEvents)
            {
                var eventPoint = PointData.Measurement("critical_events")
                    .Field("event_time", (long)(evt.TimeGenerated - new DateTime(1970, 1, 1)).TotalSeconds)
                    .Tag("source", evt.Source)
                    .Tag("message", evt.Message.Length > 100 ? evt.Message.Substring(0, 100) + "..." : evt.Message);

                _writeApi?.WritePoint(_bucket, _organization, eventPoint);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending event log metrics: {ex.Message}");
        }
    }

    private void SendDiskMetrics(DiskInfo info)
    {
        try
        {
            // Send overall disk metrics
            var point = PointData.Measurement("disk_overall")
                .Field("total_size_gb", info.TotalSize / (1024.0 * 1024.0 * 1024.0))
                .Field("used_space_gb", info.TotalUsedSpace / (1024.0 * 1024.0 * 1024.0))
                .Field("free_space_gb", info.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0))
                .Field("usage_percent", info.OverallUsagePercent);

            _writeApi?.WritePoint(_bucket, _organization, point);

            // Send individual drive metrics
            foreach (var drive in info.Drives)
            {
                var drivePoint = PointData.Measurement("disk_drives")
                    .Field("size_gb", drive.Size / (1024.0 * 1024.0 * 1024.0))
                    .Field("used_space_gb", drive.UsedSpace / (1024.0 * 1024.0 * 1024.0))
                    .Field("free_space_gb", drive.FreeSpace / (1024.0 * 1024.0 * 1024.0))
                    .Field("usage_percent", drive.UsagePercent)
                    .Tag("drive_name", drive.Name)
                    .Tag("volume_name", drive.VolumeName)
                    .Tag("file_system", drive.FileSystem);

                _writeApi?.WritePoint(_bucket, _organization, drivePoint);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending disk metrics: {ex.Message}");
        }
    }

    private void SendCpuSensorMetrics(CpuSensorInfo info)
    {
        try
        {
            var point = PointData.Measurement("cpu_sensors")
                .Field("temperature_celsius", info.CpuTemperature)
                .Field("fan_speed_rpm", info.FanSpeed)
                .Field("load_percentage", info.LoadPercentage)
                .Field("current_clock_mhz", info.CurrentClockSpeed)
                .Tag("cpu_name", info.CpuName)
                .Tag("cores", info.NumberOfCores.ToString())
                .Tag("logical_processors", info.NumberOfLogicalProcessors.ToString());

            _writeApi?.WritePoint(_bucket, _organization, point);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error sending CPU sensor metrics: {ex.Message}");
        }
    }

    private void SendCupThermalMetrics(OperatingSystemInfo osInfo,
                                       SystemPerformanceInfo perfInfo,
                                       BatteryInfo batteryInfo,
                                       PowerInfo powerInfo,
                                       DiskInfo diskInfo,
                                       CpuSensorInfo cpuInfo)
    {
        try
        {
            var point = PointData.Measurement("cupthermal")
                .Field("cpu_temperature_celsius", cpuInfo.CpuTemperature)
                .Field("fan_speed_rpm", cpuInfo.FanSpeed)
                .Field("cpu_load_percentage", cpuInfo.LoadPercentage)
                .Field("current_clock_mhz", cpuInfo.CurrentClockSpeed)
                .Field("memory_usage_percent", perfInfo.MemoryUsagePercent)
                .Field("disk_usage_percent", diskInfo.OverallUsagePercent)
                .Field("disk_read_bytes_per_sec", perfInfo.DiskReadBytesPerSec)
                .Field("disk_write_bytes_per_sec", perfInfo.DiskWriteBytesPerSec)
                .Field("network_bytes_per_sec", perfInfo.NetworkBytesPerSec)
                .Field("battery_remaining_capacity_percent", batteryInfo.RemainingCapacity)
                .Field("battery_voltage_mv", batteryInfo.Voltage)
                .Field("battery_status", batteryInfo.Status)
                .Field("power_supply_total_watts", powerInfo.TotalPower)
                .Field("power_supply_is_switching", powerInfo.IsSwitchingSupply ? 1 : 0)
                .Field("total_disk_size_gb", diskInfo.TotalSize / (1024.0 * 1024.0 * 1024.0))
                .Field("total_disk_free_gb", diskInfo.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0))
                .Field("total_processes", GetProcessInformation().TotalProcesses)
                .Tag("os_version", osInfo.Version)
                .Tag("os_build", osInfo.BuildNumber)
                .Tag("cpu_name", cpuInfo.CpuName)
                .Tag("os_architecture", osInfo.Architecture);

            _writeApi?.WritePoint(_bucket, _organization, point);
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
    public void MonitorAll()
    {
        var osInfo = GetOperatingSystemInfo();
        var perfInfo = GetSystemPerformance();
        var processInfo = GetProcessInformation();
        var batteryInfo = GetBatteryInformation();
        var powerInfo = GetPowerInformation();
        var eventInfo = GetRecentSystemEvents();
        var diskInfo = GetDiskInformation();
        var cpuInfo = GetCpuSensorInformation();

        // Send all individual measurements for granular access
        SendOsMetrics(osInfo);
        SendSystemPerformanceMetrics(perfInfo);
        SendProcessMetrics(processInfo);
        SendBatteryMetrics(batteryInfo);
        SendPowerMetrics(powerInfo);
        SendEventLogMetrics(eventInfo);
        SendDiskMetrics(diskInfo);
        SendCpuSensorMetrics(cpuInfo);

        // Also send comprehensive combined data
        SendSystemData(osInfo, perfInfo, processInfo, batteryInfo, powerInfo, eventInfo, diskInfo, cpuInfo);
    }

    /// <summary>
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}