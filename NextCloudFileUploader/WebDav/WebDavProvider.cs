using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using DecaTec.WebDav;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using log4net;

namespace NextCloudFileUploader.WebDav
{
	public class WebDavProvider
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
		public string ServerUrl { get; }

		private WebDavSession Session;

		public WebDavProvider(string serverUrl, string userName, string password)
		{
			ServerUrl = serverUrl;
			Session = CreateSession(userName, password, serverUrl);
		}

		// Помещает файл в файловое хранилище.
		public async Task<bool> PutWithHttp(EntityFile entityFile)
		{
			if (entityFile?.Data == null) return false;
			try
			{
				using (var stream = new MemoryStream(entityFile.Data)) {
					await Session.UploadFileAsync(entityFile.GetRemotePath(), stream);
				}
				Log.Info($@"Файл: #{entityFile.Number.ToString()} {entityFile.GetRemotePath()} {entityFile.Data.Length / 1024.0:####0.######} КБ ({entityFile.Data.Length / (1024.0 * 1024.0):####0.######} МБ) Created");

				return true;
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

		// Создает и выполняет запрос для создания дочерней папки в родительской папке в хранилище.
		public async Task<bool> Mkcol(string url, string remoteFolderPath)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteFolderPath)) return false;
			try
			{
				await Session.CreateDirectoryAsync(remoteFolderPath);
				Log.Info($"Папка: {remoteFolderPath} Created");

				return true;
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("405"))
				{
					Log.Info($"Папка: {remoteFolderPath} {ex.Message}");
					return false;
				}
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;

			}
		}
		
		private WebDavSession CreateSession(string login, string password, string url) {
			var credentials = new NetworkCredential(login, password);
			var session     = new WebDavSession(url, credentials);
			return session;
		}
	}

}