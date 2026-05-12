using System.Collections.Generic;

namespace CpuThermalTwinConfigurator.Models
{
    public class MetricsConfig
    {
        public Dictionary<string, bool> OsMetrics { get; set; } = new();
        public Dictionary<string, bool> SystemMetrics { get; set; } = new();
        public Dictionary<string, bool> CpuMetrics { get; set; } = new();
    }
}
