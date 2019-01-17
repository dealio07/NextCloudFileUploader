using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;


namespace NextCloudFileUploader
{
	static class Program
	{
		/// <summary>
		/// Логин на NextCloud
		/// </summary>
		private static string _userName = "varga";    // TODO: Очистить
		/// <summary>
		/// Пароль от аккаунта на NextCloud
		/// </summary>
		private static string _password = "qiz2Zs"; // TODO: Очистить
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
		private const string DbServerName = "crm-dev";  // TODO: Заменить
		/// <summary>
		/// Имя базы
		/// </summary>
		private const string InitialCatalog = "DOdevVarha"; // TODO: Заменить
		/// <summary>
		/// Строка для создания соединения
		/// </summary>
		private const string ConnectionString = "Data Source=" + DbServerName + ";Initial Catalog=" + InitialCatalog +
		                                        ";Trusted_Connection=True;";
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

		public const string Top = "top(3)";   // TODO: Убрать в проде

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			//Application.EnableVisualStyles();
			//Application.SetCompatibleTextRenderingDefault(false);
			//Application.Run(new Form1());

			var watch = Stopwatch.StartNew();

			_webDavProvider = new WebDavProvider(ServerUrl, _userName, _password);
			_folderService = new FolderService(_webDavProvider);
			_fileService = new FileService(_webDavProvider);

			if (string.IsNullOrEmpty(_userName))
				AskUserForName();

			if (string.IsNullOrEmpty(_password))
				AskUserForPassword();

			using (IDbConnection connection = new SqlConnection(ConnectionString))
			{
				try
				{
					FillFileList(connection);
					FillFolderList();
					CreateFoldersAndUploadFiles(watch).Wait();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"\nОшибка: {ex.Message}");
					Console.WriteLine($"Стек ошибки: {ex.StackTrace}");
					throw;
				}
				finally
				{
					Console.ReadLine();
				}
			}
		}


		/// <summary>
		/// Создает папки и загружает в них файлы
		/// </summary>
		/// <param name="watch">Часы для отслеживания потраченного времени</param>
		private static void CreateFoldersAndUploadFilesThroughTaskFactory(Stopwatch watch)
		{
			Task.Factory.ContinueWhenAll(new[] { _folderService.CreateFoldersFromGroupedList(_folderList) },
					tasks =>
					{
						watch.Stop();
						Console.WriteLine($"2. Папки созданы за {watch.ElapsedMilliseconds} мс");
						watch.Restart();
					})
				.ContinueWith(async task => await Task.Factory.ContinueWhenAll(new[] { _fileService.UploadFiles(_fileList) },
					tasks =>
					{
						watch.Stop();
						var filesTotalSize = 0;
						_fileList.Where(file => file.Entity == "AccountFile").ToList().ForEach(file =>
						{
							filesTotalSize += file.Data.Length;
						});
						Console.WriteLine($"3. Файлы загружены за {watch.ElapsedMilliseconds} мс");
						Console.WriteLine($"Общий объем файлов в Account: {filesTotalSize / 1000000.0:###.##} МБ");
						Console.WriteLine("Нажмите Enter, чтобы выйти.");
					}));
		}

		private static async Task CreateFoldersAndUploadFiles(Stopwatch watch)
		{
			try
			{
				await _folderService.CreateFoldersFromGroupedList(_folderList);
				watch.Stop();
				Console.WriteLine($"2. Папки созданы за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с");
				watch.Restart();
				await _fileService.UploadFiles(_fileList);
				watch.Stop();
				var filesTotalSize = 0;
				_fileList.ToList().ForEach(file =>
				{
					filesTotalSize += file.Data.Length;
				});
				Console.WriteLine($"3. Файлы загружены за {watch.Elapsed.Hours} ч {watch.Elapsed.Minutes} м {watch.Elapsed.Seconds} с");
				Console.WriteLine($"Общий объем файлов: {filesTotalSize / (1024.0 * 1024.0):###.##} МБ");
			}
			catch
			{
				Console.WriteLine("Ошибка в методе CreateFoldersAndUploadFiles");
				throw;
			}
		}

		/// <summary>
		/// Заполняет список директорий
		/// </summary>
		private static void FillFolderList()
		{
			// По сущности
			_folderList = _fileList.GroupBy(file => file.Entity).Select(grouped => grouped.Key).ToList();
			// По сущности и ID сущности
			_folderList.AddRange(_fileList.GroupBy(file => (file.Entity, file.EntityId))
				.Select(grouped => $"{grouped.Key.Item1}/{grouped.Key.Item2}"));
			// По сущности, ID сущности и ID файла
			_folderList.AddRange(_fileList.GroupBy(file => (file.Entity, file.EntityId, file.FileId))
				.Select(grouped => $"{grouped.Key.Item1}/{grouped.Key.Item2}/{grouped.Key.Item3}"));
		}

		/// <summary>
		/// Заполняет список файлов
		/// </summary>
		/// <param name="connection">Соединение с базой</param>
		private static void FillFileList(IDbConnection connection)
		{
			Console.WriteLine("1. Получаем файлы из базы");
			foreach (var entity in Entities)
			{
				_fileList.AddRange(_fileService.GetFilesFromDb(entity, connection));
			}

			Console.WriteLine("1. Файлы получены");
		}

		/// <summary>
		/// Просит пользователя ввести логин.
		/// </summary>
		private static void AskUserForName()
		{
			Console.WriteLine("Введите логин:");
			_userName = Console.ReadLine();
		}

		/// <summary>
		/// Просит пользователя ввести пароль. Символы пароля маскируются "*".
		/// </summary>
		private static void AskUserForPassword()
		{
			Console.WriteLine("Введите пароль:");
			_password = Utils.ReadAndMaskInputPassword();
		}
	}
}
