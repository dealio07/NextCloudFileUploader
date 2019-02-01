using System;
using System.Collections.Generic;
using System.Linq;

namespace NextCloudFileUploader.Entities
{
	public class EntityFile
	{
		public int Number { get; }
		public string Entity { get; }
		public string EntityId { get; }
		public string FileId { get; }
		public int Version { get; }
		public byte[] Data { get; set; }

		public EntityFile(int number, string entity, Guid entityId, Guid fileId, int version, byte[] data)
		{
			Number = number;
			Entity = entity;
			EntityId = entityId.ToString();
			FileId = fileId.ToString();
			Version = version;
			Data = data;
		}
		
		public EntityFile(int number, string entity, Guid entityId, Guid fileId, int version)
		{
			Number      = number;
			Entity      = entity;
			EntityId    = entityId.ToString();
			FileId      = fileId.ToString();
			Version     = version;
			Data        = null;
		}

		public string GetRemotePath()
		{
			return $@"{Entity}\{EntityId}\{FileId}\{Version}";
		}

		public string GetRemoteFolderPath()
		{
			return $@"{Entity}\{EntityId}\{FileId}\";
		}
		
		public override string ToString()
		{
			return $@"{Entity}: {EntityId}, FileId: {FileId}, Version: {Version}, Data: {Data?.Length.ToString()}";
		}
	}
}
