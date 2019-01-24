﻿using System;
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
		private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
		#if DEBUG
			public const string Top = "top(3)";
		#else
			public const string Top = "";
		#endif

		[STAThread]
		public static void Main()
		{
			try
			{
				var appConfig = ConfigurationManager.AppSettings;
				XmlConfigurator.Configure();

				// Строка соединения с БД.
				var сonnectionString = $"Data Source={ appConfig["dbServerName"] };Initial Catalog={ appConfig["initialCatalog"] };Trusted_Connection=True;";

				// Сущности.
				var entities = appConfig["entities"].Split(',');

				var webDavProvider = new WebDavProvider(appConfig["serverUrl"], appConfig["nextCloudUserName"], appConfig["nextCloudPassword"]);
				var folderService = new FolderService(webDavProvider);
				var dbConnection = new SqlConnection(сonnectionString);
				var fileService = new FileService(webDavProvider, dbConnection);

				Utils.LogInfoAndWriteToConsole("Приложение стартовало.", _log);
				var files = GetFileList(entities, fileService, int.Parse(appConfig["fromNumber"])).ToList();
				var folders = FillFolderList(files).ToList();
				CreateFoldersAndUploadFiles(fileService, folderService, folders, files).Wait();

				Utils.LogInfoAndWriteToConsole($"Приложение закончило работу.{Environment.NewLine}", _log);
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw;
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
				Utils.LogInfoAndWriteToConsole($"[  Папки созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", _log);
				watch.Restart();

				var filesTotalSize = 0;
				fileList.ToList().ForEach(file => filesTotalSize += file.Data.Length);

				await fileService.UploadFiles(fileList, filesTotalSize);
				watch.Stop();
				Utils.LogInfoAndWriteToConsole($"[  Файлы выгружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", _log);

				Utils.LogInfoAndWriteToConsole(">>> Файлы успешно выгружены <<<", _log);
				Utils.LogInfoAndWriteToConsole($">>> Общий объем файлов: {filesTotalSize / (1024.0 * 1024.0):####0.###} МБ <<<", _log);
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
			Utils.LogInfoAndWriteToConsole("Получаем файлы из базы", _log);
			foreach (var entity in entities)
			{
				fileList.AddRange(fileService.GetFilesFromDb(entity, fromNumber));
			}
			watch.Stop();
			Utils.LogInfoAndWriteToConsole($"[  Файлы получены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]", _log);
			return fileList;
		}
		
		
	}
}
