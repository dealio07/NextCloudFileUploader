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
		/// Создает директории для файлов
		/// </summary>
		/// <returns></returns>
		public async Task CreateFolders(IEnumerable<File> fileList)
		{
			Console.WriteLine("2. Создаем папки");
			var enumerable = fileList.ToList();
			foreach (var file in enumerable)
			{
				await _webDavProvider.CreateFolders(file.FolderNames);
				Utils.ShowPercentProgress($"Создаём папку: {file.GetRemoteFolderPath()}", enumerable.IndexOf(file), enumerable.Count);
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
			await _webDavProvider.CreateFolders(enumerable);
		}

		/// <summary>
		/// Создает директории для файлов параллельно
		/// </summary>
		/// <returns></returns>
		public async Task CreateFoldersInParallel(IEnumerable<File> fileList)
		{
			long current = 0;
			var enumerable = fileList.ToList();
			Parallel.ForEach(enumerable, async (file, state, s) =>
			{
				try
				{
					await _webDavProvider.CreateFolders(file?.FolderNames);
					Utils.ShowPercentProgress("2. Создаём папки", current, enumerable.Count);
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			});
		}
	}
}