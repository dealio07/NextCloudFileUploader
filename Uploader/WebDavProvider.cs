using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Uploader
{
	public class WebDavProvider
	{
		private const int WaitTime = 500;

		/// <summary>
		/// Адрес удаленного хранилища
		/// </summary>
		private string ServerUrl { get; }

		private string UserName;

		private string Password;

		public WebDavProvider(string serverUrl, string userName, string password)
		{
			ServerUrl = serverUrl;
			UserName = userName;
			Password = password;
		}

		/// <summary>
		/// Создает дополнительные директории из списка директорий
		/// </summary>
		/// <param name="folderNameList">Список директорий</param>
		public async Task<string> CreateFolders(List<string> folderNameList)
		{
			var remoteFolderPath = "";
			if (folderNameList == null) return remoteFolderPath;
			foreach (var folderName in folderNameList)
			{
				await MKCOL(ServerUrl + remoteFolderPath, folderName);
				if (!remoteFolderPath.Contains(folderName))
					remoteFolderPath += folderName + "/";
			}
			return remoteFolderPath;
		}

		/// <summary>
		/// Помещает файл в файловое хранилище
		/// </summary>
		/// <param name="file">Загружаемый файл</param>
		public async Task Put(File file)
		{
			if (file?.Data != null && file.FolderNames?.Count > 0)
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

					httpPutRequest.Headers.Add(@"Keep-Alive", @"True");

					// Specify the content length.
					httpPutRequest.ContentLength = file.Data.Length;

					// Retrieve the request stream.
					using (var requestStream = httpPutRequest.GetRequestStream())
					{
						await requestStream.WriteAsync(file.Data, 0, file.Data.Length);
					}

					// Retrieve the response.
					httpPutRequest.GetResponseAsync().Wait(WaitTime);
				}
				catch (Exception ex)
				{
					if (!ex.Message.Contains("405"))
					{
						Console.WriteLine($"Ошибка: {ex.Message}");
						Console.WriteLine($"Стек ошибки: {ex.StackTrace}");
						throw;
					}
				}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания конечной директории для загрузки файла в хранилище
		/// </summary>
		/// <param name="file">Добавляемый файл</param>
		public async Task MKCOL(File file)
		{
			if (file?.FolderNames?.Count > 0)
				try
				{
					// Create an HTTP request for the URL.
					var httpMkColRequest = (HttpWebRequest)WebRequest.Create(ServerUrl + file.GetRemoteFolderPath());

					// Set up new credentials.
					httpMkColRequest.Credentials = new NetworkCredential(UserName, Password);

					// Pre-authenticate the request.
					httpMkColRequest.PreAuthenticate = true;

					// Define the HTTP method.
					httpMkColRequest.Method = @"MKCOL";

					// Retrieve the response.
					var task = httpMkColRequest.GetResponseAsync();
					//task.Wait(WaitTime);
					var httpMkColResponse = (HttpWebResponse)(await task);

					if (httpMkColResponse.StatusCode == HttpStatusCode.Created && file.Data.Length > 0)
						await Put(file);
				}
				catch (Exception ex)
				{
					if (!ex.Message.Contains("405"))
					{
						throw;
					}
				}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания дочерней директории в родительской
		/// </summary>
		/// <param name="url">Родительская директория</param>
		/// <param name="remoteFolderPath">Дочерняя директория</param>
		public async Task MKCOL(string url, string remoteFolderPath)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteFolderPath)) return;
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
				httpMkColRequest.GetResponseAsync().Wait(WaitTime);
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw;
				}
			}
		}
	}
}