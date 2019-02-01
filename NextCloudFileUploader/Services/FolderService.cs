using System;
using System.Threading;
using System.Threading.Tasks;
using NextCloudFileUploader.Utilities;
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

		// Создает папки из сгруппированного списка
		public async Task<bool> CreateFolders(string folderNames)
		{
			try
			{
				var folders = folderNames.Split(new []{@"\"}, StringSplitOptions.None);
				var folderPath = string.Empty;
				foreach (var folderName in folders)
				{
					folderPath += $@"\{folderName}";
					await _webDavProvider.Mkcol(_webDavProvider.ServerUrl, folderPath);
				}
			}
			catch (Exception ex)
			{
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}

			return true;
		}
	}
}