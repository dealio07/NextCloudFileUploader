using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using log4net;

namespace NextCloudFileUploader.WebDav
{
	public class WebDavProvider
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
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
		/// <param name="entityFile">Выгружаемый файл</param>
		/// <param name="allFiles">Все файлы сущности</param>
		public async Task<bool> PutWithHttp(EntityFile entityFile, IEnumerable<EntityFile> allFiles)
		{
			if (entityFile?.Data == null || !(entityFile.FolderNames?.Count > 0)) return false;
			try
			{
				using (var handler = new HttpClientHandler { Credentials = new NetworkCredential(UserName, Password), PreAuthenticate = true })
				using (var client = new HttpClient(handler) { BaseAddress = new Uri(ServerUrl) })
				{

					var requestMessage =
						new HttpRequestMessage(HttpMethod.Put, new Uri(ServerUrl + entityFile.GetRemotePath()))
						{
							Content = new ByteArrayContent(entityFile.Data),
							Version = HttpVersion.Version11,
							Headers =
							{
								{HttpRequestHeader.Translate.ToString(), "f" },
								{HttpRequestHeader.ContentType.ToString(), "application/octet-stream" },
								{HttpRequestHeader.ContentLength.ToString(), entityFile.Data.ToString() }
							}
						};

					var result = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead);
					var entityFiles = allFiles.ToList();
					Log.Info(Utils.ShowPercentProgress("Выгружаем файлы", entityFiles.IndexOf(entityFile), entityFiles.Count));
					Log.Info($"Файл: #{entityFile.Number.ToString()} {entityFile.Entity}/{entityFile.EntityId}/{entityFile.FileId}/{entityFile.Version} {entityFile.Data.Length / 1024.0:####0.######} КБ ({entityFile.Data.Length / (1024.0 * 1024.0):####0.######} МБ) {result.StatusCode.ToString()}");

					return true;
				}

			}
			catch (WebException ex)
			{
				Console.WriteLine(ex.Message);
				Log.Debug(ex);
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
				Log.Info(Utils.ShowPercentProgress("Создаём папки", currentIndex, total));
				Log.Info($"Папка: {remoteFolderPath} {result.StatusDescription}");

				return true;
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("405"))
				{
					Log.Info(Utils.ShowPercentProgress("Создаём папки", currentIndex, total));
					Log.Info($"Папка: {remoteFolderPath} {ex.Message}");
					return false;
				}
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;

			}
		}
	}

}