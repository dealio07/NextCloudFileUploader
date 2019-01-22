using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Diagnostics;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Services;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;

namespace NextCloudFileUploader
{
	static class Program
	{
		/// <summary>
		/// Логин на NextCloud.
		/// </summary>
		private static string _userName = "";
		/// <summary>
		/// Пароль от аккаунта на NextCloud.
		/// </summary>
		private static string _password = "";
		/// <summary>
		/// Сущности.
		/// </summary>
		private static readonly string[] Entities = { "Account", "Contact", "Contract" };
		/// <summary>
		/// Список файлов.
		/// </summary>
		private static List<EntityFile> _fileList = new List<EntityFile>();
		/// <summary>
		/// Список файлов.
		/// </summary>
		private static List<string> _folderList = new List<string>();
		/// <summary>
		/// Адрес удаленного хранилища.
		/// </summary>
		private const string ServerUrl = "https://cloud.rozetka.ua/remote.php/webdav/";
		/// <summary>
		/// Имя сервера базы.
		/// </summary>
		private static string _dbServerName = "";
		/// <summary>
		/// Имя базы.
		/// </summary>
		private static string _initialCatalog = "";
		/// <summary>
		/// Строка соединения с базой данных.
		/// </summary>
		private static string _сonnectionString = "";
		/// <summary>
		/// Провайдер WebDav.
		/// </summary>
		private static WebDavProvider _webDavProvider;
		/// <summary>
		/// Сервис работы с папками.
		/// </summary>
		private static FolderService _folderService;
		/// <summary>
		/// Сервис работы с файлами.
		/// </summary>
		private static FileService _fileService;
		/// <summary>
		/// Удалять ли временные записи удачно загруженных файлов.
		/// </summary>
		private static bool _clearTempTableAfterSuccess;
        /// <summary>
        /// Сохранить ли все выгруженные файлы во временную таблицу.
        /// </summary>
        private static bool _saveAllUploadedFilesToTempTable;

		#if DEBUG
			public const string Top = "top(1000)";
		#else
			public const string Top = "";
		#endif

		[STAThread]
		static void Main()
		{
			try
			{
				PrepareUploader();
				
				_webDavProvider = new WebDavProvider(ServerUrl, _userName, _password);
				_folderService = new FolderService(_webDavProvider);

				var dbConnection = new SqlConnection(_сonnectionString);
				_fileService = new FileService(_webDavProvider, dbConnection);

				AskUserToClearTempTableOrNot();
				TryClearTempTable();
				FillFileList();
				FillFolderList();
				AskUserToSaveAllUploadedFilesToTempTableOrNot();
				CreateFoldersAndUploadFiles().Wait();
				
				Console.Write("Нажмите ENTER для завершения программы.");
				Console.ReadLine();
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw;
			}
		}

		/// <summary>
		/// Заполняет основные данные для соединения с базой данных и хранилищем.
		/// </summary>
		private static void PrepareUploader()
		{
			if (string.IsNullOrEmpty(_dbServerName))
				AskForDbServerName();
			if (string.IsNullOrEmpty(_initialCatalog))
				AskForInitialCatalog();
			if (string.IsNullOrEmpty(_userName))
				AskUserForName();
			if (string.IsNullOrEmpty(_password))
				AskUserForPassword();
			BuildConnectionString();
		}

