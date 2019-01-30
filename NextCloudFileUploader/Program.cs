using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Diagnostics;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Services;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;
using System.Configuration;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using log4net.Config;

namespace NextCloudFileUploader
{
	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			var progWatch = Stopwatch.StartNew();
			try
			{
				var appConfig = ConfigurationManager.AppSettings;
				XmlConfigurator.Configure();

				// Строка соединения с БД.
				var сonnectionString = appConfig["connStr"];

				// Сущности.
				var entities = appConfig["entities"].Split(',');

				var webDavProvider = new WebDavProvider(appConfig["serverUrl"], appConfig["nextCloudUserName"],
														appConfig["nextCloudPassword"]);
				var folderService = new FolderService(webDavProvider);
				var dbConnection  = new SqlConnection(сonnectionString);
				var fileService   = new FileService(webDavProvider, dbConnection);

				Utils.LogInfoAndWriteToConsole("Приложение стартовало.");

				var allFolders = new HashSet<string>();
				foreach (var entity in entities)
				{
					var from = int.Parse(appConfig["fromNumber"]);
					var totalFiles = fileService.GetFilesCount(entity, from.ToString());
					var entityFilesTotalSize = 0;
					if (totalFiles < from)
						totalFiles = from + 1;
					while (from < totalFiles)
					{
						Utils.LogInfoAndWriteToConsole($"Получаем файлы {entity}File из БД");
						var watch = Stopwatch.StartNew();
						var fileList = fileService.GetFilesFromDb(entity, from.ToString()).ToList();
						watch.Stop();
						if (fileList.Count > 0)
						{
							Utils.LogInfoAndWriteToConsole($"[ Получено {fileList.Count.ToString()} файлов {entity}File за {watch.Elapsed.Hours.ToString()} ч {watch.Elapsed.Minutes.ToString()} м {watch.Elapsed.Seconds.ToString()} с {watch.Elapsed.Milliseconds.ToString()} мс ]");
							fileList.ToList().ForEach(file => entityFilesTotalSize += file.Data.Length);
							var folders = FillFolderList(fileList).ToList();
							var notCreatedFolders = folders.Where(folder => !allFolders.Contains(folder)).ToList();
							CreateFoldersAndUploadFiles(fileService, folderService, notCreatedFolders, fileList).Wait();
							folders.ForEach(folder => allFolders.Add(folder));
							from = fileList[fileList.Count - 1].Number + 1;
						}
						else
						{
							Utils.LogInfoAndWriteToConsole($"[ Получено 0 файлов {entity}File ]");
							break;
						}
					}
					Utils.LogInfoAndWriteToConsole($">>> Файлы {entity}File успешно выгружены <<<");
					Utils.LogInfoAndWriteToConsole($">>> Объем выгруженных файлов {entity}File: {entityFilesTotalSize / 1024.0:####0.######} КБ ({entityFilesTotalSize / (1024.0 * 1024.0):####0.######} МБ) <<<");
				}
				progWatch.Stop();
				Utils.LogInfoAndWriteToConsole($"Приложение успешно завершило работу за {progWatch.Elapsed.Hours.ToString()} ч {progWatch.Elapsed.Minutes.ToString()} м {progWatch.Elapsed.Seconds.ToString()} с {progWatch.Elapsed.Milliseconds.ToString()} мс");
			}
			catch (Exception ex)
			{
				Utils.LogInfoAndWriteToConsole("Приложение завершило работу с ошибкой.");
				ExceptionHandler.LogExceptionToConsole(ex);
			}
		}

		/// <summary>
		/// Создает папки и выгружает в них файлы
		/// </summary>
		private static async Task CreateFoldersAndUploadFiles(FileService fileService, FolderService folderService, IEnumerable<string> folderList, IEnumerable<EntityFile> fileList)
		{
			try
			{
				var list = fileList.ToList();
				var watch = Stopwatch.StartNew();
				var groupedFolderNameList = folderList.ToList();
				await folderService.CreateFoldersFromGroupedList(groupedFolderNameList);
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[ Создано {groupedFolderNameList.Count.ToString()} папок {list[0].Entity} за {watch.Elapsed.Hours.ToString()} ч {watch.Elapsed.Minutes.ToString()} м {watch.Elapsed.Seconds.ToString()} с {watch.Elapsed.Milliseconds.ToString()} мс ]");
				watch.Restart();

				var splicedFiles = Utils.SplitList(list, 2).ToList();
				foreach (var files in splicedFiles)
				{
					await fileService.UploadFiles(files, list);
				}
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[ Выгружено {list.Count.ToString()} файлов {list[0].Entity} за {watch.Elapsed.Hours.ToString()} ч {watch.Elapsed.Minutes.ToString()} м {watch.Elapsed.Seconds.ToString()} с {watch.Elapsed.Milliseconds.ToString()} мс ]");
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}
		}

		/// <summary>
		/// Заполняет список папок.
		/// </summary>
		private static IEnumerable<string> FillFolderList(IEnumerable<EntityFile> fileList)
		{
			var entityFiles = fileList.ToList();
			// По сущности
			var folderList = entityFiles.GroupBy(file => file.Entity).Select(grouped => grouped.Key).ToList();
			// По сущности и ID сущности
			folderList.AddRange(entityFiles.GroupBy(file => (file.Entity, file.EntityId))
				.Select(grouped => $"{grouped.Key.Entity}/{grouped.Key.EntityId}"));
			// По сущности, ID сущности и ID файла
			folderList.AddRange(entityFiles.GroupBy(file => (file.Entity, file.EntityId, file.FileId))
				.Select(grouped => $"{grouped.Key.Entity}/{grouped.Key.EntityId}/{grouped.Key.FileId}"));
			return folderList;
		}
	}
}
