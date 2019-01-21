using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NextCloudFileUploader
{
	public class FolderService
	{
		private WebDavProvider _webDavProvider;

		public FolderService(WebDavProvider webDavProvider)
		{
			_webDavProvider = webDavProvider;
		}

		/// <summary>
		/// Создает папки из сгруппированного списка
		/// </summary>
		/// <param name="groupedFolderNameList">Сгруппированный список папок</param>
		public async Task<bool> CreateFoldersFromGroupedList(IEnumerable<string> groupedFolderNameList)
		{
			if (groupedFolderNameList == null) throw new Exception("Список папок пуст.");
			var current = 0;
			var enumerable = groupedFolderNameList.ToList();
			if (enumerable.Count == 0) return false;
			foreach (var folderName in enumerable)
			{
				try
				{
					var result = await _webDavProvider.Mkcol(_webDavProvider.ServerUrl, folderName, current, enumerable.Count);
				}
				catch (Exception ex)
				{
					ExceptionHandler.LogExceptionToConsole(ex);
					throw ex;
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			}

			return true;
		}
	}
}