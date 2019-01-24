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

				var cmdSqlCommand = "";
				if (entity.Equals("Account") || entity.Equals("Contact"))
					cmdSqlCommand = 
							/*$@"SELECT {Program.Top} d.Number, d.Entity, d.EntityId, d.FileId, d.Version, f.Data FROM [dbo].[DODocuments] d
								WITH (NOLOCK)
								INNER JOIN [dbo].[{entity}File] f ON f.Id = d.FileId
								WHERE f.{entity}Id = d.EntityId
									AND d.Version = f.Version
									AND d.Entity = '{entity}File'
									AND d.EntityId is not null 
									AND d.FileId is not null 
									AND f.Data is not null 
									AND d.Number >= {fromNumber.ToString()}
								ORDER BY d.Number;"*/
							$@"DECLARE @total INT = 0;
							   SELECT @total = COUNT(*) FROM [dbo].[DODocuments] d WHERE d.Entity = 'AccountFile';
							   DECLARE @num INT = 0;
							   SELECT TOP(1) @num = d.Number FROM [dbo].[DODocuments] d WHERE d.Entity = 'AccountFile';
							   IF (@total < @num)
							   BEGIN
								   SET @total = @num + 1
							   END
							   WHILE (@num < @total)
							   BEGIN
							   SELECT TOP(100) d.Number, d.Entity, d.EntityId, d.FileId, d.Version, af.Data FROM [dbo].[DODocuments] d
								   WITH (NOLOCK)
								   INNER JOIN [dbo].[AccountFile] af ON af.Id = d.FileId
								   WHERE af.AccountId = d.EntityId
									   AND d.Version = af.Version
									   AND d.Entity = 'AccountFile'
									   AND d.EntityId is not null 
									   AND d.FileId is not null 
									   AND af.Data is not null
									   AND d.Number >= @num
								   ORDER BY d.Number ASC;
								   SET @num += 100;
							   END";
				if (entity.Equals("Contract"))
					cmdSqlCommand = 
							/*$@"SELECT {Program.Top} d.Number, d.Entity, d.EntityId, d.FileId, d.Version, fv.PTData as 'Data' FROM [dbo].[DODocuments] d 
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
									AND d.Number >= {fromNumber.ToString()}
								ORDER BY d.Number;"*/
							$@"DECLARE @total INT = 0;
								SELECT @total = COUNT(*) FROM [dbo].[DODocuments] d WHERE d.Entity = 'ContractFile';
								DECLARE @num INT = 0;
								SELECT TOP(1) @num = d.Number FROM [dbo].[DODocuments] d WHERE d.Entity = 'ContractFile';
								IF (@total < @num)
								BEGIN
									SET @total = @num + 1
								END
								WHILE (@num < @total)
								BEGIN
									SELECT TOP(100) d.Number, d.Entity, d.EntityId, d.FileId, d.Version, fv.PTData FROM [dbo].[DODocuments] d WITH (NOLOCK)
										INNER JOIN [dbo].[PTFileVersion] fv ON fv.PTFile = d.FileId
											AND d.Version = fv.PTVersion
										INNER JOIN [dbo].[ContractFile] cf ON cf.ContractId = d.EntityId
										WHERE fv.PTFile = cf.Id
											AND d.Version = fv.PTVersion
											AND d.Entity = 'ContractFile'
											AND d.EntityId is not null 
											AND d.FileId is not null 
											AND cf.Data is not null
											AND d.Number >= @num
										ORDER BY d.Number ASC;
										SET @num += 100;
								END";

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