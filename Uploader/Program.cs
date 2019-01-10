using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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

		private const string Top = "top(5)";	// TODO: Убрать в проде

		/// <summary>
		/// Провайдер WebDav
		/// </summary>
		private static WebDavProvider _webDavProvider;

		private static void Main(string[] args)
		{
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
					foreach (var entity in Entities)
					{
						_files = GetFiles(entity, connection);
					}

					Parallel.ForEach(_files,
						async (file) => await CreateDirectories(file));

					Parallel.ForEach(_files,
						async (file) => await _webDavProvider.Put(file));

				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
				finally
				{
					Console.WriteLine();
					Console.WriteLine("Дождитесь пока прекратится создание папок и загрузка файлов, затем нажмите любую кнопку, чтобы выйти.");
					Console.ReadKey();
				}
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
		private static List<File> GetFiles(string entity, IDbConnection connection)
		{
			string cmdSqlCommand = "";
			if (entity.Equals("Account") || entity.Equals("Contact"))
				cmdSqlCommand = $"SELECT {Top} f.{entity}Id, f.Id, f.Version, f.Data from [dbo].[{entity}File] f"; // TODO: поменять на выбор всех файлов
			if (entity.Equals("Contract"))
				cmdSqlCommand = $"SELECT {Top} f.{entity}Id, f.Id, fv.PTVersion, fv.PTData from [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv " +
					"WHERE fv.PTFile = f.Id"; // TODO: поменять на выбор всех файлов

			return connection.Query<File>(cmdSqlCommand).ToList();
		}

		/// <summary>
		/// Создает нужные для помещения файла директории
		/// </summary>
		/// <param name="file">Помещаемый файл</param>
		private static async Task CreateDirectories(File file)
		{
			if (file != null && file.DirectoryNames.Count > 0)
			{
				await _webDavProvider.CreateAdditionalDirectories(file.DirectoryNames);
			}
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
