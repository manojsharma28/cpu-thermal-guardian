# CpuThermalTwin Flutter Configurator

A Flutter desktop/mobile app that mirrors the metric selection functionality from `CpuThermalTwinConfigurator.csproj`.

## Features
- Loads metric selections from `assets/metric-config.json`
- Shows OS, System, and CPU metric groups
- Uses expanded dropdown-style groups with checkbox selections
- Saves configuration to local storage
- Reloads default config from assets

## Run
1. Install Flutter: https://flutter.dev/docs/get-started/install
2. In `CpuThermalTwin-Flutter`, run:
   ```bash
   flutter pub get
   flutter run
   ```

## Notes
- The app uses `path_provider` to store the saved `metric-config.json` in the user documents directory.
- The initial default config file is included under `assets/metric-config.json`.
