using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Newtonsoft.Json.Linq;
using System.Buffers;
using MQTTnet.Client.Receiving;
using MQTTnet.Client.Disconnecting;
using System.Linq;



namespace MqttToInfluxDbBridge
{
    class Program
    {
        // Configuration
        private const string MQTT_BROKER = "localhost";
        private const int MQTT_PORT = 1883;
        private static readonly string[] MQTT_TOPICS = { "cpu/thermal/data", "cpu/system/data" };

        private const string INFLUX_URL = "http://localhost:8181";
        private const string INFLUX_TOKEN = "apiv3_MA-RAN1uKlertkeKOgYblq5-wSpiEp5jlT0FaDahNAxAFoeJ94IaoMg0i1gmRGRw9SJnNeYkf4UnnaorVVvFEQ";
        // Replace 'const' with 'readonly' for INFLUX_ORG to allow runtime initialization
        private static readonly string INFLUX_ORG = "Org";
        private const string INFLUX_BUCKET = "cpu_twin";

        private static IMqttClient? _mqttClient;
        private static InfluxDBClient? _influxClient;
        private static CancellationTokenSource? _cts;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔═══════════════════════════════════════════╗");
            Console.WriteLine("║   MQTT to InfluxDB Bridge - C# Edition    ║");
            Console.WriteLine("║   Digital Twin Data Pipeline              ║");
            Console.WriteLine("╚═══════════════════════════════════════════╝\n");

            _cts = new CancellationTokenSource();

            try
            {
                // Initialize InfluxDB client
                _influxClient = InfluxDBClientFactory.Create(INFLUX_URL, INFLUX_TOKEN.ToCharArray());
                var writeApi = _influxClient.GetWriteApi();

                // Initialize MQTT client
                var factory = new MqttFactory();
              
                // Add this using directive for MQTTnet.Client namespace

                _mqttClient = factory.CreateMqttClient();

                // Set up MQTT event handlers
                _mqttClient.ConnectedHandler    = new MqttClientConnectedHandlerDelegate(async e=> await OnConnected(e));
                _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(async e =>
                    await OnDisconnected(e));   
                _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(async e =>
                    await OnMessageReceived(e, writeApi));
                // Connect to MQTT broker
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                    .WithCleanSession()
                    .Build();

                await _mqttClient.ConnectAsync(options, _cts.Token);

                Console.WriteLine("🚀 Bridge started successfully!");
                Console.WriteLine("Press Ctrl+C to stop.\n");

                // Setup graceful shutdown
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _cts.Cancel();
                };

                // Keep the application running
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n🛑 Bridge shutdown requested.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private static async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            Console.WriteLine($"✅ Connected to MQTT broker: {MQTT_BROKER}:{MQTT_PORT}");

            // Subscribe to topics
            foreach (var topic in MQTT_TOPICS)
            {
                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .Build();

                await _mqttClient!.SubscribeAsync(topicFilter);
                Console.WriteLine($"📡 Subscribed to topic: {topic}");
            }
        }

        private static async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine($"❌ Disconnected from MQTT broker: {e.Reason}");

            if (!_cts!.IsCancellationRequested)
            {
                Console.WriteLine("🔄 Attempting to reconnect...");
                await Task.Delay(5000, _cts.Token);

                try
                {
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                        .WithCleanSession()
                        .Build();

                    await _mqttClient!.ConnectAsync(options, _cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Reconnection failed: {ex.Message}");
                }
            }
        }

        private static async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e, WriteApi writeApi)
        {
            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());
                var jsonData = JObject.Parse(payload);

                // Extract data from JSON payload
                var measurement = jsonData["measurement"]?.ToString();
                var timestamp = jsonData["timestamp"]?.Value<long>();
                var fields = jsonData["fields"] as JObject;
                var tags = jsonData["tags"] as JObject;

                if (string.IsNullOrEmpty(measurement))
                {
                    Console.WriteLine("⚠️  Skipping message: missing measurement");
                    return;
                }

                // Create InfluxDB point
                var point = PointData.Measurement(measurement);

                // Add tags
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        point = point.Tag(tag.Key, tag.Value?.ToString() ?? "");
                    }
                }

                // Add fields
                if (fields != null)
                {
                    foreach (var field in fields)
                    {
                        var value = field.Value;
                        if (value?.Type == JTokenType.Integer)
                            point = point.Field(field.Key, value.Value<long>());
                        else if (value?.Type == JTokenType.Float)
                            point = point.Field(field.Key, value.Value<double>());
                        else if (value?.Type == JTokenType.Boolean)
                            point = point.Field(field.Key, value.Value<bool>());
                        else
                            point = point.Field(field.Key, value?.ToString() ?? "");
                    }
                }

                // Set timestamp if provided
                if (timestamp.HasValue)
                {
                    point = point.Timestamp(timestamp.Value, WritePrecision.Ns);
                }

                // Write to InfluxDB

                // Replace the incorrect WriteRecordAsync call with the synchronous WriteRecord method
                writeApi.WriteRecord(point.ToLineProtocol(), WritePrecision.Ns, INFLUX_BUCKET, INFLUX_ORG);

                Console.WriteLine($"📊 Written {measurement} data to InfluxDB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing message: {ex.Message}");
            }
        }

        private static async Task CleanupAsync()
        {
            Console.WriteLine("\n🧹 Cleaning up...");

            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                Console.WriteLine("✅ MQTT client disconnected");
            }

            _influxClient?.Dispose();
            Console.WriteLine("✅ InfluxDB client disposed");
            Console.WriteLine("✓ Cleanup complete. Goodbye!");
        }

        private static async Task CleanInfluxDbData()
        {
            try
            {
                if (_influxClient == null)
                {
                    Console.WriteLine("❌ InfluxDB client is not initialized.");
                    return;
                }

                var deleteApi = _influxClient.GetDeleteApi();

                // Define the time range for deletion (e.g., delete all data)
                var start = DateTime.MinValue; // Start of time
                var stop = DateTime.UtcNow;   // Current time

                // Delete data from the bucket
                //await deleteApi.DeleteAsync(start, stop, "", INFLUX_BUCKET, INFLUX_ORG);

                Console.WriteLine("🧹 InfluxDB data cleaned successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cleaning InfluxDB data: {ex.Message}");
            }
        }
    }
   
}