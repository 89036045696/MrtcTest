using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;
using DataSet;

namespace Client
{
	class Program
	{
		/// <summary> кодировка протокола обмена и XML </summary>
		static Encoding mEncoding = Encoding.ASCII;
		static void Main(string[] args)
		{

			try
			{
				// создаем endpoint: с ip-адресом и номером порта
				var ipPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50000);

				byte[] bytes = new byte[1024];
				int bytesRec = 0;
				string message = null;
				while (true)
				{
					try
					{
						// (пере)подключение
						var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						socket.Connect(ipPoint);

						Console.Write("Enter command > ");
						message = Console.ReadLine();

						if (message.ToUpper().Trim().ToUpper() == "EXIT") { break; }

						byte[] msg = mEncoding.GetBytes(message);

						int bytesSent = socket.Send(msg);
						Console.WriteLine($"    :: send message {bytesSent} bytes");
						bytesRec = socket.Receive(bytes);
					}
					catch (Exception e)
					{
						Console.WriteLine($"    ERROR: {e.Message}");
						Console.ReadKey();
					}
					Console.WriteLine($"    :: receive from server {bytesRec} bytes");
					CommandProcessor(message, bytes);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			Console.ReadKey();
		}

		/// <summary> Парсинг команды и выдача результата </summary>
		/// <param name="aCommand"> Команда </param>
		/// <param name="aResponse"> Полученный результат от сервера </param>
		// TODO: проверять на соответствие отправленному запросу, ведь мы знаем что запрашивали
		static void CommandProcessor(string aCommand, byte[] aResponse)
		{
			try
			{
				// если ответ меньше 2 байт - ошибка
				// или пришёл признак ошибки от сервера 0x00
				if (aResponse?.Length < 2 || aResponse[0] == 0x00 )
				{
					Console.WriteLine($"        < INVALID COMMAND");
					return;
				}

				// инициализация хранилища результата
				List<CarData> parsedCars = new List<CarData>();
				// временное авто для конвертации
				CarData tmpCar = null;
				// TODO: переписать с функцией парсинга одной записи, а не отдельных полей
				// цикл по всем байтам ответа от сервера номер posByte,
				// num - количество считанных полй записи из ответного сообщения. 
				int posByte = 0, num = 0;
				// Буфер для признака "Число заполненных полей записи"
				byte nff = 0;
				while ( posByte < aResponse.Length )
				{
					try
					{
						// читаем первый байт
						byte b = aResponse[posByte++];

						if (b == 0x02) // 0x02 - признак начала записи
						{
							// если временное авто уже было инициализировано - сохраним его в коллекцию
							if (tmpCar != null) parsedCars.Add(tmpCar);
							// создадим новое временное авто
							tmpCar = new CarData();
							// сброс индекса номера поля
							num = 0;
							// считаем признак "Число заполненных полей в записи"
							nff = aResponse[posByte++];
						}

						if (b == 0x12) // 0x12 - признак 16-битного целого числа 
						{
							var span = new ReadOnlySpan<byte>(aResponse, posByte, sizeof(float));
							UInt16 val = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(span);
							// присвоить число нужному полю - в зависимости индекса текущего значения
							if (num <= 1) { tmpCar.Year = val; }
							else { tmpCar.DoorsNumber = val; }
							posByte += 2; // 2 - колич. байтов, которое занимает целое число в массиве согласно протоколу
							num++;
						}

						if (b == 0x09) // 0x09 - признак строки 
						{
							// читаем длину строки
							b = aResponse[posByte++];
							// присвоить значение
							tmpCar.Brand = 
								Encoding.ASCII.GetString( new ReadOnlySpan<byte>(aResponse, posByte, b) );
							posByte += b;
							num++;
						}

						if (b == 0x13) // 0x13 - признак числа с плавающей точкой в big-endian
						{
							var span = new ReadOnlySpan<byte>(aResponse, posByte, sizeof(float));
							float val = System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(span);
							// присвоить значение
							tmpCar.EngineCapacity = val;
							posByte += 4; // 4 - колич. байтов, которое занимает float число в массиве согласно протоколу
							num++;
						}
					}
					catch { /* при выходе за пределы массива - конец парсинга */ }
				}
				// если запрашивали 1 авто, сохраним авто в коллекцию
				if (tmpCar != null) parsedCars.Add(tmpCar);

				// сохраняем результат команды в файл
				File.WriteAllText(aCommand + ".xml", CarsArrayToXML(parsedCars.ToArray()), Encoding.UTF8);
				// отобразим коллекцию в консоли
				foreach (CarData eachCar in parsedCars) { Console.WriteLine($"        < " + eachCar); }
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			
		}

		/// <summary>
		/// конвертация коллекции авто в XML
		/// </summary>
		/// <param name="aCars">Коллекция с записями об авто</param>
		/// <returns>результат xml в виде строки</returns>
		static string CarsArrayToXML(CarData[] aCars)
		{
			// результат
			string result = null;
			// инициализация конвертора xml
			XmlSerializer serializer = new XmlSerializer(typeof(CarData[]));
			// инициализация потока для записи xml
			using (var stream = new MemoryStream())
			{
				// сериализация массива в xml
				serializer.Serialize(stream, aCars);
				// результат xml в виде строки
				result = mEncoding.GetString(stream.ToArray());
			}
			return result;

		}

	}
}
