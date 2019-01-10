using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

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
		static List<File> files = new List<File>();
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

		private const string Top = "top(5)";	// TODO: Убрать в проде

		/// <summary>
		/// Провайдер WebDav
		/// </summary>
		private static WebDavProvider _webDavProvider;

		static async Task Main(string[] args)
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

			using (var connection = new SqlConnection("Data Source=" + DbServerName + ";Initial Catalog=" + InitialCatalog + ";Trusted_Connection=True;"))
			{
				try
				{
					connection.Open();

					foreach (var entity in Entities)
					{
						using (var reader = InitSqlCommand(entity, connection))
							if (reader != null && !reader.IsClosed && reader.HasRows)
							{
								while (reader.Read())
								{
									FillFileList(entity, reader);
								}
							}
					}

					var result = Parallel.ForEach(files,
						async (file) => await _webDavProvider.Put(file));
					Console.WriteLine($"Status: {result.IsCompleted}");

				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
				finally
				{
					Console.WriteLine();
					Console.ReadLine();
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
		private static SqlDataReader InitSqlCommand(string entity, SqlConnection connection)
		{
			string cmdSqlCommand = "";

			if (entity.Equals("Account") || entity.Equals("Contact"))
				cmdSqlCommand = $"SELECT {Top} f.{entity}Id, f.Id, f.Version, f.Data from [dbo].[{entity}File] f"; // TODO: поменять на выбор всех файлов
			if (entity.Equals("Contract"))
				cmdSqlCommand = $"SELECT {Top} f.{entity}Id, f.Id, fv.PTVersion, fv.PTData from [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv " +
					"WHERE fv.PTFile = f.Id"; // TODO: поменять на выбор всех файлов

			SqlCommand select = new SqlCommand(cmdSqlCommand);
			select.Connection = connection;
			return select.ExecuteReader(System.Data.CommandBehavior.Default);
		}

		/// <summary>
		/// Заполняет список файлов
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <param name="reader">SqlDataReader</param>
		/// <returns></returns>
		private static void FillFileList(string entity, SqlDataReader reader)
		{
			string entityId = reader.GetValue(0).ToString();
			string fileId = reader.GetValue(1).ToString();
			string fileVersion = reader.GetValue(2).ToString();
			byte[] fileData = (byte[]) reader.GetValue(3);

			string directoryName = $"{entity}File/{entityId}/{fileId}";

			List<string> directoryNameList = SplitDirectoryName(directoryName);

			files.Add(new File($"{entity}File", entityId, fileId, fileVersion, fileData, directoryNameList));
		}

		/// <summary>
		/// Создает нужные для помещения файла директории
		/// </summary>
		/// <param name="file">Помещаемый файл</param>
		private static async void CreateDirectories(File file)
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

		/// <summary>
		/// Разделяет имя директории, если оно содержит несколько имен разделённых "/" или "\"
		/// </summary>
		/// <param name="directoryName">Имя нужной директории</param>
		/// <returns>Возвращает список названий папок</returns>
		private static List<string> SplitDirectoryName(string directoryName)
		{
			List<string> directoryNameList = new List<string>();
			if (!string.IsNullOrEmpty(directoryName) && directoryName.Contains("/"))
			{
				var arr = directoryName.Split('/');
				foreach (var str in arr)
				{
					if (str.Contains("\\"))
					{
						var arr1 = directoryName.Split('\\');
						foreach (var str1 in arr1)
						{
							directoryNameList.Add(str1);
						}
					}
					else directoryNameList.Add(str);
				}
			}
			else if (!string.IsNullOrEmpty(directoryName) && directoryName.Contains("\\"))
			{
				var arr = directoryName.Split('\\');
				foreach (var str in arr)
				{
					directoryNameList.Add(str);
				}
			}

			return directoryNameList;
		}
	}

	#region Other methods

	/*
	 // --------------- COPY REQUEST --------------- //

	// Create an HTTP request for the URL.
	HttpWebRequest httpCopyRequest =
	   (HttpWebRequest)WebRequest.Create(szURL1);

	// Set up new credentials.
	httpCopyRequest.Credentials =
	   new NetworkCredential(szUsername, szPassword);

	// Pre-authenticate the request.
	httpCopyRequest.PreAuthenticate = true;

	// Define the HTTP method.
	httpCopyRequest.Method = @"COPY";

	// Specify the destination URL.
	httpCopyRequest.Headers.Add(@"Destination", szURL2);

	// Specify that overwriting the destination is allowed.
	httpCopyRequest.Headers.Add(@"Overwrite", @"T");

	// Retrieve the response.
	HttpWebResponse httpCopyResponse =
	   (HttpWebResponse)httpCopyRequest.GetResponse();

	// Write the response status to the console.
	Console.WriteLine(@"COPY Response: {0}",
	   httpCopyResponse.StatusDescription);

	// --------------- MOVE REQUEST --------------- //

	// Create an HTTP request for the URL.
	HttpWebRequest httpMoveRequest =
	   (HttpWebRequest)WebRequest.Create(szURL2);

	// Set up new credentials.
	httpMoveRequest.Credentials =
	   new NetworkCredential(szUsername, szPassword);

	// Pre-authenticate the request.
	httpMoveRequest.PreAuthenticate = true;

	// Define the HTTP method.
	httpMoveRequest.Method = @"MOVE";

	// Specify the destination URL.
	httpMoveRequest.Headers.Add(@"Destination", szURL1);

	// Specify that overwriting the destination is allowed.
	httpMoveRequest.Headers.Add(@"Overwrite", @"T");

	// Retrieve the response.
	HttpWebResponse httpMoveResponse =
	   (HttpWebResponse)httpMoveRequest.GetResponse();

	// Write the response status to the console.
	Console.WriteLine(@"MOVE Response: {0}",
	   httpMoveResponse.StatusDescription);

	// --------------- GET REQUEST --------------- //

	// Create an HTTP request for the URL.
	HttpWebRequest httpGetRequest =
	   (HttpWebRequest)WebRequest.Create(szURL1);

	// Set up new credentials.
	httpGetRequest.Credentials =
	   new NetworkCredential(szUsername, szPassword);

	// Pre-authenticate the request.
	httpGetRequest.PreAuthenticate = true;

	// Define the HTTP method.
	httpGetRequest.Method = @"GET";

	// Specify the request for source code.
	httpGetRequest.Headers.Add(@"Translate", "F");

	// Retrieve the response.
	HttpWebResponse httpGetResponse =
	   (HttpWebResponse)httpGetRequest.GetResponse();

	// Retrieve the response stream.
	Stream responseStream =
	   httpGetResponse.GetResponseStream();

	// Retrieve the response length.
	long responseLength =
	   httpGetResponse.ContentLength;

	// Create a stream reader for the response.
	StreamReader streamReader =
	   new StreamReader(responseStream, Encoding.UTF8);

	// Write the response status to the console.
	Console.WriteLine(
	   @"GET Response: {0}",
	   httpGetResponse.StatusDescription);
	Console.WriteLine(
	   @"  Response Length: {0}",
	   responseLength);
	Console.WriteLine(
	   @"  Response Text: {0}",
	   streamReader.ReadToEnd());

	// Close the response streams.
	streamReader.Close();
	responseStream.Close();

	// --------------- DELETE REQUEST --------------- //

	// Create an HTTP request for the URL.
	HttpWebRequest httpDeleteFileRequest =
	   (HttpWebRequest)WebRequest.Create(szURL1);

	// Set up new credentials.
	httpDeleteFileRequest.Credentials =
	   new NetworkCredential(szUsername, szPassword);

	// Pre-authenticate the request.
	httpDeleteFileRequest.PreAuthenticate = true;

	// Define the HTTP method.
	httpDeleteFileRequest.Method = @"DELETE";

	// Retrieve the response.
	HttpWebResponse httpDeleteFileResponse =
	   (HttpWebResponse)httpDeleteFileRequest.GetResponse();

	// Write the response status to the console.
	Console.WriteLine(@"DELETE Response: {0}",
	   httpDeleteFileResponse.StatusDescription);
	   */

	#endregion
}