		/// <summary>
		/// Создает папки и выгружает в них файлы
		/// </summary>
		private static async Task CreateFoldersAndUploadFiles()
		{
			try
			{
				var watch = Stopwatch.StartNew();
				await _folderService.CreateFoldersFromGroupedList(_folderList);
				watch.Stop();
				Console.WriteLine($"[  Папки созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]{Environment.NewLine}");
				watch.Restart();
				
				var filesTotalSize = 0;
				_fileList.ToList().ForEach(file => filesTotalSize += file.Data.Length);
				
				await _fileService.UploadFiles(_fileList, _saveAllUploadedFilesToTempTable, filesTotalSize);
				watch.Stop();
				Console.WriteLine($"[  Файлы выгружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]{Environment.NewLine}");
				
				Console.WriteLine($">>> Файлы успешно выгружены <<<");
				Console.WriteLine($">>> Общий объем файлов: {filesTotalSize / (1024.0 * 1024.0):####0.###} МБ <<<{Environment.NewLine}");
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
			var watch = Stopwatch.StartNew();
			Console.WriteLine("1. Получаем файлы из базы");
			foreach (var entity in Entities)
			{
				_fileList.AddRange(_fileService.GetFilesFromDb(entity));
			}
			watch.Stop();
			Console.WriteLine($"[  Файлы получены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с ({watch.Elapsed.Milliseconds} мс) ]{Environment.NewLine}");
		}

		/// <summary>
		/// Спрашивает у пользователя название сервера базы данных.
		/// </summary>
		private static void AskForDbServerName()
		{
			Console.WriteLine("Введите название сервера базы данных:");
			var positionTop = Console.CursorTop;
			while (true)
			{
				_dbServerName = Console.ReadLine();
				if (!string.IsNullOrEmpty(_dbServerName) && _dbServerName.Length >= 3)
				{
					Console.WriteLine();
					break;
				}
				ClearConsoleLine(positionTop);
			}
		}

		/// <summary>
		/// Спрашивает у пользователя название базы данных (каталога).
		/// </summary>
		private static void AskForInitialCatalog()
		{
			Console.WriteLine("Введите название базы данных (каталога):");
			var positionTop = Console.CursorTop;
			while (true)
			{
				_initialCatalog = Console.ReadLine();
				if (!string.IsNullOrEmpty(_initialCatalog) && _initialCatalog.Length >= 3)
				{
					Console.WriteLine();
					break;
				}
				ClearConsoleLine(positionTop);
			}
		}

		/// <summary>
		/// Просит пользователя ввести имя учетной записи.
		/// </summary>
		private static void AskUserForName()
		{
			Console.WriteLine("Введите имя учетной записи:");
			var positionTop = Console.CursorTop;
			while (true)
			{
				_userName = Console.ReadLine();
				if (!string.IsNullOrEmpty(_userName) && _userName.Length >= 3)
				{
					Console.WriteLine();
					break;
				}
				ClearConsoleLine(positionTop);
			}
		}

		/// <summary>
		/// Просит пользователя ввести пароль. Символы пароля маскируются "*".
		/// </summary>
		private static void AskUserForPassword()
		{
			var firstLine = "Введите пароль:";
			var errorLine = "(пароль не должен быть короче 6 символов)";
			Console.WriteLine(firstLine);
			var positionTop = Console.CursorTop;
			while (true)
			{
				_password = Utils.ReadAndMaskInputPassword();
				if (!string.IsNullOrEmpty(_password) && _password.Length >= 6)
				{
					Console.SetCursorPosition(firstLine.Length - 1, positionTop - 1);
					Console.Write(":");
					Console.Write(new string(' ', Console.WindowWidth));
					Console.SetCursorPosition(0, positionTop);
					Console.Write(new string('*', _password.Length));
					Console.WriteLine(Environment.NewLine);
					break;
				}
				Console.SetCursorPosition(firstLine.Length - 1, positionTop - 1);
				Console.Write($" {errorLine}:");
				ClearConsoleLine(positionTop);
			}
		}

		/// <summary>
		/// Спрашивает пользователя, очищать ли таблицу записей удачно выгруженных файлов.
		/// </summary>
		private static void AskUserToClearTempTableOrNot()
		{
			if (!_fileService.CheckTempTableEntriesCount()) return;
			
			Console.WriteLine("В таблице удачно выгруженных файлов имеются записи.");
			Console.WriteLine("Очистить таблицу и выполнить полную выборку?");

			while (true)
			{
				Console.Write("Введите 'Y' или 'N': ");
				var positionTop = Console.CursorTop;
				var answer = Console.ReadKey();
				switch (answer.Key)
				{
					case ConsoleKey.Y:
						_clearTempTableAfterSuccess = true;
						Console.WriteLine();
						Console.WriteLine("Таблица будет очищена.");
						Console.WriteLine();
						break;
					case ConsoleKey.N:
						_clearTempTableAfterSuccess = false;
						Console.WriteLine();
						Console.WriteLine("Таблица не будет очищена.");
						Console.WriteLine();
						break;
					default:
						ClearConsoleLine(positionTop);
						continue;
				}
				break;
			}
		}

		/// <summary>
		/// Очищает временную таблицу по желанию пользователя.
		/// </summary>
		private static void TryClearTempTable()
		{
			if (_clearTempTableAfterSuccess)
				_fileService.DeleteFilesFromTempTable();
		}

        /// <summary>
		/// Спрашивает пользователя, записывать ли данные удачно выгруженных файлов во временную таблицу.
		/// </summary>
		private static void AskUserToSaveAllUploadedFilesToTempTableOrNot()
		{
			if (_fileList == null || _fileList.Count == 0) return;
			
            Console.WriteLine("Сохранить данные всех выгруженных в NextCloud файлов во временную таблицу?");
            Console.WriteLine("Это упростит процесс выгрузки при следующем запуске.");
            while (true)
            {
                Console.Write("Введите 'Y' или 'N': ");
                var positionTop = Console.CursorTop;
                var answer = Console.ReadKey();
                switch (answer.Key)
                {
                    case ConsoleKey.Y:
                        _saveAllUploadedFilesToTempTable = true;
                        Console.WriteLine();
                        Console.WriteLine("Данные будут записаны.");
                        Console.WriteLine();
                        break;
                    case ConsoleKey.N:
                        _saveAllUploadedFilesToTempTable = false;
                        Console.WriteLine();
                        Console.WriteLine("Данные не будут записаны.");
                        Console.WriteLine();
                        break;
                    default:
                        ClearConsoleLine(positionTop);
                        continue;
                }
                break;
            }
        }

        /// <summary>
        /// Строит строку соединения с базой данных.
        /// </summary>
        private static void BuildConnectionString()
		{
			_сonnectionString = $"Data Source={ _dbServerName };Initial Catalog={ _initialCatalog };Trusted_Connection=True;";
		}

		/// <summary>
		/// Очищает строку консоли
		/// </summary>
		/// <param name="linePositionTop">Порядковый номер строки</param>
		private static void ClearConsoleLine(int linePositionTop)
		{
			Console.SetCursorPosition(0, linePositionTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, linePositionTop);
		}
	}
}
