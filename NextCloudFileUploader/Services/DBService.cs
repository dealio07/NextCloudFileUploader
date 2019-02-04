using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using NextCloudFileUploader.Entities;
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
		
		// Selects top one hundred files from DB.
		public List<EntityFile> GetHundredFilesFromDbAsync(int fromNumber)
		{
			using (var dbConnection = new SqlConnection(_dbConnectionString))
			{
				if ((dbConnection.State & ConnectionState.Open) == 0)
					dbConnection.Open();

				var cmdSqlCommand = $@"SELECT TOP(100) * FROM [dbo].[DODocuments]
         				WITH (NOLOCK)
         				WHERE Number >= {fromNumber}
         				ORDER BY Number ASC;";

				return dbConnection.Query<EntityFile>(cmdSqlCommand).ToList();
			}
		}

		// Selects Data for file from DB.
		public byte[] GetFileDataAsync(string id, int version, string entity)
		{
			using (var dbConnection = new SqlConnection(_dbConnectionString))
			{
				if ((dbConnection.State & ConnectionState.Open) == 0)
					dbConnection.Open();

				var table      = entity.Equals("ContractFile") ? "PTFileVersion" : $"{entity}";
				var idCol      = entity.Equals("ContractFile") ? "PTFile" : "Id";
				var versionCol = entity.Equals("ContractFile") ? "PTVersion" : "Version";
				var dataCol    = entity.Equals("ContractFile") ? "PTData" : "Data";

				var cmdSqlCommand = $@"SELECT {dataCol} FROM [dbo].[{table}] 
				WITH (NOLOCK)
				WHERE {idCol} = '{id}'
					AND {versionCol} = {version}
					AND DATALENGTH({dataCol}) > 0";

				return dbConnection.QuerySingle<byte[]>(cmdSqlCommand);
			}
		}
	}
}