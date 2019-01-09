using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;

namespace Uploader
{
	class Program
	{
		private const string ServerUrl = "https://cloud.rozetka.ua/remote.php/webdav/"; // Адрес удаленного хранилища
		private const string DBServerName = "crm-dev";	// Имя сервера базы
		private const string InitialCatalog = "DOdevVarha"; // Имя базы

		private static string _userName = ""; // Логин на NextCloud
		private static string _password = ""; // Пароль от аккаунта на NextCloud
		private static SqlDataReader _reader;

		static async Task Main(string[] args)
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();

			if (string.IsNullOrEmpty(_userName))
			{
				Console.WriteLine("Введите логин:");
				_userName = Console.ReadLine();
			}

			if (string.IsNullOrEmpty(_password))
			{
				Console.WriteLine("Введите пароль:");
				_password = MaskPassword();
			}

			using (var connection = new SqlConnection("Data Source=" + DBServerName + ";Initial Catalog=" + InitialCatalog + ";Trusted_Connection=True;"))
			{
				try
				{
					connection.Open();

					string entity = "Contract";
					string cmdSqlCommand =
						"SELECT top(10) cf." + entity + "Id, cf.Id, ptfv.PTVersion, ptfv.PTData from [dbo].[" + entity + "File] cf , [dbo].PTFileVersion ptfv " +
						"WHERE ptfv.PTFile = cf.Id"; // TODO: поменять на выбор всех файлов

					SqlCommand select = new SqlCommand(cmdSqlCommand);
					select.Connection = connection;

					_reader = select.ExecuteReader(System.Data.CommandBehavior.Default);

					if (_reader != null && !_reader.IsClosed && _reader.HasRows)
					{
						while (_reader.Read())
						{
							string entityId = _reader.GetValue(0).ToString();
							string fileId = _reader.GetValue(1).ToString();
							string fileVersion = _reader.GetValue(2).ToString();
							byte[] fileData = (byte[]) _reader.GetValue(3);

							string directoryName = entity + "/" + entityId + "/" + fileId;
							string remoteDirectoryPath = "";

							List<string> directoryNameList = SplitDirectoryName(directoryName);


							if (directoryNameList.Count > 0)
									remoteDirectoryPath = await CreateAdditionalDirectories(directoryNameList);

							if (string.IsNullOrEmpty(directoryName))
							{
								Console.WriteLine("Сохраняем в основной папке.");
							}

							// PUT file(s)
							await Put(entity, entityId, fileId, fileVersion, fileData, remoteDirectoryPath, directoryNameList);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
				finally
				{
					watch.Stop();
					var elapsedMs = watch.ElapsedMilliseconds;
					Console.WriteLine();
					Console.WriteLine($"Прошло времени: {elapsedMs}");
					Console.WriteLine("Нажмите Enter, чтобы закрыть программу.");
					Console.ReadLine();
				}
			}
		}

		/// <summary>
		/// Создает дополнительные директории из списка directoryNameList
		/// </summary>
		private static async Task<string> CreateAdditionalDirectories(List<string> directoryNameList)
		{
			string remoteDirectoryPath = "";
			foreach (var directoryName in directoryNameList)
			{
				await MKCOL(ServerUrl + remoteDirectoryPath, directoryName);
				if (!remoteDirectoryPath.Contains(directoryName))
					remoteDirectoryPath += directoryName + "/";
			}
			
			return remoteDirectoryPath;
		}

		/// <summary>
		/// Маскирует символы вводимого в консоли пароля символами: "*"
		/// </summary>
		/// <returns>Возвращает пароль</returns>
		private static string MaskPassword()
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

		/// <summary>
		/// Помещает файл в файловое хранилище
		/// </summary>
		/// <param name="fileId">ID файла</param>
		/// <param name="fileData">Файл в битовом массиве</param>
		/// <param name="fileVersion">Версия файла</param>
		/// <param name="fileExtension">Расширение файла</param>
		private static async Task Put(string entity, string entityId, string fileId, string fileVersion, byte[] fileData, string remoteDirectoryPath, List<string> directoryNameList)
		{
			try
			{
				// Create an HTTP request for the URL.
				HttpWebRequest httpPutRequest =
			   (HttpWebRequest)WebRequest.Create(ServerUrl + remoteDirectoryPath + fileVersion);

				// Set up new credentials.
				httpPutRequest.Credentials =
				   new NetworkCredential(_userName, _password);

				// Pre-authenticate the request.
				httpPutRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpPutRequest.Method = @"PUT";

				// Specify that overwriting the destination is allowed.
				httpPutRequest.Headers.Add(@"Overwrite", @"T");

				// Specify the content length.
				//httpPutRequest.ContentLength = _fileInfo.Length;
				httpPutRequest.ContentLength = fileData.Length;

				// Retrieve the request stream.
				using (var requestStream = httpPutRequest.GetRequestStream())
				{
					await requestStream.WriteAsync(fileData, 0, fileData.Length);
					// Close the request stream.
					//requestStream.Close();
				}

				// Retrieve the response.
				HttpWebResponse httpPutResponse = (HttpWebResponse)(await httpPutRequest.GetResponseAsync());

				if (httpPutResponse != null)
				{
					// Write the response status to the console.
					Console.WriteLine(@"Загрузка файла: {0}",
						httpPutResponse.StatusDescription);
				}
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					if (!ex.Message.Contains("404"))
					{
						Console.WriteLine();
						Console.WriteLine("Ошибка: " + ex.Message);
					}
					else
						Console.WriteLine("Конечная папка не найдена. Создаем папку");
				}

				if (ex.Message.Contains("404"))
				{
					if (directoryNameList.Count > 0)
					{
						await CreateAdditionalDirectories(directoryNameList);
					}
					else
					{
						await MKCOL(ServerUrl, remoteDirectoryPath, entity, entityId, fileId, fileVersion, fileData, directoryNameList);
					}
				}
			}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания директории в хранилище
		/// </summary>
		/// <param name="url">Адрес сервера</param>
		/// <param name="remoteDirectoryPath">Адрес директории</param>
		/// <param name="fileId">ID файла</param>
		/// <param name="fileData">Файл в битовом массиве</param>
		/// <param name="fileVersion">Версия файла</param>
		/// <param name="fileExtension">Расширение файла</param>
		private static async Task MKCOL(string url, string remoteDirectoryPath, string entity, string entityId, string fileId, string fileVersion, byte[] fileData, List<string> directoryNameList)
		{
			try
			{
				// Create an HTTP request for the URL.
				HttpWebRequest httpMkColRequest =
					(HttpWebRequest)WebRequest.Create(url + remoteDirectoryPath);

				// Set up new credentials.
				httpMkColRequest.Credentials =
					new NetworkCredential(_userName, _password);

				// Pre-authenticate the request.
				httpMkColRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpMkColRequest.Method = @"MKCOL";

				// Retrieve the response.
				HttpWebResponse httpMkColResponse =
					(HttpWebResponse)(await httpMkColRequest.GetResponseAsync());

				// Write the response status to the console.
				Console.WriteLine();
				Console.WriteLine(@"Создание папки: {0}",
					httpMkColResponse.StatusDescription);

				if (httpMkColResponse.StatusCode == HttpStatusCode.Created && fileData.Length > 0)
					await Put(entity, entityId, fileId, fileVersion, fileData, remoteDirectoryPath, directoryNameList);
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw ex;
				}
			}
		}

		/// <summary>
		/// Создает и выполняет запрос для создания директории в хранилище
		/// </summary>
		/// <param name="url">Адрес сервера</param>
		/// <param name="remoteDirectoryPath">Адрес директории</param>
		private static async Task MKCOL(string url, string remoteDirectoryPath)
		{
			try
			{
				// Create an HTTP request for the URL.
				HttpWebRequest httpMkColRequest =
					(HttpWebRequest)WebRequest.Create(url + remoteDirectoryPath);

				// Set up new credentials.
				httpMkColRequest.Credentials =
					new NetworkCredential(_userName, _password);

				// Pre-authenticate the request.
				httpMkColRequest.PreAuthenticate = true;

				// Define the HTTP method.
				httpMkColRequest.Method = @"MKCOL";

				// Retrieve the response.
				HttpWebResponse httpMkColResponse =
					(HttpWebResponse)(await httpMkColRequest.GetResponseAsync());

				// Write the response status to the console.
				Console.WriteLine();
				Console.WriteLine(@"Создание папки: {0}",
					httpMkColResponse.StatusDescription);
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("405"))
				{
					throw ex;
				}
			}
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
