using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NextCloudFileUploader
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
		public async Task<bool> PutWithHttp(File file, int currentIndex, int total)
		{
			if (file?.Data == null || !(file.FolderNames?.Count > 0)) return false;
			try
			{
				using (var handler = new HttpClientHandler { Credentials = new NetworkCredential(UserName, Password), PreAuthenticate = true })
				using (var client = new HttpClient(handler) { BaseAddress = new Uri(ServerUrl) })
				{

					var requestMessage =
						new HttpRequestMessage(HttpMethod.Put, new Uri(ServerUrl + file.GetRemotePath()))
						{
							Content = new ByteArrayContent(file.Data),
							Version = HttpVersion.Version11,
							Headers =
							{
								{HttpRequestHeader.Translate.ToString(), "f" },
								{HttpRequestHeader.ContentType.ToString(), "application/octet-stream" },
								{HttpRequestHeader.ContentLength.ToString(), file.Data.ToString() }
							}
						};

					var result = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead);
					if (result.StatusCode == HttpStatusCode.Created)
					{
						Utils.ShowPercentProgress("3. Загружаем файлы", currentIndex, total);
					}
					//Console.WriteLine($" File: {file.GetRemotePath()}, Size: {file.Data.Length / 1024.0:#####0.###} KB, Status: {result.StatusCode.ToString()}");

					return true;
				}

			}
			catch (WebException ex)
			{
				Console.WriteLine(ex.Message);
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{Environment.NewLine}Ошибка в методе PutWithHttp");
				Console.WriteLine($"Файл: {file.GetRemotePath()}");
				Console.WriteLine($"Ошибка: {ex.Message}");
				Console.WriteLine();
				throw;
			}
		}
		
		/// <summary>
		/// Создает и выполняет запрос для создания дочерней директории в родительской
		/// </summary>
		/// <param name="url">Родительская директория</param>
		/// <param name="remoteFolderPath">Дочерняя директория</param>
		public async Task<bool> Mkcol(string url, string remoteFolderPath, int currentIndex, int total)
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
				var result = (HttpWebResponse)await httpMkColRequest.GetResponseAsync();
				if (result.StatusCode == HttpStatusCode.Created)
				{
					Utils.ShowPercentProgress("2. Создаём папки", currentIndex, total);
				}
				//Console.WriteLine($" Folder: {remoteFolderPath} {result.StatusCode.ToString()}");

				return true;
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw;
				}

				return false;
			}
		}
	}

}