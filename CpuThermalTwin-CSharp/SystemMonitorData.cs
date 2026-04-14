using System;
using System.Collections.Generic;

namespace CpuThermalTwin;

/// <summary>
/// Operating System Information
/// </summary>
public class OperatingSystemInfo
{
    public string Caption { get; set; } = "";
    public string Version { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string LastBootUpTime { get; set; } = "";
    public ulong TotalVisibleMemorySize { get; set; }
    public ulong FreePhysicalMemory { get; set; }
    public ulong TotalVirtualMemorySize { get; set; }
    public ulong FreeVirtualMemory { get; set; }
}

/// <summary>
/// System Performance Information
/// </summary>
public class SystemPerformanceInfo
{
    public float CpuUsagePercent { get; set; }
    public float MemoryUsagePercent { get; set; }
    public float DiskReadBytesPerSec { get; set; }
    public float DiskWriteBytesPerSec { get; set; }
    public float NetworkBytesPerSec { get; set; }
}

/// <summary>
/// Process Information
/// </summary>
public class ProcessInfo
{
    public int TotalProcesses { get; set; }
    public int Threads { get; set; }
    public List<ProcessData> TopCpuProcesses { get; set; } = new();
}

/// <summary>
/// Individual Process Data
/// </summary>
public class ProcessData
{
    public string Name { get; set; } = "";
    public int Id { get; set; }
    public double CpuTime { get; set; }
    public long MemoryUsage { get; set; }
}

/// <summary>
/// Battery Information
/// </summary>
public class BatteryInfo
{
    public string Name { get; set; } = "";
    public ushort Status { get; set; } // 1=Discharging, 2=AC Power, etc.
    public uint RemainingCapacity { get; set; } // Percentage
    public uint FullChargeCapacity { get; set; }
    public uint Voltage { get; set; } // Millivolts
    public string Chemistry { get; set; } = "";
}

/// <summary>
/// Power Supply Information
/// </summary>
public class PowerInfo
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsSwitchingSupply { get; set; }
    public uint TotalPower { get; set; } // Watts
}

/// <summary>
/// Event Log Information
/// </summary>
public class EventLogInfo
{
    public int TotalEvents { get; set; }
    public int ErrorEvents { get; set; }
    public int WarningEvents { get; set; }
    public int InformationEvents { get; set; }
    public List<EventData> RecentCriticalEvents { get; set; } = new();
}

/// <summary>
/// Individual Event Data
/// </summary>
public class EventData
{
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime TimeGenerated { get; set; }
}

/// <summary>
/// Disk Information
/// </summary>
public class DiskInfo
{
    public List<DiskDrive> Drives { get; set; } = new();
    public ulong TotalSize { get; set; }
    public ulong TotalFreeSpace { get; set; }
    public ulong TotalUsedSpace { get; set; }
    public double OverallUsagePercent { get; set; }
}

/// <summary>
/// Individual Disk Drive Data
/// </summary>
public class DiskDrive
{
    public string Name { get; set; } = "";
    public string VolumeName { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public ulong Size { get; set; }
    public ulong FreeSpace { get; set; }
    public ulong UsedSpace { get; set; }
    public double UsagePercent { get; set; }
}

/// <summary>
/// CPU Sensor Information
/// </summary>
public class CpuSensorInfo
{
    public string CpuName { get; set; } = "";
    public int NumberOfCores { get; set; }
    public int NumberOfLogicalProcessors { get; set; }
    public uint MaxClockSpeed { get; set; } // MHz
    public uint CurrentClockSpeed { get; set; } // MHz
    public ushort LoadPercentage { get; set; }
    public double CpuTemperature { get; set; } // Celsius
    public uint FanSpeed { get; set; } // RPM
}