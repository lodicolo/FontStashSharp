namespace FontStashSharp.TrueType;

public struct TrueTypeNameTable
{
	public ushort format;
	public ushort count;
	public ushort stringOffset;
	public TrueTypeNameRecord[] names;
	public ushort langTagCount;
	public TrueTypeLangTagRecord[] langTagRecord;
}
