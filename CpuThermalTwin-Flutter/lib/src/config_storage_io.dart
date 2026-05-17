import 'dart:io';
import 'package:path_provider/path_provider.dart';

Future<String?> readLocalConfig() async {
  final directory = await getApplicationDocumentsDirectory();
  final configFile = File('${directory.path}/metric-config.json');
  if (await configFile.exists()) {
    return configFile.readAsString();
  }
  return null;
}

Future<void> writeLocalConfig(String contents) async {
  final directory = await getApplicationDocumentsDirectory();
  final configFile = File('${directory.path}/metric-config.json');
  await configFile.writeAsString(contents);
}
