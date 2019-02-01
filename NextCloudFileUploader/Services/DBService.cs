using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using Dapper;

namespace NextCloudFileUploader.Services
{
	public class DbService
	{
		private string _dbConnectionString;
		
		public DbService(string connectionString)
		{
			_dbConnectionString = connectionString;
		}
		
		// Выбирает из базы все файлы по имени сущности за исключением файлов, загруженных при предыдущем запуске.
		public async Task<IEnumerable<EntityFile>> GetHundredFilesFromDbByEntityAsync(int fromNumber)
		{
			var dbConnection = new SqlConnection(_dbConnectionString);
         			try
         			{
         				if ((dbConnection.State & ConnectionState.Open) == 0)
         					dbConnection.Open();
         
         				var cmdSqlCommand = $@"SELECT TOP(100) * FROM [dbo].[DODocuments]
         				WITH (NOLOCK)
         				WHERE Number >= {fromNumber}
         				ORDER BY Number ASC;";
         
         				return (await dbConnection.QueryAsync<EntityFile>(cmdSqlCommand)).ToList();
         			}
         			catch (Exception ex)
         			{
         				ExceptionHandler.LogExceptionToConsole(ex);
         				throw ex;
         			}
         			finally
         			{
         				if ((dbConnection.State & ConnectionState.Open) != 0) dbConnection.Close();
         			}
         		}
		
		// Выбирает количество файлов (записей в БД)
		public async Task<int> GetFilesCountAsync(int fromNumber)
		{
			var dbConnection = new SqlConnection(_dbConnectionString);
			try
			{
				if ((dbConnection.State & ConnectionState.Open) == 0)
					dbConnection.Open();

				var cmdSqlCommand = $@"SELECT COUNT(*) FROM [dbo].[DODocuments] 
										WHERE Number >= {fromNumber};";

				return await dbConnection.QuerySingleAsync<int>(cmdSqlCommand);
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}
			finally
			{
				if ((dbConnection.State & ConnectionState.Open) != 0) dbConnection.Close();
			}
		}
		
		// Выбирает данные файла
		public async Task<byte[]> GetFileDataAsync(string id, int version, string entity)
		{
			var dbConnection = new SqlConnection(_dbConnectionString);
			try
			{
				if ((dbConnection.State & ConnectionState.Open) == 0)
					dbConnection.Open();

				var table = entity.Equals("ContractFile") ? "PTFileVersion" : $"{entity}";
				var idCol = entity.Equals("ContractFile") ? "PTFile" : "Id";
				var versionCol = entity.Equals("ContractFile") ? "PTVersion" : "Version";
				var dataCol = entity.Equals("ContractFile") ? "PTData" : "Data";
				
				var cmdSqlCommand = $@"SELECT {dataCol} FROM [dbo].[{table}] 
				WITH (NOLOCK)
				WHERE {idCol} = '{id}'
					AND {versionCol} = {version}
					AND DATALENGTH({dataCol}) > 0";

				return await dbConnection.QuerySingleAsync<byte[]>(cmdSqlCommand);
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}
			finally
			{
				if ((dbConnection.State & ConnectionState.Open) != 0) dbConnection.Close();
			}
		}
	}
}