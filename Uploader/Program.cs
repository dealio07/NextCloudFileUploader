using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Uploader
{
	public class Program
	{
		/// <summary>
		/// Логин на NextCloud
		/// </summary>
		private static string _userName = "varga";    // TODO: Очистить
		/// <summary>
		/// Пароль от аккаунта на NextCloud
		/// </summary>
		private static string _password = "qiz2Zs";	// TODO: Очистить
		/// <summary>
		/// Сущности
		/// </summary>
		private static readonly string[] Entities = {"Account", "Contact", "Contract"};
		/// <summary>
		/// Список файлов
		/// </summary>
		private static List<File> _fileList = new List<File>();
		/// <summary>
		/// Список файлов
		/// </summary>
		private static List<string> _folderList = new List<string>();
		/// <summary>
		/// Файлы, сгруппированные по сущности
		/// </summary>
		private static IEnumerable<IGrouping<string, File>> _groupedByEntity = new List<IGrouping<string, File>>();
		/// <summary>
		/// Файлы, сгруппированные по сущности и ID сущности
		/// </summary>
		private static IEnumerable<IGrouping<(string Entity, string EntityId), File>> _groupedByEntityAndEntityId = new List<IGrouping<(string Entity, string EntityId), File>>();
		/// <summary>
		/// Файлы, сгруппированные по сущности, ID сущности и ID файла
		/// </summary>
		private static IEnumerable<IGrouping<(string Entity, string EntityId, string FileId), File>> _groupedByEntityAndEntityIdAndFileId = new List<IGrouping<(string Entity, string EntityId, string FileId), File>>();
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

		private static void Main(string[] args)
		{
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
					GroupFiles();
					FillFolderList();
					CreateFoldersAndUploadFiles(watch);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					throw;
				}
				finally
				{
					Console.WriteLine("Дождитесь полной загрузки файлов.");
					Console.ReadLine();
				}
			}
		}

		/// <summary>
		/// Создает папки и загружает в них файлы
		/// </summary>
		/// <param name="watch">Часы для отслеживания потраченного времени</param>
		private static void CreateFoldersAndUploadFiles(Stopwatch watch)
		{
			Task.Factory.ContinueWhenAll(new[] { _folderService.CreateFoldersInParallel(_fileList) }, tasks =>
				  {
					  watch.Stop();
					  Console.WriteLine($"2. Папки созданы за {watch.ElapsedMilliseconds / 1000} сек");
					  watch.Restart();
				  })
				.ContinueWith(task => Task.Factory.ContinueWhenAll(new[] { _fileService.UploadFilesInParallel(_fileList) }, tasks => { watch.Stop(); }))
				.ContinueWith(task => Task.Factory.ContinueWhenAll(new[]
				{
					new Task(() =>
					{
						Console.WriteLine($"3. Файлы загружены за {watch.ElapsedMilliseconds / 1000} сек");
						Console.WriteLine("Нажмите Enter, чтобы выйти.");
					})
				}, tasks => { }));
		}

		/// <summary>
		/// Группирует файлы по сущности, ID сущности и ID файла
		/// </summary>
		private static void GroupFiles()
		{
			_groupedByEntity = _fileList.GroupBy(file => file.Entity);
			_groupedByEntityAndEntityId = _fileList.GroupBy(file => (file.Entity, file.EntityId));
			_groupedByEntityAndEntityIdAndFileId = _fileList.GroupBy(file => (file.Entity, file.EntityId, file.FileId));
		}

		/// <summary>
		/// Заполняет список директорий
		/// </summary>
		private static void FillFolderList()
		{
			_folderList = _groupedByEntity.Select(grouped => grouped.Key).ToList();
			_folderList.AddRange(
				_groupedByEntityAndEntityId.Select(grouped => $"{grouped.Key.Entity}/{grouped.Key.EntityId}"));
			_folderList.AddRange(_groupedByEntityAndEntityIdAndFileId.Select(grouped =>
				$"{grouped.Key.Entity}/{grouped.Key.EntityId}/{grouped.Key.FileId}"));
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
