using System;
using System.Xml.Serialization;

namespace DataSet
{
	/// <summary> Описание авто </summary>
	[XmlRoot("MyDocument", Namespace = "http://www.microsoft.com/namespace")]
	public class CarData
	{
		/// <summary> Наименование марки авто </summary>
		[XmlAttribute("Brand")] public string Brand { get; set; }

		/// <summary> Год выпуска </summary>
		[XmlAttribute("Year")] public UInt16 Year { get; set; }

		/// <summary> Объем двигателя </summary>
		[XmlAttribute("EngineCapacity")] public float EngineCapacity { get; set; }

		/// <summary> Кол-во дверей </summary>
		[XmlAttribute("DoorsNumber")] public UInt16 DoorsNumber { get; set; }

		public CarData() { }

		public byte CountFilledFields()
		{
			byte ret = 0;
			if (false == string.IsNullOrEmpty(Brand)) { ret++; }
			if (Year != 0) { ret++; }
			if (EngineCapacity != 0.0f) { ret++; }
			if (DoorsNumber != 0) { ret++; }
			return ret;
		}

		public override string ToString()
		{
			string ret = "";
			if (false == string.IsNullOrEmpty(Brand)) { ret += $"Brand = {Brand}; "; }
			if (Year != 0) { ret += $"Year={Year}; "; }
			if (EngineCapacity > 0.0f) { ret += $"EngineCapacity = {EngineCapacity}; "; }
			if (DoorsNumber != 0) { ret += $"DoorsNumber = {DoorsNumber}; "; }
			return ret.Trim();
		}
	}
}
