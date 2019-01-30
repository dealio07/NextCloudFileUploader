using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NextCloudFileUploader.Entities;
using NextCloudFileUploader.Utilities;
using NextCloudFileUploader.WebDav;
using log4net;

namespace NextCloudFileUploader.Services
{
	public class FileService
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private WebDavProvider _webDavProvider;

		public FileService(WebDavProvider webDavProvider)
		{
			_webDavProvider = webDavProvider;
		}

        /// <summary>
        /// Выгружает файлы в хранилище.
        /// </summary>
        /// <param name="fileList">Список файлов, которые следует выгрузить</param>
        /// /// <param name="allFiles">Список всех файлов сущности</param>
        public async Task<bool> UploadFiles(IEnumerable<EntityFile> fileList, IEnumerable<EntityFile> allFiles)
		{
			var files = fileList.ToList();
			var current = 0;

			foreach (var file in files)
			{
				try
				{
					await _webDavProvider.PutWithHttp(file, allFiles);
				}
				catch (Exception ex)
				{
					Log.Error($"Ошибка при выгрузке файла #{file.Number.ToString()}");
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