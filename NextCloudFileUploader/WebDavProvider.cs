using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NextCloudFileUploader
{
	public class WebDavProvider
	{
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
		/// Помещает файл в файловое хранилище.
		/// </summary>
		/// <param name="file">Выгружаемый файл</param>
		/// <param name="currentIndex">Индекс данного выгружаемого файла среди всех выгружаемых файлов</param>
		/// <param name="total">Количество выгружаемых файлов</param>
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
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания дочерней папки в родительской папке в хранилище.
		/// </summary>
		/// <param name="url">Родительская папка</param>
		/// <param name="remoteFolderPath">Дочерняя папка</param>
		/// <param name="currentIndex">Индекс данной создаваемой папки среди всех создаваемых папок.</param>
		/// <param name="total">Количество создаваемых папок</param>
		public async Task<bool> Mkcol(string url, string remoteFolderPath, int currentIndex, int total)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteFolderPath)) return false;
			try
			{
				var httpMkColRequest = (HttpWebRequest)WebRequest.Create(url + remoteFolderPath);

				httpMkColRequest.Credentials = new NetworkCredential(UserName, Password);
				httpMkColRequest.PreAuthenticate = true;
				httpMkColRequest.Method = @"MKCOL";

				var result = (HttpWebResponse)await httpMkColRequest.GetResponseAsync();
				if (result.StatusCode == HttpStatusCode.Created)
				{
					Utils.ShowPercentProgress("2. Создаём папки", currentIndex, total);
				}

				return true;
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("405")) return false;
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;

			}
		}
	}

}