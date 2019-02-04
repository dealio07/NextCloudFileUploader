using System;
using System.Linq;
using NextCloudFileUploader.WebDav;

namespace NextCloudFileUploader.Services
{
	public class FolderService
	{
		private WebDavProvider _webDavProvider;

		public FolderService(WebDavProvider webDavProvider)
		{
			_webDavProvider = webDavProvider;
		}

		// Creates directories in cloud by path string.
		public void CreateFolders(string folderNames, string rootFolder)
		{
			var folders = folderNames.Split(new[] {@"/"}, StringSplitOptions.None).ToList();
			if (!string.IsNullOrEmpty(rootFolder))
				folders.Insert(0, rootFolder);
			var folderPath = string.Empty;
			foreach (var folderName in folders)
			{
				folderPath += $@"/{folderName}";
				_webDavProvider.CreateDirectory(_webDavProvider.ServerUrl, folderPath).Wait();
			}
		}
	}
}