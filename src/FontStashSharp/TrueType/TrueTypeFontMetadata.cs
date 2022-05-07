using FontStashSharp.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FontStashSharp.TrueType;

public sealed class TrueTypeFontMetadata : IFontMetadata
{
	private readonly TrueTypeNameTable _nameTable;
	private readonly Dictionary<TrueTypeNameId, Dictionary<ushort, Dictionary<TrueTypePlatformSpecificId, Dictionary<TrueTypePlatformId, string>>>> _nlepMap;

	public TrueTypeFontMetadata(ref TrueTypeNameTable nameTable)
	{
		_nameTable = nameTable;
		_nlepMap = new();

		ProcessTable();
	}

	public string Family => FindName(CultureInfo.CurrentUICulture, TrueTypeNameId.PreferredFamily) ?? FindName(CultureInfo.CurrentUICulture, TrueTypeNameId.FontFamily);

	public string Subfamily => FindName(CultureInfo.CurrentUICulture, TrueTypeNameId.PreferredSubfamily) ?? FindName(CultureInfo.CurrentUICulture, TrueTypeNameId.FontSubfamily);

	private string FindName(CultureInfo cultureInfo, TrueTypeNameId nameId)
	{
		if (!_nlepMap.TryGetValue(nameId, out var lepMap))
		{
			return default;
		}

		var currentCultureInfo = cultureInfo;

		do
		{
			if (lepMap.TryGetValue((ushort)cultureInfo.LCID, out var epMap))
			{
				var name = epMap.Values?.FirstOrDefault()?.Values.FirstOrDefault();
				if (name != default)
				{
					return name;
				}
			}

			if (currentCultureInfo == currentCultureInfo.Parent)
			{
				break;
			}

			currentCultureInfo = currentCultureInfo.Parent;

		} while (true);

		return lepMap.Values.FirstOrDefault()?.Values?.FirstOrDefault()?.Values.FirstOrDefault();
	}

	private void ProcessTable()
	{
		foreach (var name in _nameTable.names)
		{
			var nameId = (TrueTypeNameId)name.nameID;
			if (!_nlepMap.TryGetValue(nameId, out var lepMap))
			{
				lepMap = new();
				if (!_nlepMap.TryAdd(nameId, lepMap))
				{
					throw new InvalidOperationException();
				}
			}

			var languageID = name.languageID;
			if (!lepMap.TryGetValue(languageID, out var epMap))
			{
				epMap = new();
				if (!lepMap.TryAdd(languageID, epMap))
				{
					throw new InvalidOperationException();
				}
			}

			var platformSpecificId = (TrueTypePlatformSpecificId)name.platformSpecificID;
			if (!epMap.TryGetValue(platformSpecificId, out var pMap))
			{
				pMap = new();
				if (!epMap.TryAdd(platformSpecificId, pMap))
				{
					throw new InvalidOperationException();
				}
			}

			var platformId = (TrueTypePlatformId)name.platformID;
			if (!pMap.TryAdd(platformId, name.name))
			{
				throw new InvalidOperationException();
			}
		}
	}
}
