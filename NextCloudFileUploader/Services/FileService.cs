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
        /// /// <param name="saveDataAboutUploadedFiles">Флаг, указывающий на необходимость сохранить данные об успешно выгруженных файлах во временную таблицу</param>
        /// /// <param name="totalBytes">Объем всех файлов в байтах</param>
        public async Task<bool> UploadFiles(IEnumerable<EntityFile> fileList, bool saveDataAboutUploadedFiles, long totalBytes)
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
					PrepareForSavingLoadedFiles(file, files);
					ExceptionHandler.LogExceptionToConsole(ex);
					throw ex;
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			}

            if (saveDataAboutUploadedFiles)
				PrepareForSavingAllLoadedFiles(files);

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

		/// <summary>
		/// Сохраняет данные файлов, которые были успешно выгружены в хранилище до ошибки.
		/// </summary>
		/// <param name="entityFile">Последний выгруженный файл</param>
		/// <param name="fileList">Список файлов</param>
		private void SaveLoadedFilesToTempTable(EntityFile entityFile, List<EntityFile> fileList)
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var counter = 0;
				var values = " VALUES";
				var loadedFiles = new List<EntityFile>();
				var lastFileIndex = fileList.FindIndex(f => f.Entity == entityFile.Entity
											 && f.EntityId == entityFile.EntityId
											 && f.FileId == entityFile.FileId
											 && f.Version == entityFile.Version);
				if (lastFileIndex > 0)
					loadedFiles = fileList.GetRange(0, lastFileIndex);

				var dbCommand = _dbConnection.CreateCommand();

				foreach (var f in loadedFiles)
				{
					if (string.IsNullOrEmpty(f.Entity) || string.IsNullOrEmpty(f.EntityId) ||
					    string.IsNullOrEmpty(f.FileId) || string.IsNullOrEmpty(f.Version)) continue;

					values += $" ('{f.Entity}', '{f.EntityId}', '{f.FileId}', '{f.Version}', @Data{counter}),";
					dbCommand.Parameters.Add(new SqlParameter($"@Data{counter}", SqlDbType.VarBinary) { Value = f.Data });
				}

				var valuesWithoutLastComma = values.Remove(values.Length - 1, 1);

				dbCommand.CommandText = $@"BEGIN TRAN
												 BEGIN
													INSERT INTO [dbo].[LastFileUploadedToNextCloud] (Entity, EntityId, FileId, Version, Data)
													{valuesWithoutLastComma}
												 END
												 COMMIT TRAN";

				var result = dbCommand.ExecuteNonQuery();
				if (result != loadedFiles.Count)
					throw new Exception("Сохранились не все выгруженные файлы");
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

        /// <summary>
		/// Сохраняет данные всех файлов, которые были успешно выгружены в хранилище.
		/// </summary>
		/// <param name="fileList">Список всех файлов</param>
		private void SaveAllLoadedFilesToTempTable(List<EntityFile> fileList)
        {
            try
            {
                if ((_dbConnection.State & ConnectionState.Open) == 0)
                    _dbConnection.Open();

	            var counter = 0;
                var values = " VALUES";

				var dbCommand = _dbConnection.CreateCommand();

				foreach (var f in fileList)
                {
	                if (string.IsNullOrEmpty(f.Entity) || string.IsNullOrEmpty(f.EntityId) ||
	                    string.IsNullOrEmpty(f.FileId) || string.IsNullOrEmpty(f.Version)) continue;

	                values += $" ('{f.Entity}', '{f.EntityId}', '{f.FileId}', '{f.Version}', @Data{counter}),";
	                dbCommand.Parameters.Add(new SqlParameter($"@Data{counter}", SqlDbType.VarBinary) { Value = f.Data });
	                counter++;
                }

                var valuesWithoutLastComma = values.Remove(values.Length - 1, 1);

                dbCommand.CommandText = $@"BEGIN TRAN
												 BEGIN
													INSERT INTO [dbo].[LastFileUploadedToNextCloud] (Entity, EntityId, FileId, Version, Data)
													{valuesWithoutLastComma}
												 END
										   COMMIT TRAN";

                var result = dbCommand.ExecuteNonQuery();
                if (result != fileList.Count)
                    throw new Exception("Сохранились не все выгруженные файлы");
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

        /// <summary>
        /// Проверяет наличие записей в таблице файлов, которые были успешно выгружены в хранилище.
        /// </summary>
        /// <returns>Возвращает флаг, пуста ли таблица записей файлов, которые были успешно выгружены в хранилище до ошибки</returns>
        public bool CheckTempTableEntriesCount()
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var dbCommand = _dbConnection.CreateCommand();
				dbCommand.CommandText = @"SELECT COUNT(*) FROM [dbo].[LastFileUploadedToNextCloud] WITH (NOLOCK)";
				var result = dbCommand.ExecuteScalar();
				return int.Parse(result.ToString()) > 0;
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

		/// <summary>
		/// Очищает таблицу записей удачно выгруженных файлов.
		/// </summary>
		internal void DeleteFilesFromTempTable()
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var dbCommand = _dbConnection.CreateCommand();
				dbCommand.CommandText = @"DELETE FROM [dbo].[LastFileUploadedToNextCloud]";
				dbCommand.ExecuteNonQuery();
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

		/// <summary>
		/// Проверяет размер списка файлов и вызывает метод SaveLoadedFilesToTempTable для выполнения Insert частями,
		/// если размер списка больше 500 элементов.
		/// </summary>
		/// <param name="entityFile">Последний выгруженный файл</param>
		/// <param name="fileList">Список файлов</param>
		private void PrepareForSavingLoadedFiles(EntityFile entityFile, List<EntityFile> fileList)
		{
			if (500 <= fileList.Count)
			{
				var lists = Utils.SplitList(fileList, 500);
				foreach (var list in lists)
				{
					SaveLoadedFilesToTempTable(entityFile, list);
				}
			}
			else SaveLoadedFilesToTempTable(entityFile, fileList);
		}
		
		/// <summary>
		/// Проверяет размер списка файлов и вызывает метод SaveAllLoadedFilesToTempTable для выполнения Insert частями,
		/// если размер списка больше 500 элементов.
		/// </summary>
		/// <param name="fileList">Список файлов</param>
		private void PrepareForSavingAllLoadedFiles(List<EntityFile> fileList)
		{
			if (500 <= fileList.Count)
			{
				var lists = Utils.SplitList(fileList, 500);
				foreach (var list in lists)
				{
					SaveAllLoadedFilesToTempTable(list);
				}
			}
			else SaveAllLoadedFilesToTempTable(fileList);
		}
	}
}