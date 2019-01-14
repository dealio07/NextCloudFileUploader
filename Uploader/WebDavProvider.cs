using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Uploader
{
	public class WebDavProvider
	{
		/// <summary>
		/// Адрес удаленного хранилища
		/// </summary>
		public string ServerUrl { get; }

		private string UserName;

		private string Password;

		public WebDavProvider(string serverUrl, string userName, string password)
		{
			ServerUrl = serverUrl;
			UserName = userName;
			Password = password;
		}

		/// <summary>
		/// Помещает файл в файловое хранилище
		/// </summary>
		/// <param name="file">Загружаемый файл</param>
		public async Task<bool> Put(File file)
		{
			if (file?.Data == null || !(file.FolderNames?.Count > 0)) return false;
			try
			{
				// Create an HTTP request for the URL.
				var httpPutRequest = (HttpWebRequest)WebRequest.Create(ServerUrl + file.GetRemotePath());

				// Set up new credentials.
				httpPutRequest.Credentials = new NetworkCredential(UserName, Password);

				// Pre-authenticate the request.
				httpPutRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpPutRequest.Method = @"PUT";

				// Specify that overwriting the destination is allowed.
				httpPutRequest.Headers.Add(@"Overwrite", @"T");

				// Optional, but allows for larger files.
				httpPutRequest.SendChunked = false;

				// Specify the content length.
				httpPutRequest.ContentLength = file.Data.Length;

				var responseConsistency = false;
				// Retrieve the request stream.
				using (var stream = httpPutRequest.GetRequestStream())
					await stream.WriteAsync(file.Data, 0, file.Data.Length).ContinueWith(t =>
					{
						// Retrieve the response.
						httpPutRequest.GetResponseAsync().Wait();
						responseConsistency = httpPutRequest.ContentLength == file.Data.Length;
					});
				return responseConsistency;
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw;
				}
			}
			return false;
		}

		/// <summary>
		/// Создает и выполняет запрос для создания дочерней директории в родительской
		/// </summary>
		/// <param name="url">Родительская директория</param>
		/// <param name="remoteFolderPath">Дочерняя директория</param>
		public async Task<bool> Mkcol(string url, string remoteFolderPath)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteFolderPath)) return false;
			try
			{
				// Create an HTTP request for the URL.
				var httpMkColRequest = (HttpWebRequest)WebRequest.Create(url + remoteFolderPath);

				// Set up new credentials.
				httpMkColRequest.Credentials = new NetworkCredential(UserName, Password);

				// Pre-authenticate the request.
				httpMkColRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpMkColRequest.Method = @"MKCOL";

				// Retrieve the response.
				//httpMkColRequest.GetResponseAsync().Wait(WaitTime);
				var result = (HttpWebResponse)await httpMkColRequest.GetResponseAsync();
				if (result != null && result.StatusCode == HttpStatusCode.Created)
					return true;
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw;
				}
			}
			return false;
		}
	}
}