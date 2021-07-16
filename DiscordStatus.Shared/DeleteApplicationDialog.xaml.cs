using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
	public sealed partial class DeleteApplicationDialog : ContentDialog
	{
		private DiscordApplication _app;
		private string _token;
		private HttpClient _client = new HttpClient();

		public DeleteApplicationDialog(DiscordApplication application, string token)
		{
			_app = application;
			_token = token;
			this.InitializeComponent();

			InstructionBlock.Text = $"To delete this application, please confirm the name ({_app.Name}) below.";
			AppNameBox.PlaceholderText = _app.Name;
		}

		private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
			var deferral = args.GetDeferral();
			try
            {
				var request = Helpers.CreateHttpRequestMessage(HttpMethod.Post, $"https://discord.com/api/v9/applications/{_app.ID}/delete");
				request.Headers.Authorization = AuthenticationHeaderValue.Parse(_token);

				var response = await _client.SendAsync(request);
				response.EnsureSuccessStatusCode();
			}
			catch (Exception e)
            {
				var dialog = new MessageDialog($"Failed to delete app: {e.GetType()} {e.Message}");
				await dialog.ShowAsync();
			}

			deferral.Complete();
		}

		private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
		}

		private void AppNameBox_TextChanged(object sender, TextChangedEventArgs args)
        {
			var text = AppNameBox.Text;
			if (text == _app.Name)
            {
				PrimaryButtonText = "Delete";
            }
			else
            {
				PrimaryButtonText = string.Empty;
            }
        }
	}
}
