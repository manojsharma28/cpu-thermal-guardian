import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'src/config_storage_io.dart'
    if (dart.library.html) 'src/config_storage_stub.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const CpuThermalTwinConfiguratorApp());
}

class CpuThermalTwinConfiguratorApp extends StatelessWidget {
  const CpuThermalTwinConfiguratorApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'CPU Thermal Twin Configurator',
      theme: ThemeData(
        primarySwatch: Colors.blue,
      ),
      home: const ConfiguratorPage(),
    );
  }
}

class MetricOption {
  final String key;
  final String label;
  bool selected;

  MetricOption({
    required this.key,
    required this.label,
    this.selected = false,
  });
}

class MetricsConfig {
  final Map<String, bool> osMetrics;
  final Map<String, bool> systemMetrics;
  final Map<String, bool> cpuMetrics;

  MetricsConfig({
    required this.osMetrics,
    required this.systemMetrics,
    required this.cpuMetrics,
  });

  factory MetricsConfig.fromJson(Map<String, dynamic> json) {
    return MetricsConfig(
      osMetrics: Map<String, bool>.from(json['OsMetrics'] as Map? ?? {}),
      systemMetrics: Map<String, bool>.from(json['SystemMetrics'] as Map? ?? {}),
      cpuMetrics: Map<String, bool>.from(json['CpuMetrics'] as Map? ?? {}),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'OsMetrics': osMetrics,
      'SystemMetrics': systemMetrics,
      'CpuMetrics': cpuMetrics,
    };
  }
}

class ConfiguratorPage extends StatefulWidget {
  const ConfiguratorPage({super.key});

  @override
  State<ConfiguratorPage> createState() => _ConfiguratorPageState();
}

class _ConfiguratorPageState extends State<ConfiguratorPage> {
  final List<MetricOption> _osMetrics = [
    MetricOption(key: 'os_version', label: 'OS Version'),
    MetricOption(key: 'os_architecture', label: 'OS Architecture'),
    MetricOption(key: 'boot_uptime_seconds', label: 'Boot Uptime'),
    MetricOption(key: 'os_build', label: 'OS Build Number'),
    MetricOption(key: 'installed_culture', label: 'Installed Culture'),
  ];

  final List<MetricOption> _systemMetrics = [
    MetricOption(key: 'cpu_usage_percent', label: 'CPU Usage Percent'),
    MetricOption(key: 'memory_usage_percent', label: 'Memory Usage Percent'),
    MetricOption(key: 'disk_read_bytes_per_sec', label: 'Disk Read Bytes/sec'),
    MetricOption(key: 'disk_write_bytes_per_sec', label: 'Disk Write Bytes/sec'),
    MetricOption(key: 'network_bytes_per_sec', label: 'Network Bytes/sec'),
    MetricOption(key: 'total_processes', label: 'Total Process Count'),
    MetricOption(key: 'total_threads', label: 'Total Threads'),
    MetricOption(key: 'system_events_last_hour', label: 'System Event Count'),
  ];

  final List<MetricOption> _cpuMetrics = [
    MetricOption(key: 'cpu_temperature_celsius', label: 'CPU Temperature'),
    MetricOption(key: 'cpu_fan_speed_rpm', label: 'CPU Fan Speed'),
    MetricOption(key: 'load_percentage', label: 'CPU Load Percentage'),
    MetricOption(key: 'current_clock_mhz', label: 'CPU Clock Speed MHz'),
    MetricOption(key: 'number_of_cores', label: 'CPU Core Count'),
  ];

