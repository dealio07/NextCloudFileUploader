﻿using System;
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

		public async Task<bool> CreateFoldersFromGroupedList(IEnumerable<string> groupedFolderNameList)
		{
			if (groupedFolderNameList == null) throw new Exception("Список папок пуст.");
			long current = 0;
			var enumerable = groupedFolderNameList.ToList();
			if (enumerable.Count == 0) return false;
			foreach (var folderName in enumerable)
			{
				try
				{
					var mcolResult = await _webDavProvider.Mkcol(_webDavProvider.ServerUrl, folderName);
					Utils.ShowPercentProgress("2. Создаём папки", current, enumerable.Count);
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