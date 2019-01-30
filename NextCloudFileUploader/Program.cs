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
using log4net.Config;

namespace NextCloudFileUploader
{
	public static class Program
	{
		[STAThread]
		public static void Main()
		{
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

				foreach (var entity in entities)
				{
					var from = 0;
					var totalFiles = fileService.GetFilesCount(entity, from.ToString());
					if (totalFiles < from)
						totalFiles = from + 1;
					while (from < totalFiles)
					{
						var watch = Stopwatch.StartNew();
						Utils.LogInfoAndWriteToConsole($"Получаем файлы {entity} из базы");
						var fileList = fileService.GetFilesFromDb(entity, from.ToString()).ToList();
						watch.Stop();
						Utils.LogInfoAndWriteToConsole($"[  Файлы {entity} получены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]");
						Utils.LogInfoAndWriteToConsole($"Получено {fileList.Count.ToString()} файлов {entity}");
						if (fileList.Count > 0)
						{
							var folders = FillFolderList(fileList).ToList();
							CreateFoldersAndUploadFiles(fileService, folderService, folders, fileList).Wait();
							from = fileList[fileList.Count - 1].Number + 1;
						}
						else break;
					}
				}
				Utils.LogInfoAndWriteToConsole("Приложение завершило работу.");
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
		private static async Task CreateFoldersAndUploadFiles(FileService fileService, FolderService folderService, List<string> folderList, List<EntityFile> fileList)
		{
			try
			{
				var watch = Stopwatch.StartNew();
				await folderService.CreateFoldersFromGroupedList(folderList);
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[  Папки {fileList[0].Entity} созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]");
				watch.Restart();

				var filesTotalSize = 0;
				fileList.ToList().ForEach(file => filesTotalSize += file.Data.Length);

				var splicedFiles = Utils.SplitList(fileList, 2).ToList();
				foreach (var files in splicedFiles)
				{
					await fileService.UploadFiles(files, filesTotalSize);
				}
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[  Файлы {fileList[0].Entity} выгружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]");

				Utils.LogInfoAndWriteToConsole($">>> Файлы {fileList[0].Entity} успешно выгружены <<<");
				Utils.LogInfoAndWriteToConsole($">>> Объем выгруженных файлов  {fileList[0].Entity}: {filesTotalSize / 1024.0:####0.######} КБ ({filesTotalSize / (1024.0 * 1024.0):####0.######} МБ) <<<");
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
