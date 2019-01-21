using System;

namespace NextCloudFileUploader.Utilities
{
	public class ExceptionHandler
	{
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
		}
	}
}
