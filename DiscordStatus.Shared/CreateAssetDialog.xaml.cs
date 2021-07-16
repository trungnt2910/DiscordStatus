using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace DiscordStatus
{
	public sealed partial class CreateAssetDialog : ContentDialog, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public Asset Asset { get; private set; }

		private string _assetName;
		public string AssetName 
		{
			get => _assetName;
			set
			{
				_assetName = value;
				PropertyChanged(this, new PropertyChangedEventArgs("AssetName"));
			}
		}

		private string _token;
		private string _applicationId;
		private byte[] _data;
		private HttpClient _client = new HttpClient();
		private string _mime;

		public CreateAssetDialog(string applicationId, string token)
		{
			_applicationId = applicationId;
			_token = token;
			this.InitializeComponent();
		}

		private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
			var deferral = args.GetDeferral();

			try
            {
				var message = Helpers.CreateHttpRequestMessage(HttpMethod.Post,
					$"https://discord.com/api/v9/oauth2/applications/{_applicationId}/assets");

				message.Headers.Authorization = AuthenticationHeaderValue.Parse(_token);

				var payload = new
				{
					name = _assetName,
					image = $"data:{_mime};base64,{Convert.ToBase64String(_data)}",
					type = 1
				};

				message.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

				var response = await _client.SendAsync(message);
				response.EnsureSuccessStatusCode();

				var json = await response.Content.ReadAsStringAsync();
				Asset = JsonConvert.DeserializeObject<Asset>(json);
			}
			catch (Exception e)
			{
				var dialog = new MessageDialog($"Failed to upload asset: {e.GetType()} {e.Message}");
				await dialog.ShowAsync();
			}

			deferral.Complete();
		}

		private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
		{
		}

		private async void UploadButton_Click(object sender, RoutedEventArgs args)
        {
			var picker = new Windows.Storage.Pickers.FileOpenPicker();
			picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
			picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
			picker.FileTypeFilter.Add(".jpg");
			picker.FileTypeFilter.Add(".jpeg");
			picker.FileTypeFilter.Add(".png");

			Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
			if (file != null)
			{
				var fileName = file.Name;
				fileName = Regex.Replace(fileName, @"[^\u0000-\u007F]+", "_");
				fileName.Replace(' ', '_');
				var ext = Path.GetExtension(fileName);
				fileName = Path.GetFileNameWithoutExtension(fileName);

                switch (ext)
                {
					case "jpg":
                    case "jpeg":
						_mime = "image/jpeg";
					break;
					case "png":
						_mime = "image/png";
					break;
				}

				AssetName = fileName;

				var stream = await file.OpenStreamForReadAsync();
                var inputMemoryStream = new MemoryStream();
                var outputMemoryStream = new MemoryStream();

                await stream.CopyToAsync(inputMemoryStream);
                stream.Dispose();

                inputMemoryStream.Seek(0, SeekOrigin.Begin);

				_data = inputMemoryStream.ToArray();
				inputMemoryStream.Seek(0, SeekOrigin.Begin);
				var bitmap = new BitmapImage();
				await bitmap.SetSourceAsync(inputMemoryStream.AsRandomAccessStream());
				PreviewImage.Source = bitmap;

				inputMemoryStream.Dispose();
				outputMemoryStream.Dispose();

				PrimaryButtonText = "Create";
			}
		}
	}
}
