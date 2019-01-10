using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Uploader
{
	class WebDavProvider
	{
		private const int WaitTime = 500;

		/// <summary>
		/// Адрес удаленного хранилища
		/// </summary>
		private string ServerUrl { get; }

		public WebDavProvider(string serverUrl)
		{
			ServerUrl = serverUrl;
		}

		/// <summary>
		/// Создает дополнительные директории из списка директорий
		/// </summary>
		/// <param name="directoryNameList">Список директорий</param>
		public async Task<string> CreateAdditionalDirectories(List<string> directoryNameList)
		{
			var remoteDirectoryPath = "";
			if (directoryNameList == null) return remoteDirectoryPath;
			foreach (var directoryName in directoryNameList)
			{
				await MKCOL(ServerUrl + remoteDirectoryPath, directoryName);
				if (!remoteDirectoryPath.Contains(directoryName))
					remoteDirectoryPath += directoryName + "/";
			}
			return remoteDirectoryPath;
		}

		/// <summary>
		/// Помещает файл в файловое хранилище
		/// </summary>
		/// <param name="file">Загружаемый файл</param>
		public async Task Put(File file)
		{
			if (file?.Data != null && file.DirectoryNames?.Count > 0)
				try
				{
					// Create an HTTP request for the URL.
					var httpPutRequest = (HttpWebRequest)WebRequest.Create(ServerUrl + file.GetRemotePath());

					// Set up new credentials.
					httpPutRequest.Credentials = new NetworkCredential(Program.UserName, Program.Password);

					// Pre-authenticate the request.
					httpPutRequest.PreAuthenticate = true;

					// Define the HTTP method.
					httpPutRequest.Method = @"PUT";

					// Specify that overwriting the destination is allowed.
					httpPutRequest.Headers.Add(@"Overwrite", @"T");

					// Specify the content length.
					httpPutRequest.ContentLength = file.Data.Length;

					// Retrieve the request stream.
					using (var requestStream = httpPutRequest.GetRequestStream())
					{
						await requestStream.WriteAsync(file.Data, 0, file.Data.Length);
					}

					// Retrieve the response.
					var result = await httpPutRequest.GetResponseAsync();
					result.Close();
				}
				catch (Exception ex)
				{
					if (!ex.Message.Contains("405"))
					{
						Console.WriteLine(!ex.Message.Contains("404")
							? $"Ошибка: {ex.Message}"
							: "Конечная папка не найдена.");
					}
				}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания конечной директории для загрузки файла в хранилище
		/// </summary>
		/// <param name="file">Добавляемый файл</param>
		public async Task MKCOL(File file)
		{
			if (file?.DirectoryNames?.Count > 0)
				try
				{
					// Create an HTTP request for the URL.
					var httpMkColRequest = (HttpWebRequest)WebRequest.Create(ServerUrl + file.GetRemoteDirectoryPath());

					// Set up new credentials.
					httpMkColRequest.Credentials = new NetworkCredential(Program.UserName, Program.Password);

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
		/// <param name="remoteDirectoryPath">Дочерняя директория</param>
		public async Task MKCOL(string url, string remoteDirectoryPath)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteDirectoryPath)) return;
			try
			{
				// Create an HTTP request for the URL.
				var httpMkColRequest = (HttpWebRequest)WebRequest.Create(url + remoteDirectoryPath);

				// Set up new credentials.
				httpMkColRequest.Credentials = new NetworkCredential(Program.UserName, Program.Password);

				// Pre-authenticate the request.
				httpMkColRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpMkColRequest.Method = @"MKCOL";

				// Retrieve the response.
				var task = httpMkColRequest.GetResponseAsync();
				task.Wait();
				await task;
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