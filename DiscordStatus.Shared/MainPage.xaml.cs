using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Timers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using System.Text;
using System.Net.WebSockets;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DiscordStatus
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ClientWebSocket _client;
        private Timer _timer;
        private string _sessionId;

        public MainPage()
        {
            this.InitializeComponent();
            foreach (var e in Enum.GetValues(typeof(DiscordActivityType)))
            {
                ActivityTypeBox.Items.Add(e);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                Debug.WriteLine("Starting client...");
                _client?.Dispose();
                _timer?.Stop();
                _client = new ClientWebSocket();
                await _client.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=6&encoding=json"), CancellationToken.None);
                _ = WebSocketLoop();
                var token = AuthTokenBox.Text;
                var activityName = ActivityNameBox.Text;
                var isOnMobile = IsOnMobileCheck.IsChecked ?? false;
                var activityType = (DiscordActivityType)ActivityTypeBox.SelectedItem;
                var payload = new
                {
                    op = 2,
                    d = new
                    {
                        token = token,
                        intents = 513,
                        properties = new Dictionary<string, string>
                        {
                            // Only exists in your dreams...
                            { "$os",  "Windows 11" + (isOnMobile ? " Mobile" : string.Empty)},
                            // Windows 11 Mobile running Android apps!
                            { "$browser", (isOnMobile ? "Discord Android" : "Microsoft Edge") },
                            { "$device", "surfacephone" }
                        },
                        presence = new
                        {
                            activities = new object[]
                                {
                                new
                                {
                                    name = activityName,
                                    type = activityType,
                                }
                                },
                            status = "online",
                            afk = false
                        }
                    }
                };
                var data = JsonConvert.SerializeObject(payload);
                Debug.WriteLine("Sending payload");
                await _client.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs args)
        {
            var activityName = ActivityNameBox.Text;
            var activityType = (DiscordActivityType)ActivityTypeBox.SelectedItem;
            var payload = new
            {
                op = 3,
                d = new
                {
                    since = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    activities = new object[]
                    {
                        new
                        {
                            name = activityName,
                            type = (int)activityType,
                        }
                    },
                    status = "online",
                    afk = false
                }
            };
            var data = JsonConvert.SerializeObject(payload);
            Debug.WriteLine("Updating Presence");
            await _client.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task WebSocketLoop()
        {
            var buffer = new byte[1024 * 4];
            var memoryStream = new MemoryStream();

            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                memoryStream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var reader = new StreamReader(memoryStream);
                    var text = await reader.ReadToEndAsync();

                    MessageReceived(text);

                    reader.Dispose();
                    memoryStream.Dispose();
                    memoryStream = new MemoryStream();
                }
                result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await _client.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            memoryStream.Dispose();
        }

        private void MessageReceived(string message)
        {
            //Debug.WriteLine(message.Text);
            var payload = JsonConvert.DeserializeObject<JObject>(message);

            var op = payload.Value<int>("op");
            var t = payload.Value<string>("t");
            var d = payload["d"];

            switch (op)
            {
                case 10:
                    Debug.WriteLine(message);
                    var heartbeatInterval = d.Value<int>("heartbeat_interval");
                    Debug.WriteLine($"[Debug]: Channel connected to WebSocket, pinging every {heartbeatInterval} ms");
                    _timer?.Stop();
                    _timer = Heartbeat(heartbeatInterval);
                    break;
            }

            switch (t)
            {
                case "READY":
                {
                    _sessionId = d.Value<string>("session_id");
                    System.Diagnostics.Debug.WriteLine(_sessionId);
                }
                break;
            }
        }

        private Timer Heartbeat(int milliseconds)
        {
            var timer = new Timer(milliseconds);
            timer.Elapsed += async (sender, args) =>
            {
                Debug.WriteLine("Ping.");
                await _client.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { op = 1, d = (string)null })), WebSocketMessageType.Text, true, CancellationToken.None);
            };
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
            return timer;
        }
    }
}
