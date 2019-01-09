using System.Collections.Generic;

namespace Uploader
{
	class File
	{
		public string Entity { get; }
		public string EntityId { get; }
		public string FileId { get; }
		public string VersionLikeName { get; }
		public byte[] Data { get; }
		public List<string> DirectoryNames { get; }

		public File(string entity, string entityId, string fileId, string versionLikeName, byte[] data, List<string> directoryNames)
		{
			Entity = entity;
			EntityId = entityId;
			FileId = fileId;
			VersionLikeName = versionLikeName;
			Data = data;
			DirectoryNames = directoryNames;
		}

		public string GetRemotePath()
		{
			return $"{Entity}/{EntityId}/{FileId}/{VersionLikeName}";
		}

		public string GetRemoteDirectoryPath()
		{
			return $"{Entity}/{EntityId}/{FileId}/";
		}
	}
}
