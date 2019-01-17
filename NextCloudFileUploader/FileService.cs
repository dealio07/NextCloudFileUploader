using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NextCloudFileUploader
{
	public class FileService
	{
		private WebDavProvider _webDavProvider;
		private IDbConnection _dbConnection;
		private File _lastFile;

		public FileService(WebDavProvider webDavProvider, IDbConnection dbConnection)
		{
			_webDavProvider = webDavProvider;
			_dbConnection = dbConnection;
		}

		/// <summary>
		/// Выгружает файлы в хранилище
		/// </summary>
		/// <returns></returns>
		public async Task<bool> UploadFiles(IEnumerable<File> fileList)
		{
			var enumerable = fileList.ToList();
			var current = 0;

			//if (_lastFile == null)
			//	_lastFile = GetLastFileFromDb();

			//if (_lastFile != null)
			//{
			//	var count = enumerable.Count;
			//	var lastFileIndex = enumerable.FindIndex(file => file.Entity == _lastFile.Entity
			//								 && file.EntityId == _lastFile.EntityId
			//								 && file.FileId == _lastFile.FileId
			//								 && file.Version == _lastFile.Version);
			//	if (lastFileIndex > 0)
			//		enumerable = enumerable.GetRange(lastFileIndex, count - lastFileIndex);
			//}

			foreach (var file in enumerable)
			{
				try
				{
					//if (current == 3)
					//	throw new Exception("Тест сохранения");
					var result = await _webDavProvider.PutWithHttp(file, current, enumerable.Count);
				}
				catch (Exception ex)
				{
					SavePositionToDb(file, fileList);
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

			DeleteLastFileFromDb();

			return true;

		}

		/// <summary>
		/// Выполняет запрос в базу
		/// </summary>
		/// <param name="entity">Название сущности</param>
		/// <param name="connection">Соединение с базой</param>
		/// <returns>Возвращает SqlDataReader</returns>
		public IEnumerable<File> GetFilesFromDb(string entity)
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

				return _dbConnection.Query<File>(cmdSqlCommand).ToList();
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				if ((_dbConnection.State & ConnectionState.Open) != 0) _dbConnection.Close();
			}
		}

		/// <summary>
		/// Сохраняет данные файла, на котором прервалась загрузка
		/// </summary>
		/// <param name="file">Файл</param>
		/// <param name="connection">Соединение с базой данных</param>
		/// <returns>Возвращает количество измененных строк в таблице (0 или 1)</returns>
		private int SavePositionToDb(File file, List<File> fileList)
		{
			try
			{
				//var command = $@"BEGIN TRAN
				//					 IF EXISTS (SELECT f.* FROM [dbo].[LastFileUploadedToNextCloud] f WITH (UPDLOCK,SERIALIZABLE) 
				//							    WHERE f.Version LIKE '%')
				//					 BEGIN
				//					 UPDATE [dbo].[LastFileUploadedToNextCloud] 
				//							SET Entity =	'{file.Entity}', 
				//								EntityId =	'{file.EntityId}', 
				//								FileId =	'{file.FileId}', 
				//								Version =	'{file.Version}',
				//								Data =		@Data
				//							WHERE Version LIKE '%'
				//					 END
				//					 ELSE
				//					 BEGIN
				//						INSERT INTO [dbo].[LastFileUploadedToNextCloud] (Entity, EntityId, FileId, Version, Data)
				//						VALUES ('{file.Entity}', '{file.EntityId}', '{file.FileId}', '{file.Version}', @Data)
				//					 END
				//					 COMMIT TRAN";
				var values = "";
				var notLoadedFiles = new List<File>();
				var count = fileList.Count;
				var lastFileIndex = fileList.FindIndex(f => f.Entity == file.Entity
											 && f.EntityId == file.EntityId
											 && f.FileId == file.FileId
											 && f.Version == file.Version);
				if (lastFileIndex > 0)
					notLoadedFiles = fileList.GetRange(lastFileIndex, count - lastFileIndex);

				foreach (var f in notLoadedFiles)
				{
					if (f.Data != null)
					{
						values += $"VALUES ('{f.Entity}', '{f.EntityId}', '{f.FileId}', '{f.Version}', 0x00),";
					}
				}
				var command = $@"BEGIN TRAN
									 BEGIN
										INSERT INTO [dbo].[LastFileUploadedToNextCloud] (Entity, EntityId, FileId, Version, Data)
										{values}
									 END
									 COMMIT TRAN";
				//TODO: Сделать выгрузку кучи файлов
				using (SqlCommand _cmd = new SqlCommand(command, (SqlConnection)_dbConnection))
				{
					SqlParameter param = _cmd.Parameters.Add("@Data", SqlDbType.VarBinary);
					param.Value = file.Data;

					_dbConnection.Open();
					var result = _cmd.ExecuteNonQuery();
					return result;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				if ((_dbConnection.State & ConnectionState.Open) != 0) _dbConnection.Close();
			}
		}

		/// <summary>
		/// Достает из базы данных последний файл, на котором прервалась загрузка
		/// </summary>
		/// <returns>Возвращает последний файл, на котором прервалась загрузка</returns>
		private File GetLastFileFromDb()
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var cmd = $@"
				SELECT f.Entity as 'Entity', f.EntityId as 'EntityId', f.FileId as 'FileId', f.Version as 'Version', f.Data as 'Data' FROM [dbo].[LastFileUploadedToNextCloud] f 
				WITH (NOLOCK) 
				WHERE f.Version LIKE '%' and f.Id is not null and f.Data is not null";

				var file = _dbConnection.QuerySingleOrDefault<File>(cmd);

				return file;
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				if ((_dbConnection.State & ConnectionState.Open) != 0) _dbConnection.Close();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private int DeleteLastFileFromDb()
		{
			try
			{
				if ((_dbConnection.State & ConnectionState.Open) == 0)
					_dbConnection.Open();

				var command = _dbConnection.CreateCommand();
				command.CommandText = @"DELETE FROM [dbo].[LastFileUploadedToNextCloud] WHERE Version LIKE '%'";
				return command.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				if ((_dbConnection.State & ConnectionState.Open) != 0) _dbConnection.Close();
			}
		}
	}
}