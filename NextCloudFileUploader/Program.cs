using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Diagnostics;
using NextCloudFileUploader.Services;
using NextCloudFileUploader.WebDav;
using System.Configuration;
using log4net;
using log4net.Config;

namespace NextCloudFileUploader
{
	public static class Program
	{
		private static readonly ILog Log = LogManager.GetLogger("NextCloudFileUploader");
		
		[STAThread]
		public static void Main()
		{
			var progWatch = Stopwatch.StartNew() ;
			try
			{
				var appConfig = ConfigurationManager.AppSettings;
				XmlConfigurator.Configure();

				var webDavProvider = new WebDavProvider(appConfig["serverUrl"], appConfig["nextCloudUserName"],
														appConfig["nextCloudPassword"]);
				var folderService = new FolderService(webDavProvider);
				var dbService = new DbService(appConfig["connStr"]);

				Log.Info("Starting the application.");

				Upload(dbService, appConfig, folderService, webDavProvider, appConfig["root"]);
				
				progWatch.Stop();
				Log.Info($"Uploading successfully done in {progWatch.Elapsed.Hours.ToString()} h {progWatch.Elapsed.Minutes.ToString()} m {progWatch.Elapsed.Seconds.ToString()} s {progWatch.Elapsed.Milliseconds.ToString()} ms.");
			}
			catch (Exception ex)
			{
				Log.Error("Uploading ended with error.", ex);
			}
		}

		private static void Upload(DbService dbService, NameValueCollection appConfig,
									FolderService folderService, WebDavProvider webDavProvider, string rootFolder)
		{
			var from = int.Parse(appConfig["fromNumber"]);
			var entityFilesTotalSize = 0;
			while (true)
			{
				var fileList = dbService.GetHundredFilesFromDbAsync(from);
				Log.Info($"Received {fileList.Count} files");
				if (fileList.Count == 0)
					break;
				
				from += fileList.Count;

				var rangePartitioner = Partitioner.Create(0, fileList.Count, 10);
				Parallel.ForEach(rangePartitioner, (range, loopState) =>
				{
					for (var i = range.Item1; i < range.Item2; i++)
					{
						try
						{
							var file = fileList[i];
							file.Data = dbService.GetFileDataAsync(file.FileId, file.Version, file.Entity);
							if (file.Data == null) continue;
							entityFilesTotalSize += file.Data.Length;
							folderService.CreateFolders(file.GetRemoteFolderPath(), rootFolder);
							webDavProvider.PutWithHttp(file, rootFolder).Wait();
						}
						catch (Exception)
						{
							Log.Error($@"Failed uploading file {fileList[i].Number} {fileList[i].GetRemotePath()}.");
							throw;
						}
					}
				});
			}

			Log.Info($"Total size of files: {entityFilesTotalSize / 1024.0:####0.###} KB");
		}
	}
}
