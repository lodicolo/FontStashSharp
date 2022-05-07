using FontStashSharp.TrueType;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using static StbTrueTypeSharp.StbTrueType;

namespace FontStashSharp;

internal partial class StbTrueTypeSharpSource
{
	private static unsafe TrueTypeNameTable GetNameTable(stbtt_fontinfo fontInfo)
	{
		var nameTableOffset = stbtt__find_table(fontInfo.data, (uint)fontInfo.fontstart, "name");
		if (nameTableOffset == default)
		{
			return default;
		}

		RawNameTable nameTable = fontInfo.data + nameTableOffset;
		return From(fontInfo, nameTable);
	}

	private static unsafe TrueTypeNameTable From(stbtt_fontinfo fontInfo, RawNameTable nameTable)
	{
		if (nameTable == default)
		{
			return default;
		}

		var nameTableVariablePtr = fontInfo.data + nameTable.stringOffset;

		return new TrueTypeNameTable
		{
			format = nameTable.format,
			count = nameTable.count,
			stringOffset = nameTable.stringOffset,
			names = nameTable.nameRecord.Select(nameRecord => From(fontInfo, nameRecord)).ToArray(),
			langTagCount = nameTable.langTagCount,
			langTagRecord = nameTable.langTagRecord.Select((langTagRecord, index) => From(nameTableVariablePtr, (uint)(0x8000 + index), langTagRecord)).ToArray(),
		};
	}

	private static unsafe TrueTypeNameRecord From(stbtt_fontinfo fontInfo, RawNameRecord nameRecord)
	{
		if (nameRecord == default)
		{
			return default;
		}

		int length = 0;
		var strData = (byte*)stbtt_GetFontNameString(fontInfo, &length, nameRecord.platformID, nameRecord.platformSpecificID, nameRecord.languageID, nameRecord.nameID);

		var name = nameRecord.platformID switch
		{
			STBTT_PLATFORM_ID_MAC => Encoding.UTF8.GetString(strData, length),
			STBTT_PLATFORM_ID_MICROSOFT => Encoding.BigEndianUnicode.GetString(strData, length),
			STBTT_PLATFORM_ID_UNICODE => Encoding.BigEndianUnicode.GetString(strData, length),
			_ => throw new NotSupportedException($"Platform {nameRecord.platformID} not supported.")
		};

		return new TrueTypeNameRecord
		{
			platformID = nameRecord.platformID,
			platformSpecificID = nameRecord.platformSpecificID,
			languageID = nameRecord.languageID,
			nameID = nameRecord.nameID,
			length = nameRecord.length,
			offset = nameRecord.offset,
			name = name,
		};
	}

	private static unsafe TrueTypeLangTagRecord From(byte* nameTableVariablePtr, uint lcid, RawLangTagRecord langTagRecord)
	{
		if (langTagRecord == default)
		{
			return default;
		}

		int length = 0;
		var strData = nameTableVariablePtr + langTagRecord.offset;

		var langTag = Encoding.BigEndianUnicode.GetString(strData, length);

		return new TrueTypeLangTagRecord
		{
			length = langTagRecord.length,
			offset = langTagRecord.offset,
			lcid = (ushort)lcid,
			langTag = langTag,
		};
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RawNameTable : IEquatable<RawNameTable>
	{
		public ushort format;
		public ushort count;
		public ushort stringOffset;
		public RawNameRecord[] nameRecord;
		public ushort langTagCount;
		public RawLangTagRecord[] langTagRecord;

		public override bool Equals(object obj) => obj is RawNameTable nameTable && Equals(nameTable);

		public bool Equals(RawNameTable other) => format == other.format && count == other.count && stringOffset == other.stringOffset && nameRecord.SequenceEqual(other.nameRecord) && langTagCount == other.langTagCount && langTagRecord.SequenceEqual(other.langTagRecord);

		public static bool operator ==(RawNameTable a, RawNameTable b) => a.Equals(b);

		public static bool operator !=(RawNameTable a, RawNameTable b) => !a.Equals(b);

		public static unsafe implicit operator RawNameTable(byte* nameTablePtr)
		{
			if (nameTablePtr == default)
			{
				return default;
			}

			var offset = 0;
			var format = ttUSHORT(nameTablePtr + sizeof(ushort) * offset++);
			var count = ttUSHORT(nameTablePtr + sizeof(ushort) * offset++);
			var stringOffset = ttUSHORT(nameTablePtr + sizeof(ushort) * offset++);

			switch (format)
			{
				case 0:
				case 1:
					break;

				default:
#if DEBUG
					throw new NotSupportedException($"Names table format {format} not supported.");
#else
					return default;
#endif
			}

			var nameRecord = new RawNameRecord[count];
			var nameRecordOffset = nameTablePtr + sizeof(ushort) * offset;
			for (var index = 0; index < count; index++)
			{
				nameRecord[index] = nameRecordOffset + sizeof(RawNameRecord) * index;
			}

			ushort langTagCount = 0;
			var langTagRecord = new RawLangTagRecord[0];

			if (format == 1)
			{
				nameTablePtr = nameRecordOffset + sizeof(RawNameRecord) * count;
				offset = 0;

				langTagCount = ttUSHORT(nameTablePtr + sizeof(ushort) * offset++);
				langTagRecord = new RawLangTagRecord[langTagCount];

				var langTagRecordOffset = nameTablePtr + sizeof(ushort) * offset;
				for (var index = 0; index < count; index++)
				{
					langTagRecord[index] = langTagRecordOffset + sizeof(RawLangTagRecord) * index;
				}
			}

			return new RawNameTable
			{
				format = format,
				count = count,
				stringOffset = stringOffset,
				nameRecord = nameRecord,
				langTagCount = langTagCount,
				langTagRecord = langTagRecord,
			};
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RawLangTagRecord : IEquatable<RawLangTagRecord>
	{
		public ushort length;
		public ushort offset;

		public override bool Equals(object obj) => obj is RawLangTagRecord nameRecord && Equals(nameRecord);

		public bool Equals(RawLangTagRecord other) => length == other.length && offset == other.offset;

		public static bool operator ==(RawLangTagRecord a, RawLangTagRecord b) => a.Equals(b);

		public static bool operator !=(RawLangTagRecord a, RawLangTagRecord b) => !a.Equals(b);

		public static unsafe implicit operator RawLangTagRecord(byte* nameRecordPtr)
		{
			var offset = 0;
			return new RawLangTagRecord
			{
				length = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				offset = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
			};
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RawNameRecord : IEquatable<RawNameRecord>
	{
		public ushort platformID;
		public ushort platformSpecificID;
		public ushort languageID;
		public ushort nameID;
		public ushort length;
		public ushort offset;

		public override bool Equals(object obj) => obj is RawNameRecord nameRecord && Equals(nameRecord);

		public bool Equals(RawNameRecord other) => platformID == other.platformID && platformSpecificID == other.platformSpecificID && languageID == other.languageID && nameID == other.nameID && length == other.length && offset == other.offset;

		public static bool operator ==(RawNameRecord a, RawNameRecord b) => a.Equals(b);

		public static bool operator !=(RawNameRecord a, RawNameRecord b) => !a.Equals(b);

		public static unsafe implicit operator RawNameRecord(byte* nameRecordPtr)
		{
			var offset = 0;
			return new RawNameRecord
			{
				platformID = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				platformSpecificID = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				languageID = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				nameID = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				length = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
				offset = ttUSHORT(nameRecordPtr + sizeof(ushort) * offset++),
			};
		}
	}
}
