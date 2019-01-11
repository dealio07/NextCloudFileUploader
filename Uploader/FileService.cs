using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace Uploader
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
		public async Task UploadFiles(IEnumerable<File> fileList)
		{
			long current = 0;
			var enumerable = fileList.ToList();
			Parallel.ForEach(enumerable, async (file, state, s) =>
			{
				try
				{
					await _webDavProvider.Put(file);
					Utils.ShowPercentProgress("3. Загружаем файлы", current, enumerable.Count);
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			});
		}

		/// <summary>
		/// Выполняет запрос в базу
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <param name="connection">Соединение с базой</param>
		/// <returns>Возвращает SqlDataReader</returns>
		public IEnumerable<File> GetFilesFromDb(string entity, IDbConnection connection)
		{
			string cmdSqlCommand = "";
			if (entity.Equals("Account") || entity.Equals("Contact"))
				cmdSqlCommand = $"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', f.Version, f.Data from [dbo].[{entity}File] f " +
				                $"WHERE f.{entity}Id is not null and f.Id is not null and f.Data is not null";
			if (entity.Equals("Contract"))
				cmdSqlCommand = $"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', fv.PTVersion as 'Version', fv.PTData as 'Data' from [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv " +
				                $"WHERE fv.PTFile = f.Id and f.{entity}Id is not null and f.Id is not null and f.Data is not null";

			return connection.Query<File>(cmdSqlCommand).ToList();
		}
	}
}