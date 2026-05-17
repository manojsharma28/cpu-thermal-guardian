// ignore: avoid_web_libraries_in_flutter, deprecated_member_use
import 'dart:html' as html;

Future<String?> readLocalConfig() async {
  return html.window.localStorage['metric-config'];
}

Future<void> writeLocalConfig(String contents) async {
  html.window.localStorage['metric-config'] = contents;
}
