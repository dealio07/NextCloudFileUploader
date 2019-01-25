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
using System.Reflection;
using log4net;
using log4net.Config;

namespace NextCloudFileUploader
{
	public static class Program
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		[STAThread]
		public static void Main()
		{
			try
			{
				var appConfig = ConfigurationManager.AppSettings;
				XmlConfigurator.Configure();

				// Строка соединения с БД.
				var сonnectionString =
						$"Data Source={appConfig["dbServerName"]};Initial Catalog={appConfig["initialCatalog"]};Trusted_Connection=True;";

				// Сущности.
				var entities = appConfig["entities"].Split(',');

				var webDavProvider = new WebDavProvider(appConfig["serverUrl"], appConfig["nextCloudUserName"],
														appConfig["nextCloudPassword"]);
				var folderService = new FolderService(webDavProvider);
				var dbConnection  = new SqlConnection(сonnectionString);
				var fileService   = new FileService(webDavProvider, dbConnection);

				Utils.LogInfoAndWriteToConsole("Приложение стартовало.", Log);
				var files   = GetFileList(entities, fileService, int.Parse(appConfig["fromNumber"])).ToList();
				var folders = FillFolderList(files).ToList();
				CreateFoldersAndUploadFiles(fileService, folderService, folders, files).Wait();
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw;
			}
			finally
			{
				Utils.LogInfoAndWriteToConsole($"Приложение завершило работу.{Environment.NewLine}", Log);
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
				Utils.LogInfoAndWriteToConsole($"[  Папки созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", Log);
				watch.Restart();

				var filesTotalSize = 0;
				fileList.ToList().ForEach(file => filesTotalSize += file.Data.Length);

				await fileService.UploadFiles(fileList, filesTotalSize);
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[  Файлы выгружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", Log);

				Utils.LogInfoAndWriteToConsole(">>> Файлы успешно выгружены <<<", Log);
				Utils.LogInfoAndWriteToConsole($">>> Общий объем файлов: {filesTotalSize / 1024.0:####0.######} КБ ({filesTotalSize / (1024.0 * 1024.0):####0.######} МБ) <<<", Log);
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

		/// <summary>
		/// Заполняет список файлов.
		/// </summary>
		private static IEnumerable<EntityFile> GetFileList(IEnumerable<string> entities, FileService fileService, int fromNumber)
		{
			var fileList = new List<EntityFile>();
			var watch = Stopwatch.StartNew();
			Utils.LogInfoAndWriteToConsole("Получаем файлы из базы", Log);
			foreach (var entity in entities)
			{
				var from = fromNumber;
				var totalFiles = fileService.GetFilesCount(entity);
				if (totalFiles < from)
					totalFiles = from + 1;
				while (from < totalFiles)
				{
					fileList.AddRange(fileService.GetFilesFromDb(entity, from));
					from += 100;
				}
			}
			watch.Stop();
			Utils.LogInfoAndWriteToConsole($"[  Файлы получены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", Log);
			Utils.LogInfoAndWriteToConsole($"Всего {fileList.Count.ToString()} файлов", Log);
			return fileList;
		}
		
		
	}
}
