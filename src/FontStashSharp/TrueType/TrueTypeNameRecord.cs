namespace FontStashSharp.TrueType;

public struct TrueTypeNameRecord
{
	public ushort platformID;
	public ushort platformSpecificID;
	public ushort languageID;
	public ushort nameID;
	public ushort length;
	public ushort offset;
	public string name;
}
