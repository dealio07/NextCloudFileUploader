using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NextCloudFileUploader
{
	public class FileService
	{
		private WebDavProvider _webDavProvider;

		public FileService(WebDavProvider webDavProvider)
		{
			_webDavProvider = webDavProvider;
		}

		/// <summary>
		/// Выгружает файлы в хранилище
		/// </summary>
		/// <returns></returns>
		public async Task<bool> UploadFiles(IEnumerable<File> fileList)
		{

			var enumerable = fileList.ToList();
			long current = 0;
			foreach (var file in enumerable)
			{
				try
				{
					var result = await _webDavProvider.PutWithHttp(file);
					Utils.ShowPercentProgress("3. Загружаем файлы", current, enumerable.Count);
					
				}
				catch (Exception ex)
				{
					Console.WriteLine("\nОшибка в методе UploadFiles");
					Console.WriteLine($"Ошибка: {ex.Message}");
					Console.WriteLine($"Стек ошибки: {ex.StackTrace}");
					throw;
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			}

			return true;

		}

		/// <summary>
		/// Выполняет запрос в базу
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <param name="connection">Соединение с базой</param>
		/// <returns>Возвращает SqlDataReader</returns>
		public IEnumerable<File> GetFilesFromDb(string entity, IDbConnection connection)
		{
			var cmdSqlCommand = "";
			if (entity.Equals("Account") || entity.Equals("Contact"))
				cmdSqlCommand =
					$"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', f.Version, f.Data from [dbo].[{entity}File] f WITH (NOLOCK) " +
					$"WHERE f.{entity}Id is not null and f.Id is not null and f.Data is not null " +
					$"ORDER BY f.{entity}Id DESC, f.Id DESC";
			if (entity.Equals("Contract"))
				cmdSqlCommand = 
					$"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', fv.PTVersion as 'Version', fv.PTData as 'Data' from [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv WITH (NOLOCK) " +
				    $"WHERE fv.PTFile = f.Id and f.{entity}Id is not null and f.Id is not null and f.Data is not null " +
					$"ORDER BY f.{entity}Id DESC, f.Id DESC";

			return connection.Query<File>(cmdSqlCommand).ToList();
		}

		public string SavePositionToDB(File file, IDbConnection connection)
		{
			var command = connection.CreateCommand();
			command.CommandText = "UPDATE";
			return "";
		}
	}
}