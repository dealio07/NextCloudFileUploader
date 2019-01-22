using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;

namespace NextCloudFileUploader.Services
{
	public class FileService
	{
		private WebDavProvider _webDavProvider;
		private IDbConnection _dbConnection;

		public FileService(WebDavProvider webDavProvider, IDbConnection dbConnection)
		{
			_webDavProvider = webDavProvider;
			_dbConnection = dbConnection;
		}

        /// <summary>
        /// Выгружает файлы в хранилище.
        /// </summary>
        /// <param name="fileList">Список файлов, которые следует выгрузить</param>
        /// /// <param name="totalBytes">Объем всех файлов в байтах</param>
        public async Task<bool> UploadFiles(IEnumerable<EntityFile> fileList, long totalBytes)
		{
			var files = fileList.ToList();
			var current = 0;
			var uploadedBytes = 0;

			foreach (var file in files)
			{
				try
				{
					uploadedBytes += file.Data.Length;
					var result = await _webDavProvider.PutWithHttp(file, current, files.Count, uploadedBytes, totalBytes);
				}
				catch (Exception ex)
				{
					// TODO: Добавить логирование при ошибке
					ExceptionHandler.LogExceptionToConsole(ex);
					throw ex;
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			}

			return true;
		}

		/// <summary>
		/// Выбирает из базы все файлы по имени сущности за исключением файлов, загруженных при предыдущем запуске.
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <returns>Возвращает файлы для выгрузки в хранилище</returns>
		public IEnumerable<EntityFile> GetFilesFromDb(string entity)
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var cmdSqlCommand = "";
				if (entity.Equals("Account") || entity.Equals("Contact"))
					cmdSqlCommand =
						$@"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', f.Version, f.Data FROM [dbo].[{entity}File] f 
						WITH (NOLOCK) 
						WHERE f.{entity}Id is not null AND f.Id is not null AND f.Data is not null AND
						(f.{entity}Id not in (SELECT EntityId FROM [dbo].[LastFileUploadedToNextCloud] WHERE EntityId = f.{entity}Id AND FileId = f.Id AND Version = f.Version))
						ORDER BY f.CreatedOn";
				if (entity.Equals("Contract"))
					cmdSqlCommand =
						$@"SELECT {Program.Top} '{entity}File' as Entity, f.{entity}Id as 'EntityId', f.Id as 'FileId', fv.PTVersion as 'Version', fv.PTData as 'Data' FROM [dbo].[{entity}File] f, [dbo].[PTFileVersion] fv 
						WITH (NOLOCK) 
						WHERE fv.PTFile = f.Id AND f.{entity}Id is not null AND f.Id is not null AND f.Data is not null AND 
						(f.{entity}Id not in (SELECT EntityId FROM [dbo].[LastFileUploadedToNextCloud] WHERE EntityId = f.{entity}Id AND FileId = f.Id AND Version = f.Version))
						ORDER BY f.CreatedOn";

				return _dbConnection.Query<EntityFile>(cmdSqlCommand).ToList();
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}
			finally
			{
				if ((_dbConnection.State & ConnectionState.Open) != 0) _dbConnection.Close();
			}
		}
	}
}