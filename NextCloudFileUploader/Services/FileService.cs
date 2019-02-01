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

        // Выгружает файлы в хранилище.
        public async Task<bool> UploadFile(EntityFile file)
		{
			try
			{
				await _webDavProvider.PutWithHttp(file);
			}
			catch (Exception ex)
			{
				Log.Error($"Ошибка при выгрузке файла #{file.Number.ToString()}");
				ExceptionHandler.LogExceptionToConsole(ex);
				throw ex;
			}

			return true;
		}
	}
}