using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace DiscordStatus
{
	public sealed partial class CreateApplicationDialog : ContentDialog
	{
		private HttpClient _client = new HttpClient();
		private string _token;

		public DiscordApplication Application { get; private set; }

		public CreateApplicationDialog(string token)
		{
			_token = token;
			this.InitializeComponent();
		}

		private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
			var deferral = args.GetDeferral();
			
			try
            {
				var request = Helpers.CreateHttpRequestMessage(HttpMethod.Post, "https://discord.com/api/v9/applications");
				request.Headers.Authorization = AuthenticationHeaderValue.Parse(_token);

				request.Content = new StringContent(JsonConvert.SerializeObject(new { name = NameBox.Text, team_id = (string)null }), Encoding.UTF8, "application/json");

				var response = await _client.SendAsync(request);
				response.EnsureSuccessStatusCode();

				var json = await response.Content.ReadAsStringAsync();
				Application = JsonConvert.DeserializeObject<DiscordApplication>(json);
			}
			catch (Exception e)
            {
				var dialog = new MessageDialog($"Failed to create application: {e.GetType()} {e.Message}");
				await dialog.ShowAsync();
            }

			deferral.Complete();
		}

		private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
		}

        private async void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
			await Launcher.LaunchUriAsync(new Uri("https://discord.com/developers/docs/legal"));
		}
    }
}
