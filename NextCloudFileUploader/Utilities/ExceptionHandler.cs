using System;
using System.Reflection;
using log4net;

namespace NextCloudFileUploader.Utilities
{
	public static class ExceptionHandler
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
		/// <summary>
		/// Выписывает информацию по возникшей ошибке в консоль.
		/// </summary>
		/// <param name="ex">Ошибка</param>
		public static void LogExceptionToConsole(Exception ex)
		{
			Console.WriteLine($"{Environment.NewLine}------------------------- Ошибка в {ex.TargetSite.Name} -------------------------");
			Console.WriteLine($"Сообщение ошибки: {Environment.NewLine}{ex.Message}{Environment.NewLine}");
			Console.WriteLine($"Стек ошибки: {Environment.NewLine}{ex.StackTrace}");
			Console.WriteLine($"------------------------- Ошибка в {ex.TargetSite.Name} -------------------------{Environment.NewLine}");
			Log.Error(GetLogMessage(ex));
		}

		private static string GetLogMessage(Exception ex)
		{
			return $"{Environment.NewLine}------------------------- Ошибка в {ex.TargetSite.Name} -------------------------{Environment.NewLine}Сообщение ошибки: {Environment.NewLine}{ex.Message}{Environment.NewLine}Стек ошибки: {Environment.NewLine}{ex.StackTrace}";
		}
	}
}
