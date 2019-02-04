using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using DecaTec.WebDav;
using NextCloudFileUploader.Entities;
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

		// Uploads file into cloud.
		public async Task PutWithHttp(EntityFile entityFile, string rootFolder)
		{
			var root = string.IsNullOrEmpty(rootFolder) ? string.Empty : $@"{rootFolder}/";
			if (entityFile?.Data == null) return;
			using (var stream = new MemoryStream(entityFile.Data))
			{
				await Session.UploadFileAsync($@"{root}{entityFile.GetRemotePath()}", stream);
			}

			Log.Info($@"File {entityFile.Number.ToString()} {entityFile.GetRemotePath()} {entityFile.Data.Length / 1024.0:####0.###} KB Created.");
		}

		// Creates folder into cloud.
		public async Task CreateDirectory(string url, string remoteFolderPath)
		{
			if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(remoteFolderPath)) return;
			await Session.CreateDirectoryAsync(remoteFolderPath);
		}
		
		private WebDavSession CreateSession(string login, string password, string url) {
			var credentials = new NetworkCredential(login, password);
			var session     = new WebDavSession(url, credentials);
			return session;
		}
	}

}