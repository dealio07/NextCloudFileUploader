using System;
using System.Collections.Generic;
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
		public async Task CreateFolders(List<File> fileList)
		{
			Console.WriteLine("2. Создаем папки");
			foreach (var file in fileList)
			{
				await _webDavProvider.CreateFolders(file.FolderNames);
				Program.ShowPercentProgress($"Создаём папку: {file.GetRemoteFolderPath()}", fileList.IndexOf(file), fileList.Count);
			}
		}

		/// <summary>
		/// Создает директории для файлов из списка
		/// </summary>
		/// <returns></returns>
		public async Task CreateFoldersFromList(List<string> folderList)
		{
			Console.WriteLine("2. Создаем папки");
			await _webDavProvider.CreateFolders(folderList);
		}

		/// <summary>
		/// Создает директории для файлов параллельно
		/// </summary>
		/// <returns></returns>
		public async Task CreateFoldersInParallel(List<File> fileList)
		{
			long current = 0;
			Parallel.ForEach(fileList, async (file, state, s) =>
			{
				try
				{
					await _webDavProvider.CreateFolders(file?.FolderNames);
					Program.ShowPercentProgress("2. Создаём папки", current, fileList.Count);
				}
				finally
				{
					Interlocked.Increment(ref current);
				}
			});
		}
	}
}