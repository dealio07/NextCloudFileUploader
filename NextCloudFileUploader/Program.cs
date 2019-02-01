using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Diagnostics;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Services;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;
using System.Configuration;
using System.Linq;
using System.Threading;
using log4net.Config;

namespace NextCloudFileUploader
{
	public static class Program
	{
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
				var fileService   = new FileService(webDavProvider);
				var dbService   = new DbService(appConfig["connStr"]);

				Utils.LogInfoAndWriteToConsole("Приложение стартовало.");

				Upload(dbService, appConfig, fileService, folderService).Wait();
				
				progWatch.Stop();
				Utils.LogInfoAndWriteToConsole($"Приложение успешно завершило работу за {progWatch.Elapsed.Hours.ToString()} ч {progWatch.Elapsed.Minutes.ToString()} м {progWatch.Elapsed.Seconds.ToString()} с {progWatch.Elapsed.Milliseconds.ToString()} мс");
			}
			catch (Exception ex)
			{
				Utils.LogInfoAndWriteToConsole("Приложение завершило работу с ошибкой.");
				ExceptionHandler.LogExceptionToConsole(ex);
			}
		}

		private static async Task Upload(DbService           dbService, NameValueCollection appConfig,
										 FileService         fileService, FolderService folderService)
		{
			var from                 = int.Parse(appConfig["fromNumber"]);
			var entityFilesTotalSize = 0;
			while (true)
			{
				var fileList = (await dbService.GetHundredFilesFromDbByEntityAsync(from)).ToList();
				Utils.LogInfoAndWriteToConsole($"[ Получено {fileList.Count} файлов ]");
				if (fileList.Count == 0)
					break;
				
				from += fileList.Count;

				var rangePartitioner = Partitioner.Create(0, fileList.Count, 50);
				Parallel.ForEach(rangePartitioner, (range, loopState) =>
				{
					for (var i = range.Item1; i < range.Item2; i++)
					{
						var file = fileList[i];
						try
						{
							file.Data = dbService.GetFileDataAsync(file.FileId, file.Version, file.Entity).Result;
							if (file.Data == null) continue;
							entityFilesTotalSize += file.Data.Length;
							folderService.CreateFolders(file.GetRemoteFolderPath()).Wait();
							fileService.UploadFile(file).Wait();
						}
						catch (Exception ex)
						{
							Utils.LogInfoAndWriteToConsole($"Error on file #{file.Number}");
							ExceptionHandler.LogExceptionToConsole(ex);
							//throw ex;
						}
					}
				});
				
			}

			Utils.LogInfoAndWriteToConsole(
					$">>> Объем выгруженных файлов: {entityFilesTotalSize / 1024.0:####0.######} КБ ({entityFilesTotalSize / (1024.0 * 1024.0):####0.######} МБ) <<<");
		}
	}
}
