using System;
using System.Linq;

namespace NextCloudFileUploader
{
	public class ExceptionHandler
	{
		/// <summary>
		/// Выписывает информацию по возникшей ошибке в консоль.
		/// </summary>
		/// <param name="ex">Ошибка</param>
		public static void LogExceptionToConsole(Exception ex)
		{
			Console.WriteLine($"{Environment.NewLine}------------------------- Ошибка в {ex.Source.Last()} -------------------------");
			Console.WriteLine($"Сообщение ошибки: {Environment.NewLine}{ex.Message}");
			Console.WriteLine($"Стек ошибки: {Environment.NewLine}{ex.StackTrace}");
			Console.WriteLine($"------------------------- Ошибка в {ex.Source.Last()} -------------------------{Environment.NewLine}");
		}
	}
}
