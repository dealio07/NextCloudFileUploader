using System;
using System.Collections.Generic;
using System.Linq;

namespace NextCloudFileUploader
{
	public class EntityFile
	{
		public string Entity { get; }
		public string EntityId { get; }
		public string FileId { get; }
		public string Version { get; }
		public byte[] Data { get; }
		public List<string> FolderNames { get; }

		public EntityFile(string entity, string entityId, string fileId, string version, byte[] data)
		{
			Entity = entity;
			EntityId = entityId;
			FileId = fileId;
			Version = version;
			Data = data;
			FolderNames = SplitFolderName($"{entity}/{entityId}/{fileId}/");
		}

		public EntityFile(string entity, Guid entityId, Guid fileId, int version, byte[] data)
		{
			Entity = entity; 
			EntityId = entityId.ToString();
			FileId = fileId.ToString();
			Version = version.ToString();
			Data = data;
			FolderNames = SplitFolderName(GetRemoteFolderPath());
		}

		public string GetRemotePath()
		{
			return $"{Entity}/{EntityId}/{FileId}/{Version}";
		}

		public string GetRemoteFolderPath()
		{
			return $"{Entity}/{EntityId}/{FileId}/";
		}

		/// <summary>
		/// Разделяет имя директории, если оно содержит несколько имен разделённых "/" или "\"
		/// </summary>
		/// <param name="folderName">Имя нужной директории</param>
		/// <returns>Возвращает список названий папок</returns>
		private List<string> SplitFolderName(string folderName)
		{
			var folderNameList = new List<string>();
			if (string.IsNullOrEmpty(folderName)) return folderNameList;
			if (folderName.Contains("/"))
			{
				var arr = folderName.Split('/');
				foreach (var str in arr)
				{
					if (string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str)) continue;
					if (str.Contains("\\"))
					{
						var arr1 = folderName.Split('\\');
						folderNameList.AddRange(arr1.Where(str1 => !string.IsNullOrEmpty(str1) && !string.IsNullOrWhiteSpace(str1)));
					}
					else folderNameList.Add(str);
				}
			}
			else if (folderName.Contains("\\"))
			{
				var arr = folderName.Split('\\');
				folderNameList.AddRange(arr.Where(str => !string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str)));
			}

			return folderNameList;
		}

		public override string ToString()
		{
			return $"{Entity}: {EntityId},\nFileId: {FileId},\nVersion: {Version},\nData: {Data?.Length}";
		}
	}
}
