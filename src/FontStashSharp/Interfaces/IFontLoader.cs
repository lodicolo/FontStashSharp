using System;

namespace FontStashSharp.Interfaces
{
  /// <summary>
  /// Font Rasterization Service
  /// </summary>
  public interface IFontLoader
	{
		IFontSource Load(byte[] data);
	}
}
