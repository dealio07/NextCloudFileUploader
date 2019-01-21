using System;
using System.Collections.Generic;
using Xunit;

namespace NextCloudFileUploader.Test
{
	public class EntityFileTest
	{
		[Fact]
		public void Creates()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile("AccountFile", entityId, fileId, 1, new byte[] {0});

			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.True(file.Entity.Equals("AccountFile"));
			Assert.NotNull(file.EntityId);
			Assert.False(Guid.Empty == Guid.Parse(file.EntityId));
			Assert.NotNull(file.FileId);
			Assert.False(Guid.Empty == Guid.Parse(file.FileId));
			Assert.NotNull(file.Version);
			Assert.True(file.Version == "1");
			Assert.NotNull(file.Data);
			Assert.NotEmpty(file.Data);
			Assert.True(file.Data.Length == 1);
		}

		[Fact]
		public void CreatesWithStrings()
		{
			var entityId = Guid.NewGuid().ToString();
			var fileId   = Guid.NewGuid().ToString();
			var file     = new EntityFile("AccountFile", entityId, fileId, "1", new byte[] {0});

			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.True(file.Entity.Equals("AccountFile"));
			Assert.NotNull(file.EntityId);
			Assert.False(Guid.Empty == Guid.Parse(file.EntityId));
			Assert.NotNull(file.FileId);
			Assert.False(Guid.Empty == Guid.Parse(file.FileId));
			Assert.NotNull(file.Version);
			Assert.True(file.Version == "1");
			Assert.NotNull(file.Data);
			Assert.NotEmpty(file.Data);
			Assert.True(file.Data.Length == 1);
		}

		[Fact]
		public void CreatesRemotePath()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile("AccountFile", entityId, fileId, 1, new byte[] {0});

			var remotePath = file.GetRemotePath();
			
			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.NotNull(file.EntityId);
			Assert.NotNull(file.FileId);
			Assert.NotNull(file.Version);
			Assert.Equal(remotePath, $"AccountFile/{entityId}/{fileId}/1");
		}

		[Fact]
		public void CreatesRemoteFolderPath()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile("AccountFile", entityId, fileId, 1, new byte[] {0});

			var remoteFolderPath = file.GetRemoteFolderPath();
			
			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.NotNull(file.EntityId);
			Assert.NotNull(file.FileId);
			Assert.Equal(remoteFolderPath, $"AccountFile/{entityId}/{fileId}/");
		}

		[Fact]
		public void SplitsFolderName()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile("AccountFile", entityId, fileId, 1, new byte[] {0});

			Assert.NotNull(file);
			Assert.NotNull(file.FolderNames);
			Assert.NotEmpty(file.FolderNames);
			Assert.Equal(file.FolderNames[0], "AccountFile");
			Assert.Equal(file.FolderNames[1], entityId.ToString());
			Assert.Equal(file.FolderNames[2], fileId.ToString());
		}
	}
}