using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Uploader
{
	public class FolderService
	{
		private WebDavProvider _webDavProvider;

		public FolderService(WebDavProvider webDavProvider)
		{
			_webDavProvider = webDavProvider;
		}

		/// <summary>
		/// Создает дополнительные директории из списка директорий
		/// </summary>
		/// <param name="folderNameList">Список директорий</param>
		public async Task CreateFolders(IEnumerable<string> folderNameList)
		{
			var remoteFolderPath = "";
			if (folderNameList == null) return;
			foreach (var folderName in folderNameList)
			{
				var result = await _webDavProvider.Mkcol(_webDavProvider.ServerUrl + remoteFolderPath, folderName);
				if (!remoteFolderPath.Contains(folderName))
					remoteFolderPath += folderName + "/";
			}
		}

		public async Task CreateFoldersFromGroupedList(IEnumerable<string> groupedFolderNameList)
		{
			if (groupedFolderNameList == null) throw new Exception("Список папок пуст.");
			long current = 0;
			var enumerable = groupedFolderNameList.ToList();
			if (enumerable.Count == 0) return;
			foreach (var folderName in enumerable)
			{
				try
				{
					await _webDavProvider.Mkcol(_webDavProvider.ServerUrl, folderName);
					Utils.ShowPercentProgress("2. Создаём папки", current, enumerable.Count);
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			}
		}

		/// <summary>
		/// Создает директории для файлов
		/// </summary>
		/// <returns></returns>
		public async Task CreateFoldersFromFileList(IEnumerable<File> fileList)
		{
			var enumerable = fileList.ToList();
			foreach (var file in enumerable)
			{
				await CreateFolders(file.FolderNames);
				Utils.ShowPercentProgress("2. Создаём папки", enumerable.IndexOf(file), enumerable.Count);
			}
		}

		/// <summary>
		/// Создает директории для файлов из списка
		/// </summary>
		/// <returns></returns>
		public async Task CreateFoldersFromList(IEnumerable<string> folderList)
		{
			Console.WriteLine("2. Создаем папки");
			var enumerable = folderList.ToList();
			await CreateFolders(enumerable);
		}
	}
}