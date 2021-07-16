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
using System.Net.Http;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Popups;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DiscordStatus
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public bool _assetsEnabled = true;
        public bool AssetsEnabled 
        {
            get => _assetsEnabled;
            set
            {
                _assetsEnabled = value;
                Assets.IsEnabled = value;
            }
        }

        public ObservableCollection<Asset> AssetCollection { get; set; } = new ObservableCollection<Asset>();
        public ObservableCollection<DiscordApplication> ApplicationCollection { get; set; } = new ObservableCollection<DiscordApplication>();

        private ClientWebSocket _client;
        private Timer _timer;
        private string _sessionId;
        private int? _seq;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private HttpClient _httpClient = new HttpClient();

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
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _client = new ClientWebSocket();
                await _client.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=6&encoding=json"), CancellationToken.None);
                _cts = new CancellationTokenSource();
                _loopTask = WebSocketLoop(_cts.Token);
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
                                _assetsEnabled ?
                                (object)new
                                {
                                    name = activityName,
                                    type = (int)activityType,
                                    application_id = ApplicationIdBox.Text,
                                    assets = new
                                    {
                                        large_image = ApplicationImageBox.Text,
                                        large_text = activityName
                                    }
                                } :
                                new
                                {
                                    name = activityName,
                                    type = (int)activityType
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
                        _assetsEnabled ?
                        (object)new
                        {
                            name = activityName,
                            type = (int)activityType,
                            application_id = ApplicationIdBox.Text,
                            assets = new
                            {
                                large_image = ApplicationImageBox.Text,
                                large_text = activityName
                            }
                        } : 
                        new
                        {
                            name = activityName,
                            type = (int)activityType
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

        private async Task ReconnectAsync()
        {
            try
            {
                _client?.Dispose();
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                if (_loopTask != null)
                {
                    try
                    {
                        await _loopTask;
                    }
                    catch
                    {
                        // This is a dead task, swallow every exception here.
                    }
                }
                _client = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                await _client.ConnectAsync(new Uri("wss://gateway.discord.gg/?v=6&encoding=json"), CancellationToken.None);
                _loopTask = WebSocketLoop(_cts.Token);
                var token = AuthTokenBox.Text;
                var activityName = ActivityNameBox.Text;
                var isOnMobile = IsOnMobileCheck.IsChecked ?? false;
                var activityType = (DiscordActivityType)ActivityTypeBox.SelectedItem;
                var payload = new
                {
                    op = 6,
                    d = new
                    {
                        token = token,
                        session_id = _sessionId,
                        seq = _seq
                    }
                };
                var data = JsonConvert.SerializeObject(payload);
                Debug.WriteLine("Sending resume payload");
                await _client.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task WebSocketLoop(CancellationToken ct)
        {
            try
            {
                var buffer = new byte[1024 * 4];
                var memoryStream = new MemoryStream();

                var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
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
            catch (ObjectDisposedException e)
            {
                Debug.WriteLine(e);
            }
        }

        private void MessageReceived(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Debug.WriteLine(message);
            Console.WriteLine(message);
            var payload = JsonConvert.DeserializeObject<JObject>(message);

            var op = payload.Value<int?>("op");
            var t = payload.Value<string>("t");
            var d = payload["d"];
            var s = payload.Value<int?>("s");

            _seq = s ?? _seq;

            switch (op)
            {
                case 9:
                {
                    Console.WriteLine("Invalid session.");
                    StartButton_Click(null, null);
                }
                break;
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
                    Debug.WriteLine(_sessionId);
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
                Console.WriteLine("Ping.");
                try
                {
                    await _client.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { op = 1, d = _seq })), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Attempting to reconnect.");
                    await ReconnectAsync();
                }
            };
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
            return timer;
        }

        private async void SearchAssetsButton_Click(object sender, RoutedEventArgs args)
        {
            var client = new HttpClient();

            var id = ApplicationIdBox.Text;
            Debug.WriteLine($"https://discord.com/api/v9/oauth2/applications/{id}/assets");
            var message = Helpers.CreateHttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v9/oauth2/applications/{id}/assets");

            var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            Debug.WriteLine(json);

            var list = JsonConvert.DeserializeObject<List<Asset>>(json);
            AssetCollection.Clear();
            foreach (var l in list)
            {
                AssetCollection.Add(l);
            }
            foreach (var asset in AssetCollection)
            {
                Debug.WriteLine(asset);
            }
            SelectAssetBox.IsEnabled = true;
        }

        private void ApplicationIdBox_TextChanged(object sender, TextChangedEventArgs args)
        {
            SelectAssetBox.IsEnabled = false;
        }

        private void ApplicationImageBox_TextChanged(object sender, TextChangedEventArgs args)
        {
            var appId = ApplicationIdBox.Text;
            var imageId = ApplicationImageBox.Text;
            // Images are immune to CORS
            AssetPreviewImage.Source = new BitmapImage(
                new Uri($"https://cdn.discordapp.com/app-assets/{appId}/{imageId}.png?size=512"));
            AssetPreviewImage.Visibility = Visibility.Visible;
        }

        private void SelectAssetBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            var item = SelectAssetBox.SelectedItem as Asset;
            ApplicationImageBox.Text = item?.ID;
        }

        private async void SearchApplicationButton_Click(object sender, RoutedEventArgs args)
        {
            var message = Helpers.CreateHttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/applications?with_team_applications=true");
            var token = AuthTokenBox.Text;
            message.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(token);

            var response = await _httpClient.SendAsync(message);

            var json = await response.Content.ReadAsStringAsync();
            var arr = JsonConvert.DeserializeObject<List<DiscordApplication>>(json);

            ApplicationCollection.Clear();

            foreach (var app in arr)
            {
                ApplicationCollection.Add(app);
            }

            SelectApplicationBox.IsEnabled = true;
        }

        private void SelectApplicationBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ApplicationIdBox.Text = (SelectApplicationBox.SelectedItem as DiscordApplication)?.ID ?? string.Empty;
        }

        private async void CreateApplicationButton_Click(object sender, RoutedEventArgs args)
        {
            var dialog = new CreateApplicationDialog(AuthTokenBox.Text);
            await dialog.ShowAsync();

            var application = dialog.Application;
            if (application != null)
            {
                ApplicationCollection.Add(application);
                SelectApplicationBox.SelectedIndex = ApplicationCollection.Count - 1;
            }
        }

        private async void CreateAssetButton_Click(object sender, RoutedEventArgs args)
        {
            var dialog = new CreateAssetDialog(ApplicationIdBox.Text, AuthTokenBox.Text);
            await dialog.ShowAsync();

            var asset = dialog.Asset;
            if (asset != null)
            {
                AssetCollection.Add(asset);
                SelectAssetBox.SelectedIndex = AssetCollection.Count - 1;
            }
        }

        private async void DeleteApplicationButton_Click(object sender, RoutedEventArgs args)
        {
            var application = SelectApplicationBox.SelectedItem as DiscordApplication;
            if (application == null)
            {
                var dialog = new MessageDialog("Select an application first!");
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new DeleteApplicationDialog(application, AuthTokenBox.Text);
                await dialog.ShowAsync();
            }
            SearchApplicationButton_Click(null, null);
        }
    }
}
