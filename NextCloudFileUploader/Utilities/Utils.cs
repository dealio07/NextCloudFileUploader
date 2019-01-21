using System;
using System.Collections.Generic;

namespace NextCloudFileUploader.Utilities
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
			var percent = 100 * (processed + 1) / total;
			if (processed >= total - 1 && percent < 100)
				percent = 100;
			Console.Write($"\r{message}: {percent : ##0.#}% (выполнено {processed + 1} из {total})");
			if (processed >= total - 1)
				Console.Write(Environment.NewLine);
		}
		
		/// <summary>
		/// Показывает прогресс выполняемого процесса
		/// </summary>
		/// <param name="message">Отображаемое сообщение</param>
		/// <param name="processed">Обработано объектов</param>
		/// <param name="total">Общее количество объектов</param>
		/// <param name="processedByte">Отправлено байт</param>
		/// <param name="totalBytes">Общее количество байт</param>
		public static void ShowPercentProgress(string message, long processed, long total, long processedByte, long totalBytes)
		{
			var percent = 100 * (processed + 1) / total;
			if (processed >= total - 1 && percent < 100)
				percent = 100;
			Console.Write($"\r{message}: {percent : ##0.#}% (выполнено {processed + 1} из {total} / отправлено {processedByte} из {totalBytes} байт)");
			if (processed >= total - 1)
				Console.Write(Environment.NewLine);
		}

		/// <summary>
		/// Маскирует символы вводимого в консоли пароля символами: "*"
		/// </summary>
		/// <returns>Возвращает пароль</returns>
		public static string ReadAndMaskInputPassword()
		{
			var password = "";
			do
			{
				var key = Console.ReadKey(true);
				// Backspace и Enter не должны срабатывать
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
		
		/// <summary>
		/// Разбивает список на под-списки.
		/// </summary>
		/// <param name="bigList">Основной список, который следует разбить</param>
		/// <param name="nSize">Размер под-списка</param>
		/// <typeparam name="T">Тип элементов списка</typeparam>
		/// <returns>Возвращает под-списки основного списка указанного размера.</returns>
		public static IEnumerable<List<T>> SplitList<T>(List<T> bigList, int nSize)
		{
			for (var i = 0; i < bigList.Count; i += nSize)
			{
				yield return bigList.GetRange(i, Math.Min(nSize, bigList.Count - i));
			}
		}
	}
}