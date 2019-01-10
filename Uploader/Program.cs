using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace Uploader
{
	class Program
	{
		/// <summary>
		/// Логин на NextCloud
		/// </summary>
		public static string UserName = "varga";    // TODO: Очистить
		/// <summary>
		/// Пароль от аккаунта на NextCloud
		/// </summary>
		public static string Password = "qiz2Zs";	// TODO: Очистить
		/// <summary>
		/// Сущности
		/// </summary>
		private static readonly string[] Entities = {"Account", "Contact", "Contract"};
		/// <summary>
		/// Список файлов
		/// </summary>
		static List<File> _files = new List<File>();
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

		private const string Top = "top(50)";   // TODO: Убрать в проде

		/// <summary>
		/// Провайдер WebDav
		/// </summary>
		private static WebDavProvider _webDavProvider;

		private static void Main(string[] args)
		{
			var watch = System.Diagnostics.Stopwatch.StartNew(); // TODO: Убрать
			_webDavProvider = new WebDavProvider(ServerUrl);

			if (string.IsNullOrEmpty(UserName))
			{
				AskUserForName();
			}

			if (string.IsNullOrEmpty(Password))
			{
				AskUserForPassword();
			}

			using (IDbConnection connection = new SqlConnection(ConnectionString))
			{
				try
				{
					Console.WriteLine("1. Получаем файлы");
					foreach (var entity in Entities)
					{
						_files.AddRange(GetFiles(entity, connection));
					}
					Console.WriteLine("1. Файлы получены");

					Task.Factory.ContinueWhenAll(new[] { CreateDirectories() }, tasks =>
					{
						watch.Stop();
						Console.WriteLine($"2. Папки созданы за: { watch.ElapsedMilliseconds / 1000 }сек");
						watch.Restart();
					})
						.ContinueWith(task => Task.Factory.ContinueWhenAll(new[] { UploadFiles() }, tasks =>
					{
						watch.Stop();
						Console.WriteLine($"3. Файлы загружены за: { watch.ElapsedMilliseconds / 1000 }сек");
					}));

				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
				finally
				{
					//watch.Stop();
					//Console.WriteLine($"Выполнено за: {watch.ElapsedMilliseconds / 1000}сек");
					Console.ReadLine();
				}
			}
		}

		/// <summary>
		/// Выгружает файлы в хранилище
		/// </summary>
		/// <returns></returns>
		private static async Task UploadFiles()
		{
			Console.WriteLine("3. Загружаем файлы");
			foreach (var file in _files)
			{
				await _webDavProvider.Put(file);
				ShowPercentProgress($"Загружаем файл: {file.GetRemotePath()}", _files.IndexOf(file), _files.Count);
			}
		}

		/// <summary>
		/// Создает директории для файлов
		/// </summary>
		/// <returns></returns>
		private static async Task CreateDirectories()
		{
			Console.WriteLine("2. Создаем папки");
			foreach (var file in _files)
			{
				await _webDavProvider.CreateAdditionalDirectories(file.DirectoryNames);
				ShowPercentProgress($"Создаём папку: {file.GetRemoteDirectoryPath()}", _files.IndexOf(file), _files.Count);
			}
		}

		/// <summary>
		/// Показывает прогресс выполняемого процесса
		/// </summary>
		/// <param name="message">Отображаемое сообщение</param>
		/// <param name="processed">Обработано объектов</param>
		/// <param name="total">Общее количество объектов</param>
		static void ShowPercentProgress(string message, long processed, long total)
		{

			var percent = (100 * (processed + 1)) / total;
			Console.Write($"\r{message} {percent : #0.#}% готово");
			if (processed >= total - 1)
			{
				Console.WriteLine(Environment.NewLine);
			}
		}

		/// <summary>
		/// Просит пользователя ввести логин.
		/// </summary>
		private static void AskUserForName()
		{
			Console.WriteLine("Введите логин:");
			UserName = Console.ReadLine();
		}

		/// <summary>
		/// Просит пользователя ввести пароль. Символы пароля маскируются "*".
		/// </summary>
		private static void AskUserForPassword()
		{
			Console.WriteLine("Введите пароль:");
			Password = ReadAndMaskInputPassword();
		}

		/// <summary>
		/// Выполняет запрос в базу
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <param name="connection">Соединение с базой</param>
		/// <returns>Возвращает SqlDataReader</returns>
		private static IEnumerable<File> GetFiles(string entity, IDbConnection connection)
		{
			string cmdSqlCommand = "";
			if (entity.Equals("Account") || entity.Equals("Contact"))
				cmdSqlCommand = $"SELECT {Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', f.Version, f.Data from [dbo].[{entity}File] f " +
					$"WHERE f.{entity}Id is not null and f.Id is not null and f.Data is not null";
			if (entity.Equals("Contract"))
				cmdSqlCommand = $"SELECT {Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', fv.PTVersion as 'Version', fv.PTData as 'Data' from [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv " +
					$"WHERE fv.PTFile = f.Id and f.{entity}Id is not null and f.Id is not null and f.Data is not null";

			return connection.Query<File>(cmdSqlCommand).ToList();
		}

		/// <summary>
		/// Маскирует символы вводимого в консоли пароля символами: "*"
		/// </summary>
		/// <returns>Возвращает пароль</returns>
		private static string ReadAndMaskInputPassword()
		{
			string password = "";
			do
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				// Backspace не должен срабатывать
				if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
				{
					password += key.KeyChar;
					Console.Write("*");
				}
				else
				{
					if (key.Key == ConsoleKey.Backspace && password.Length > 0)
					{
						password = password.Substring(0, (password.Length - 1));
						Console.Write("\b \b");
					}
					else if (key.Key == ConsoleKey.Enter)
					{
						Console.WriteLine();
						break;
					}
				}
			} while (true);

			return password;
		}
	}
}
