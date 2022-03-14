using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DataSet;

namespace Server
{
	class Program
	{
		/// <summary> кодировка протокола обмена </summary>
		static Encoding mEncoding = Encoding.ASCII;

		/// <summary> Предутановленный словарь автомобилей, ключем является id - уникальный номер </summary>
		static SortedDictionary<UInt16, CarData> mCars = new SortedDictionary<ushort, CarData>
		{
			{ 1,  new CarData { Brand = "Nissan",   Year = 2008,  EngineCapacity = 1.6f, DoorsNumber = 0 } },
			{ 2,  new CarData { Brand = "Audi",     Year = 2020,  EngineCapacity = 0.0f, DoorsNumber = 0 } },
			{ 3,  new CarData { Brand = "Mercedes", Year = 0,   EngineCapacity = 1.8f, DoorsNumber = 2 } },
			{ 4,  new CarData { Brand = "BMW",      Year = 0,   EngineCapacity = 0.0f, DoorsNumber = 0 } },
			{ 5,  new CarData { Brand = "Lada",     Year = 2012,  EngineCapacity = 2.0f, DoorsNumber = 3 } },
			{ 6,  new CarData { Brand = "Kia",      Year = 2013,  EngineCapacity = 2.2f, DoorsNumber = 4 } },
			{ 7,  new CarData { Brand = "Hyundai",  Year = 0,    EngineCapacity = 0.0f, DoorsNumber = 0 } },
			{ 8,  new CarData { Brand = "",         Year = 0,    EngineCapacity = 0.0f, DoorsNumber = 0 } }
		};

