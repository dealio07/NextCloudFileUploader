using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Diagnostics;

namespace NextCloudFileUploader
{
	static class Program
	{
		/// <summary>
		/// Логин на NextCloud
		/// </summary>
		private static string _userName = "";
		/// <summary>
		/// Пароль от аккаунта на NextCloud
		/// </summary>
		private static string _password = "";
		/// <summary>
		/// Сущности
		/// </summary>
		private static readonly string[] Entities = { "Account", "Contact", "Contract" };
		/// <summary>
		/// Список файлов
		/// </summary>
		private static List<File> _fileList = new List<File>();
		/// <summary>
		/// Список файлов
		/// </summary>
		private static List<string> _folderList = new List<string>();
		/// <summary>
		/// Адрес удаленного хранилища
		/// </summary>
		private const string ServerUrl = "https://cloud.rozetka.ua/remote.php/webdav/";
		/// <summary>
		/// Имя сервера базы
		/// </summary>
		private static string _dbServerName = "";
		/// <summary>
		/// Имя базы
		/// </summary>
		private static string _initialCatalog = "";
		/// <summary>
		/// Строка соединения с базой данных
		/// </summary>
		private static readonly string СonnectionString = $"Data Source={_dbServerName};Initial Catalog={_initialCatalog};Trusted_Connection=True;";
		/// <summary>
		/// Провайдер WebDav
		/// </summary>
		private static WebDavProvider _webDavProvider;
		/// <summary>
		/// Сервис работы с папками
		/// </summary>
		private static FolderService _folderService;
		/// <summary>
		/// Сервис работы с файлами
		/// </summary>
		private static FileService _fileService;
		/// <summary>
		/// Удалять ли временные записи удачно загруженных файлов
		/// </summary>
		private static bool _clearTempTableAfterSuccess;

		public const string Top = "top(10)";   // TODO: Убрать в проде

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			if (string.IsNullOrEmpty(_dbServerName))
				AskForDbServerName();
			if (string.IsNullOrEmpty(_initialCatalog))
				AskForInitialCatalog();
			if (string.IsNullOrEmpty(_userName))
				AskUserForName();
			if (string.IsNullOrEmpty(_password))
				AskUserForPassword();
			
			try
			{
				_webDavProvider = new WebDavProvider(ServerUrl, _userName, _password);
				_folderService = new FolderService(_webDavProvider);
				_fileService = new FileService(_webDavProvider, new SqlConnection(СonnectionString));

				if (_fileService.CheckTempTableEntriesCount())
					AskUserToClearTempTableOrNot();

				FillFileList();
				FillFolderList();
				
				CreateFoldersAndUploadFiles(Stopwatch.StartNew()).Wait();
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw;
			}
			finally
			{
				Console.ReadLine();
			}
		}


		/// <summary>
		/// Создает папки и загружает в них файлы
		/// </summary>
		/// <param name="watch">Таймер</param>
		private static async Task CreateFoldersAndUploadFiles(Stopwatch watch)
		{
			try
			{
				await _folderService.CreateFoldersFromGroupedList(_folderList);
				watch.Stop();
				Console.WriteLine($"[ Папки созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ]");
				watch.Restart();
				await _fileService.UploadFiles(_fileList, _clearTempTableAfterSuccess);
				watch.Stop();
				var filesTotalSize = 0;
				_fileList.ToList().ForEach(file => filesTotalSize += file.Data.Length);
				Console.WriteLine($"[ Файлы загружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ]");
				Console.WriteLine($">>> Общий объем файлов: {filesTotalSize / (1024.0 * 1024.0):###.##} МБ <<<");
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
		private static void FillFolderList()
		{
			// По сущности
			_folderList = _fileList.GroupBy(file => file.Entity).Select(grouped => grouped.Key).ToList();
			// По сущности и ID сущности
			_folderList.AddRange(_fileList.GroupBy(file => (file.Entity, file.EntityId))
				.Select(grouped => $"{grouped.Key.Entity}/{grouped.Key.EntityId}"));
			// По сущности, ID сущности и ID файла
			_folderList.AddRange(_fileList.GroupBy(file => (file.Entity, file.EntityId, file.FileId))
				.Select(grouped => $"{grouped.Key.Entity}/{grouped.Key.EntityId}/{grouped.Key.FileId}"));
		}

		/// <summary>
		/// Заполняет список файлов.
		/// </summary>
		private static void FillFileList()
		{
			Console.WriteLine("1. Получаем файлы из базы");
			foreach (var entity in Entities)
			{
				_fileList.AddRange(_fileService.GetFilesFromDb(entity));
			}

			Console.WriteLine("1. Файлы получены");
		}

		/// <summary>
		/// Просит пользователя ввести имя учетной записи.
		/// </summary>
		private static void AskUserForName()
		{
			while (true)
			{
				Console.Write("Введите имя учетной записи:");
				_userName = Console.ReadLine();
				if (!string.IsNullOrEmpty(_userName))
				{
					Console.WriteLine();
					break;
				}
				Console.Write("\r");
			}
		}

		/// <summary>
		/// Просит пользователя ввести пароль. Символы пароля маскируются "*".
		/// </summary>
		private static void AskUserForPassword()
		{
			while (true)
			{
				Console.Write("Введите пароль:");
				_password = Utils.ReadAndMaskInputPassword();
				if (!string.IsNullOrEmpty(_password))
				{
					Console.WriteLine();
					break;
				}
				Console.Write("\r");
			}
		}

		/// <summary>
		/// Спрашивает пользователя, очищать ли таблицу записей удачно выгруженных файлов.
		/// </summary>
		private static void AskUserToClearTempTableOrNot()
		{
			Console.WriteLine("В таблице удачно выгруженных файлов имеются записи. Очистить?");

			while (true)
			{
				Console.Write("Ввести '+' или '-': ");
				var answer = Console.ReadKey();
				switch (answer.Key)
				{
					case ConsoleKey.Add:
						_clearTempTableAfterSuccess = true;
						Console.WriteLine();
						break;
					case ConsoleKey.Subtract:
						_clearTempTableAfterSuccess = false;
						Console.WriteLine();
						break;
					default:
						Console.Write("\r");
						continue;
				}
				break;
			}
		}

		/// <summary>
		/// Спрашивает у пользователя название сервера базы данных
		/// </summary>
		private static void AskForDbServerName()
		{
			while (true)
			{
				Console.Write("Введите название сервера базы данных:");
				_dbServerName = Console.ReadLine();
				if (!string.IsNullOrEmpty(_dbServerName))
				{
					Console.WriteLine();
					break;
				}
				Console.Write("\r");
			}
		}

		/// <summary>
		/// Спрашивает у пользователя название базы данных (каталога)
		/// </summary>
		private static void AskForInitialCatalog()
		{
			while (true)
			{
				Console.Write("Введите название базы данных (каталога):");
				_initialCatalog = Console.ReadLine();
				if (!string.IsNullOrEmpty(_initialCatalog))
				{
					Console.WriteLine();
					break;
				}
				Console.Write("\r");
			}
		}
	}
}
