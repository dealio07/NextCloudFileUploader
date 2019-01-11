using System;

namespace Uploader
{
	public class Utils
	{
		/// <summary>
		/// Показывает прогресс выполняемого процесса
		/// </summary>
		/// <param name="message">Отображаемое сообщение</param>
		/// <param name="processed">Обработано объектов</param>
		/// <param name="total">Общее количество объектов</param>
		public static void ShowPercentProgress(string message, long processed, long total)
		{
			var percent = (100 * (processed + 1)) / total;
			if (processed >= total - 1 && percent < 100)
				percent = 100;
			Console.Write($"\r{message}: {percent : ##0.#}% выполнено {processed + 1} из {total}");
			if (processed >= total - 1)
				Console.Write(Environment.NewLine);
		}

		/// <summary>
		/// Маскирует символы вводимого в консоли пароля символами: "*"
		/// </summary>
		/// <returns>Возвращает пароль</returns>
		public static string ReadAndMaskInputPassword()
		{
			string password = "";
			do
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				// Backspace не должен срабатывать
				if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
				{
					password += key.KeyChar;
					Console.Write("*");
				}
				else
				{
					if (key.Key == ConsoleKey.Backspace && password.Length > 0)
					{
						password = password.Substring(0, (password.Length - 1));
						Console.Write("\b \b");
					}
					else if (key.Key == ConsoleKey.Enter)
					{
						Console.WriteLine();
						break;
					}
				}
			} while (true);

			return password;
		}
	}
}