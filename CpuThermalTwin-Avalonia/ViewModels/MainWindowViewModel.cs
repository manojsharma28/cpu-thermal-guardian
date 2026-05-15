using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using CpuThermalTwinConfigurator.Models;
using CpuThermalTwinConfigurator.ViewModels;

namespace CpuThermalTwinConfigurator
{
    public class MainWindowViewModel : ViewModelBase
    {
        private const string ConfigFileName = "metric-config.json";
        private string _statusMessage = "Ready to configure metrics.";
        private bool _isOsMetricsOpen;
        private bool _isSystemMetricsOpen;
        private bool _isCpuMetricsOpen;

        public ObservableCollection<MetricOption> OsMetrics { get; } = new();
        public ObservableCollection<MetricOption> SystemMetrics { get; } = new();
        public ObservableCollection<MetricOption> CpuMetrics { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand ResetCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsOsMetricsOpen
        {
            get => _isOsMetricsOpen;
            set => SetProperty(ref _isOsMetricsOpen, value);
        }

        public bool IsSystemMetricsOpen
        {
            get => _isSystemMetricsOpen;
            set => SetProperty(ref _isSystemMetricsOpen, value);
        }

        public bool IsCpuMetricsOpen
        {
            get => _isCpuMetricsOpen;
            set => SetProperty(ref _isCpuMetricsOpen, value);
        }

        public string OsMetricsButtonText => $"OS Metrics ({OsMetrics.Count(m => m.IsSelected)}/{OsMetrics.Count})";
        public string SystemMetricsButtonText => $"System Metrics ({SystemMetrics.Count(m => m.IsSelected)}/{SystemMetrics.Count})";
        public string CpuMetricsButtonText => $"CPU Metrics ({CpuMetrics.Count(m => m.IsSelected)}/{CpuMetrics.Count})";

        public MainWindowViewModel()
        {
            InitializeMetricCollections();
            SaveCommand = new RelayCommand(_ => SaveConfig());
            LoadCommand = new RelayCommand(_ => LoadConfig());
            ResetCommand = new RelayCommand(_ => ResetDefaults());
        }

        private void InitializeMetricCollections()
        {
            try
            {
                var path = GetConfigPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<MetricsConfig>(json);

                    if (config != null)
                    {
                        LoadMetricNames(OsMetrics, config.OsMetrics.Keys);
                        LoadMetricNames(SystemMetrics, config.SystemMetrics.Keys);
                        LoadMetricNames(CpuMetrics, config.CpuMetrics.Keys);
                        WireUpPropertyChanges();
                        return;
                    }
                }

                // Fallback to default values if the file doesn't exist or is invalid
                LoadDefaultMetrics();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error initializing metrics: {ex.Message}";
                LoadDefaultMetrics();
            }
        }

        private void LoadMetricNames(ObservableCollection<MetricOption> collection, IEnumerable<string> names)
        {
            collection.Clear();
            foreach (var name in names)
            {
                collection.Add(new MetricOption { Name = name });
            }
        }

        private void LoadDefaultMetrics()
        {
            OsMetrics.Clear();
            SystemMetrics.Clear();
            CpuMetrics.Clear();

            OsMetrics.Add(new MetricOption { Name = "OS version" });
            OsMetrics.Add(new MetricOption { Name = "OS architecture" });
            OsMetrics.Add(new MetricOption { Name = "Boot uptime" });
            OsMetrics.Add(new MetricOption { Name = "OS build number" });
            OsMetrics.Add(new MetricOption { Name = "Installed culture" });

            SystemMetrics.Add(new MetricOption { Name = "CPU usage percent" });
            SystemMetrics.Add(new MetricOption { Name = "Memory usage percent" });
            SystemMetrics.Add(new MetricOption { Name = "Disk read bytes/sec" });
            SystemMetrics.Add(new MetricOption { Name = "Disk write bytes/sec" });
            SystemMetrics.Add(new MetricOption { Name = "Network bytes/sec" });
            SystemMetrics.Add(new MetricOption { Name = "Total process count" });
            SystemMetrics.Add(new MetricOption { Name = "Running process count" });
            SystemMetrics.Add(new MetricOption { Name = "System event count" });

            CpuMetrics.Add(new MetricOption { Name = "CPU temperature" });
            CpuMetrics.Add(new MetricOption { Name = "CPU fan speed" });
            CpuMetrics.Add(new MetricOption { Name = "CPU load percentage" });
            CpuMetrics.Add(new MetricOption { Name = "CPU clock speed MHz" });
            CpuMetrics.Add(new MetricOption { Name = "CPU core count" });

            WireUpPropertyChanges();
        }

        private void WireUpPropertyChanges()
        {
            foreach (var metric in OsMetrics.Concat(SystemMetrics).Concat(CpuMetrics))
            {
                metric.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MetricOption.IsSelected))
                    {
                        OnPropertyChanged(nameof(OsMetricsButtonText));
                        OnPropertyChanged(nameof(SystemMetricsButtonText));
                        OnPropertyChanged(nameof(CpuMetricsButtonText));
                    }
                };
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new MetricsConfig
                {
                    OsMetrics = OsMetrics.ToDictionary(option => option.Name, option => option.IsSelected),
                    SystemMetrics = SystemMetrics.ToDictionary(option => option.Name, option => option.IsSelected),
                    CpuMetrics = CpuMetrics.ToDictionary(option => option.Name, option => option.IsSelected)
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetConfigPath(), json);
                StatusMessage = $"Configuration saved to {GetConfigPath()}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving configuration: {ex.Message}";
            }
        }

        private void LoadConfig()
        {
            try
            {
                var path = GetConfigPath();
                if (!File.Exists(path))
                {
                    StatusMessage = "No saved configuration found.";
                    return;
                }

                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<MetricsConfig>(json);
                if (config == null)
                {
                    StatusMessage = "Unable to deserialize configuration file.";
                    return;
                }

                LoadMetricCollection(OsMetrics, config.OsMetrics);
                LoadMetricCollection(SystemMetrics, config.SystemMetrics);
                LoadMetricCollection(CpuMetrics, config.CpuMetrics);
                StatusMessage = $"Configuration loaded from {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading configuration: {ex.Message}";
            }
        }

        private void LoadMetricCollection(ObservableCollection<MetricOption> collection, Dictionary<string, bool>? saved)
        {
            if (saved == null) return;
            foreach (var option in collection)
            {
                if (saved.TryGetValue(option.Name, out var isSelected))
                {
                    option.IsSelected = isSelected;
                }
            }
        }

        private void ResetDefaults()
        {
            foreach (var metric in OsMetrics.Concat(SystemMetrics).Concat(CpuMetrics))
            {
                metric.IsSelected = false;
            }

            StatusMessage = "Reset configuration to defaults.";
        }

        private static string GetConfigPath()
        {
            var exeFolder = AppContext.BaseDirectory;
            return Path.Combine(exeFolder, ConfigFileName);
        }
    }
}