		static void Main(string[] args)
		{
			try
			{
				// создаем endpoint: с ip-адресом и номером порта
				var localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50000);
				// создаем сокет
				var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				// связываем сокет с локальной точкой, по которой будем принимать данные
				listenSocket.Bind(localEndPoint);
				listenSocket.Listen(10);
				Console.WriteLine("Waiting for a connection...");
				
				byte[] rxBytes = new byte[100];		// приёмный буфер

				while (true) // прием комманды и отправка ответа
				{
					Socket connectedSocket = listenSocket.Accept();

					int rxAmountBytes		= 0;					// количество принятых байтов, сбрасываем перед новым приёмом
					string rxCommand		= null;				// входящая строка-команда

					// чтение строковой команды из подключенного сокета
					do
					{
						rxAmountBytes = connectedSocket.Receive(rxBytes);
						rxCommand += mEncoding.GetString(rxBytes, 0, rxAmountBytes);
					} while (connectedSocket.Available > 0);

					// выведем на экран полученную команду
					Console.WriteLine("Text received : {0}", rxCommand);

					// обработка входящей команды и генерация ответа клиенту
					byte[] txBytes = CommandProcessor(rxCommand);
					try
					{
						// отправить ответ
						connectedSocket.Send(txBytes);
						Console.WriteLine($" :: send to client {txBytes.Length} rxBytes");
					}
					catch (Exception e)
					{
						Console.WriteLine(e.Message);
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			Console.Read();
		}

		/// <summary> Обработка поступающих команд от клиента и
		/// генерация ответа клиенту в соответствии с протоколом. </summary>
		/// <param name="aRxCommand"> команда </param>
		/// <returns> ответ на команду </returns>
		static byte[] CommandProcessor(string aRxCommand)
		{
			// разбить команду на отдельные слова
			string[] splitted = aRxCommand.Split(' ');

			// в команде обязательно 2 слова, иначе - ответим кодом ошибки 0x0
			if (splitted.Length != 2) { return new byte[] { 0x0 }; }
			// парсить будем все слова в верхнем регистре и без лишних пробелов
			foreach (var str in splitted)
			{
				str.Trim().ToUpper();
			}

			switch (splitted[0].Trim().ToUpper())
			{
				// начало команды равно GET
				case "GET":
					switch (splitted[1].Trim().ToUpper())
					{
						// все записи
						case "ALL":
							// вернуть все записи
							return ToByte(mCars.Values.ToArray());
						// предположительно конкретный номер записи
						default:
							// если 2 слово - то парсим число = номер записи
							if (int.TryParse(splitted[1], out int id))
							{
								// и есть такой ключ в списке авто
								if (mCars.ContainsKey((ushort)id))
								{
									// вернуть запись
									return ToByte(mCars[(ushort)id]);
								}
							}
							break;
					}
					break;
			}
			// не распознана команда
			return new byte[] { 0 };
		}

		/// <summary> Кодирование строки в массив байт с кодом признака </summary>
		/// <param name="aStr"></param>
		/// <returns></returns>
		static byte[] Write(string aStr)
		{
			// резервирование массива
			byte[] ret = new byte[2 + aStr.Length];
			// Код признака
			ret[0] = 0x09;
			// Длина текста
			ret[1] = (byte)aStr.Length;
			// значение записываем в результат
			Encoding.ASCII.GetBytes(aStr).CopyTo(ret, 2);
			return ret;
		}

		// TODO: методы Write переписать с шаблонами

		/// <summary> Кодирование числа в массив байт с кодом признака </summary>
		static byte[] Write(UInt16 value)
		{
			// резервирование массива
			byte[] ret = new byte[1 + sizeof(UInt16)];
			// Код признака
			ret[0] = 0x12;
			// конвертируем значение в массив байтов big-endian и копируем в выходной массив
			if (BitConverter.IsLittleEndian) { value = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value); }
			BitConverter.GetBytes(value).CopyTo(ret, 1);
			return ret;
		}

		/// <summary> Кодирование числа с плавающей точки в массив байт с кодом признака </summary>
		static byte[] Write(float value)
		{
			// резервирование массива
			byte[] ret = new byte[1 + sizeof(float)];
			// Код признака
			ret[0] = 0x13;
			// конвертируем значение в массив байтов big-endian и копируем в выходной массив
			if (BitConverter.IsLittleEndian)
			{
				// преобразование через WriteSingleBigEndian(span...
				var span = new Span<byte>(ret, 1, sizeof(float));
				System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(span, value);
			}
			else
			{
				BitConverter.GetBytes(value).CopyTo(ret, 1);
			}

			return ret;
		}

		/// <summary> Конвертация авто в массив байт по протоколу </summary>
		static byte[] ToByte(CarData car)
		{
			// Считаем, что если есть запись, то она не пустая, т.е. в ней какое-то поле заполнено
			// поэтому отправим ответ
			List<byte> ret = new List<byte>();
			// Поле заголовка - Признак начала записи
			ret.Add(0x02);
			// Число заполненных (передаваемых) полей в записи
			ret.Add(car.CountFilledFields());
			if (false == string.IsNullOrEmpty(car.Brand))
			{
				// Признак поля типа строка
				// длина смысловой части строки
				// смысловая часть ASCII-строки
				ret.AddRange(Write(car.Brand));
			}
			if (car.Year != 0)
			{
				// Признак поля типа 16-битовое целое
				// значение поля "Год выпуска"
				ret.AddRange(Write(car.Year));
			}
			if (car.EngineCapacity > 0)
			{
				// Признак поля типа с плавающей точкой
				// значение поля "Объем двигателя"
				ret.AddRange(Write(car.EngineCapacity));
			}
			if (car.DoorsNumber != 0)
			{
				// Признак поля типа 16-битовое целое
				// значение поля "Число дверей"
				ret.AddRange(Write(car.DoorsNumber));
			}

			return ret.ToArray();
		}

		/// <summary> Конвертация массива авто в массив байт по протоколу </summary>
		static byte[] ToByte(CarData[] car)
		{
			// инициализация потока в памяти
			using (var stream = new MemoryStream())
			{
				// конвертор
				BinaryWriter b = new BinaryWriter(stream);
				// перебрать массив авто
				foreach (CarData data in car)
					// записать массив байт в поток
					b.Write(ToByte(data));
				// вернуть массива авто в виде массива байт 
				return stream.ToArray();
			}
		}
	}
}
