using System;
using System.Collections.Generic;
using System.Linq;

namespace Uploader
{
	public class File
	{
		public string Entity { get; set; }
		public string EntityId { get; set; }
		public string FileId { get; set; }
		public string Version { get; set; }
		public byte[] Data { get; set; }
		public List<string> DirectoryNames { get; }

		public File(Guid entityId, Guid fileId, Int32 version, Byte[] data)
		{
			EntityId = entityId.ToString();
			FileId = fileId.ToString();
			Version = version.ToString();
			Data = data;
			DirectoryNames = SplitDirectoryName(GetRemoteDirectoryPath());
		}

		public string GetRemotePath()
		{
			return $"{Entity}/{EntityId}/{FileId}/{Version}";
		}

		public string GetRemoteDirectoryPath()
		{
			return $"{Entity}/{EntityId}/{FileId}/";
		}

		/// <summary>
		/// Разделяет имя директории, если оно содержит несколько имен разделённых "/" или "\"
		/// </summary>
		/// <param name="directoryName">Имя нужной директории</param>
		/// <returns>Возвращает список названий папок</returns>
		private List<string> SplitDirectoryName(string directoryName)
		{
			var directoryNameList = new List<string>();
			if (string.IsNullOrEmpty(directoryName)) return directoryNameList;
			if (directoryName.Contains("/"))
			{
				var arr = directoryName.Split('/');
				foreach (var str in arr)
				{
					if (string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str)) continue;
					if (str.Contains("\\"))
					{
						var arr1 = directoryName.Split('\\');
						directoryNameList.AddRange(arr1.Where(str1 => !string.IsNullOrEmpty(str1) && !string.IsNullOrWhiteSpace(str1)));
					}
					else directoryNameList.Add(str);
				}
			}
			else if (directoryName.Contains("\\"))
			{
				var arr = directoryName.Split('\\');
				directoryNameList.AddRange(arr.Where(str => !string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str)));
			}

			return directoryNameList;
		}
	}
}
