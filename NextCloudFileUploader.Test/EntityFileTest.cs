using System;
using NextCloudFileUploader.Entities;
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
			var file     = new EntityFile(1, "AccountFile", entityId, fileId, 1, new byte[] {0});

			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.Equal("AccountFile", file.Entity);
			Assert.NotNull(file.EntityId);
			Assert.False(Guid.Empty == Guid.Parse(file.EntityId));
			Assert.NotNull(file.FileId);
			Assert.False(Guid.Empty == Guid.Parse(file.FileId));
			Assert.True(file.Version == 1);
			Assert.NotNull(file.Data);
			Assert.NotEmpty(file.Data);
			Assert.True(file.Data.Length == 1);
		}

		[Fact]
		public void CreatesRemotePath()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile(1, "AccountFile", entityId, fileId, 1, new byte[] {0});

			var remotePath = file.GetRemotePath();
			
			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.NotNull(file.EntityId);
			Assert.NotNull(file.FileId);
			Assert.Equal($"AccountFile/{entityId.ToString()}/{fileId.ToString()}/1", remotePath);
		}

		[Fact]
		public void CreatesRemoteFolderPath()
		{
			var entityId = Guid.NewGuid();
			var fileId   = Guid.NewGuid();
			var file     = new EntityFile(1, "AccountFile", entityId, fileId, 1, new byte[] {0});

			var remoteFolderPath = file.GetRemoteFolderPath();
			
			Assert.NotNull(file);
			Assert.NotNull(file.Entity);
			Assert.NotNull(file.EntityId);
			Assert.NotNull(file.FileId);
			Assert.Equal($"AccountFile/{entityId.ToString()}/{fileId.ToString()}/", remoteFolderPath);
		}
	}
}