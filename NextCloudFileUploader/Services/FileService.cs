using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;
using log4net;

namespace NextCloudFileUploader.Services
{
	public class FileService
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
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
					Log.Error($"Ошибка при выгрузке файла #{file.Number}");
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
		/// <param name="fromNumber">Порядковый номер записи, номер файла в таблице,
		/// с которого будет произведена выборка</param>
		/// <returns>Возвращает файлы для выгрузки в хранилище</returns>
		public IEnumerable<EntityFile> GetFilesFromDb(string entity, int fromNumber)
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var fromString = fromNumber.ToString();
				var cmdSqlCommand = "";
				if (entity.Equals("Account") || entity.Equals("Contact"))
					cmdSqlCommand = 
							$@"SELECT TOP(100) d.Number, d.Entity, d.EntityId, d.FileId, d.Version, af.Data 
								FROM [dbo].[DODocuments] d
								WITH (NOLOCK)
								INNER JOIN [dbo].[{entity}File] af ON af.Id = d.FileId
								WHERE af.{entity}Id = d.EntityId
									AND d.Version = af.Version
									AND d.Entity = '{entity}File'
									AND d.EntityId is not null 
									AND d.FileId is not null 
									AND af.Data is not null
									AND d.Number >= {fromString}
								ORDER BY d.Number ASC;";
				if (entity.Equals("Contract"))
					cmdSqlCommand = 
							$@"SELECT TOP(100) d.Number, d.Entity, d.EntityId, d.FileId, d.Version, fv.PTData as 'Data' 
								FROM [dbo].[DODocuments] d 
								WITH (NOLOCK)
								INNER JOIN [dbo].[PTFileVersion] fv ON fv.PTFile = d.FileId
									AND d.Version = fv.PTVersion
								INNER JOIN [dbo].[{entity}File] cf ON cf.{entity}Id = d.EntityId
								WHERE fv.PTFile = cf.Id
									AND d.Version = fv.PTVersion
									AND d.Entity = '{entity}File'
									AND d.EntityId is not null 
									AND d.FileId is not null 
									AND cf.Data is not null
									AND d.Number >= {fromString}
								ORDER BY d.Number ASC;";

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
		
		public int GetFilesCount(string entity)
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var cmdSqlCommand = $@"SELECT COUNT(*) FROM [dbo].[DODocuments] d WHERE d.Entity = '{entity}File';";

				return _dbConnection.QuerySingle<int>(cmdSqlCommand);
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