  String _statusMessage = 'Loading configuration...';
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadConfiguration();
  }

  Future<void> _loadConfiguration() async {
    try {
      final localContents = await readLocalConfig();
      if (localContents != null) {
        final data = jsonDecode(localContents) as Map<String, dynamic>;
        _applyConfig(MetricsConfig.fromJson(data));
        _statusMessage = 'Configuration loaded from local storage.';
      } else {
        final assetData = await rootBundle.loadString('assets/metric-config.json');
        final data = jsonDecode(assetData) as Map<String, dynamic>;
        _applyConfig(MetricsConfig.fromJson(data));
        _statusMessage = 'Loaded default configuration from asset.';
      }
    } catch (exception) {
      _statusMessage = 'Failed to load configuration: $exception';
    }

    setState(() {
      _isLoading = false;
    });
  }

  void _applyConfig(MetricsConfig config) {
    for (final metric in _osMetrics) {
      metric.selected = config.osMetrics[metric.key] ?? false;
    }
    for (final metric in _systemMetrics) {
      metric.selected = config.systemMetrics[metric.key] ?? false;
    }
    for (final metric in _cpuMetrics) {
      metric.selected = config.cpuMetrics[metric.key] ?? false;
    }
  }

  Future<void> _saveConfiguration() async {
    try {
      final config = MetricsConfig(
        osMetrics: {for (var metric in _osMetrics) metric.key: metric.selected},
        systemMetrics: {for (var metric in _systemMetrics) metric.key: metric.selected},
        cpuMetrics: {for (var metric in _cpuMetrics) metric.key: metric.selected},
      );
      final contents = const JsonEncoder.withIndent('  ').convert(config.toJson());
      await writeLocalConfig(contents);
      setState(() {
        _statusMessage = 'Configuration saved to local storage.';
      });
    } catch (exception) {
      setState(() {
        _statusMessage = 'Failed to save configuration: $exception';
      });
    }
  }

  Future<void> _resetSelection() async {
    setState(() {
      for (final metric in _osMetrics) {
        metric.selected = false;
      }
      for (final metric in _systemMetrics) {
        metric.selected = false;
      }
      for (final metric in _cpuMetrics) {
        metric.selected = false;
      }
      _statusMessage = 'All metric selections reset.';
    });
  }

  Future<void> _reloadFromAsset() async {
    try {
      final assetData = await rootBundle.loadString('assets/metric-config.json');
      final data = jsonDecode(assetData) as Map<String, dynamic>;
      setState(() {
        _applyConfig(MetricsConfig.fromJson(data));
        _statusMessage = 'Default configuration reloaded from asset.';
      });
    } catch (exception) {
      setState(() {
        _statusMessage = 'Failed to reload asset config: $exception';
      });
    }
  }

  Widget _buildMetricGroup(String title, List<MetricOption> metrics) {
    final selectedCount = metrics.where((metric) => metric.selected).length;
    return Card(
      child: ExpansionTile(
        title: Text(title),
        subtitle: Text('$selectedCount selected'),
        children: metrics
            .map((metric) => CheckboxListTile(
                  title: Text(metric.label),
                  value: metric.selected,
                  onChanged: (value) {
                    setState(() {
                      metric.selected = value ?? false;
                    });
                  },
                ))
            .toList(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('CPU Thermal Twin Configurator'),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                    'Select the OS, System, and CPU metrics to enable for the monitor.',
                    style: TextStyle(fontSize: 16),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      ElevatedButton(
                        onPressed: _saveConfiguration,
                        child: const Text('Save Configuration'),
                      ),
                      const SizedBox(width: 8),
                      ElevatedButton(
                        onPressed: _loadConfiguration,
                        child: const Text('Load Configuration'),
                      ),
                      const SizedBox(width: 8),
                      ElevatedButton(
                        onPressed: _resetSelection,
                        child: const Text('Reset Selections'),
                      ),
                      const SizedBox(width: 8),
                      ElevatedButton(
                        onPressed: _reloadFromAsset,
                        child: const Text('Reload Defaults'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Expanded(
                    child: ListView(
                      children: [
                        _buildMetricGroup('OS Metrics', _osMetrics),
                        _buildMetricGroup('System Metrics', _systemMetrics),
                        _buildMetricGroup('CPU Metrics', _cpuMetrics),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _statusMessage,
                    style: const TextStyle(fontSize: 14),
                  ),
                ],
              ),
            ),
    );
  }
}
