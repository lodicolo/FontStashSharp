using FontStashSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp.SharpFont;
using System.Linq;

#if MONOGAME || FNA
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Graphics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using Texture2D = System.Object;
#endif

namespace FontStashSharp
{
	public class FontSystem : IDisposable
	{
		private readonly List<IFontSource> _fontSources = new List<IFontSource>();
		private readonly Int32Map<DynamicSpriteFont> _fonts = new Int32Map<DynamicSpriteFont>();
		private readonly FontSystemSettings _settings;
		private readonly List<uint> _supportedCodePoints;

		private FontAtlas _currentAtlas;

		public FontSystemEffect Effect => _settings.Effect;
		public int EffectAmount => _settings.EffectAmount;

		public int TextureWidth => _settings.TextureWidth;
		public int TextureHeight => _settings.TextureHeight;

		public bool PremultiplyAlpha => _settings.PremultiplyAlpha;

		public float FontResolutionFactor => _settings.FontResolutionFactor;

		public int KernelWidth => _settings.KernelWidth;
		public int KernelHeight => _settings.KernelHeight;

		public Texture2D ExistingTexture => _settings.ExistingTexture;
		public Rectangle ExistingTextureUsedSpace => _settings.ExistingTextureUsedSpace;

		public IReadOnlyList<uint> SupportedCodePoints => _supportedCodePoints.AsReadOnly();

		public bool UseKernings = true;
		public int? DefaultCharacter = ' ';

		public int CharacterSpacing = 0;
		public int LineSpacing = 0;

		internal int BlurAmount => Effect == FontSystemEffect.Blurry ? EffectAmount : 0;
		internal int StrokeAmount => Effect == FontSystemEffect.Stroked ? EffectAmount : 0;

		internal List<IFontSource> FontSources => _fontSources;

		public List<FontAtlas> Atlases { get; } = new List<FontAtlas>();

		public event EventHandler CurrentAtlasFull;
		private readonly IFontLoader _fontLoader;

		public FontSystem(FontSystemSettings settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			_settings = settings.Clone();
			_supportedCodePoints = new();

			if (_settings.FontLoader == null)
			{
				//_fontLoader = new FreeTypeLoader();
				var loaderSettings = new StbTrueTypeSharpSettings
				{
				  KernelWidth = _settings.KernelWidth,
				  KernelHeight = _settings.KernelHeight
				};
				_fontLoader = new StbTrueTypeSharpLoader(loaderSettings);
			}
			else
			{
				_fontLoader = _settings.FontLoader;
			}
		}

		public FontSystem() : this(FontSystemSettings.Default)
		{
		}

		public void Dispose()
		{
			if (_fontSources != null)
			{
				foreach (var font in _fontSources)
					font.Dispose();
				_fontSources.Clear();
			}

			Atlases?.Clear();
			_currentAtlas = null;
			_fonts.Clear();
		}

		public void AddFont(byte[] data)
		{
			var fontSource = _fontLoader.Load(data);
			_supportedCodePoints.AddRange(fontSource.SupportedCodePoints.Where(codePoint => !_supportedCodePoints.Contains(codePoint)));
			_fontSources.Add(fontSource);
		}

		public void AddFont(Stream stream)
		{
			AddFont(stream.ToByteArray());
		}

		public DynamicSpriteFont GetFont(int fontSize)
		{
			DynamicSpriteFont result;
			if (_fonts.TryGetValue(fontSize, out result))
			{
				return result;
			}

			if (_fontSources.Count == 0)
			{
				throw new Exception("Could not create a font without a single font source. Use AddFont to add at least one font source.");
			}

			var fontSource = _fontSources[0];

			int ascent, descent, lineHeight;
			fontSource.GetMetricsForSize(fontSize, out ascent, out descent, out lineHeight);

			result = new DynamicSpriteFont(this, fontSize, lineHeight);
			_fonts[fontSize] = result;
			return result;
		}

		public void Reset()
		{
			Atlases.Clear();
			_fonts.Clear();
		}

		internal int? GetCodepointIndex(int codepoint, out int fontSourceIndex)
		{
			fontSourceIndex = 0;
			var g = default(int?);

			for(var i = 0; i < _fontSources.Count; ++i)
			{
				var f = _fontSources[i];
				g = f.GetGlyphId(codepoint);
				if (g != null)
				{
					fontSourceIndex = i;
					break;
				}
			}

			return g;
		}

#if MONOGAME || FNA || STRIDE
		private FontAtlas GetCurrentAtlas(GraphicsDevice device, int textureWidth, int textureHeight)
#else
		private FontAtlas GetCurrentAtlas(ITexture2DManager device, int textureWidth, int textureHeight)
#endif
		{
			if (_currentAtlas == null)
			{
				Texture2D existingTexture = null;
				if (ExistingTexture != null && Atlases.Count == 0)
				{
					existingTexture = ExistingTexture;
				}

				_currentAtlas = new FontAtlas(textureWidth, textureHeight, 256, existingTexture);

				// If existing texture is used, mark existing used rect as used
				if (existingTexture != null && !ExistingTextureUsedSpace.IsEmpty)
				{
					if (!_currentAtlas.AddSkylineLevel(0, ExistingTextureUsedSpace.X, ExistingTextureUsedSpace.Y, ExistingTextureUsedSpace.Width, ExistingTextureUsedSpace.Height))
					{
						throw new Exception(string.Format("Unable to specify existing texture used space: {0}", ExistingTextureUsedSpace));
					}

					// TODO: Clear remaining space
				}

				Atlases.Add(_currentAtlas);
			}

			return _currentAtlas;
		}

#if MONOGAME || FNA || STRIDE
		internal void RenderGlyphOnAtlas(GraphicsDevice device, DynamicFontGlyph glyph)
#else
		internal void RenderGlyphOnAtlas(ITexture2DManager device, DynamicFontGlyph glyph)
#endif
		{
			var textureSize = new Point(TextureWidth, TextureHeight);

			if (ExistingTexture != null)
			{
#if MONOGAME || FNA || STRIDE
				textureSize = new Point(ExistingTexture.Width, ExistingTexture.Height);
#else
				textureSize = device.GetTextureSize(ExistingTexture);
#endif
			}

			int gx = 0, gy = 0;
			var gw = glyph.Bounds.Width;
			var gh = glyph.Bounds.Height;

			var currentAtlas = GetCurrentAtlas(device, textureSize.X, textureSize.Y);
			if (!currentAtlas.AddRect(gw, gh, ref gx, ref gy))
			{
				CurrentAtlasFull?.Invoke(this, EventArgs.Empty);

				// This code will force creation of new atlas
				_currentAtlas = null;
				currentAtlas = GetCurrentAtlas(device, textureSize.X, textureSize.Y);

				// Try to add again
				if (!currentAtlas.AddRect(gw, gh, ref gx, ref gy))
				{
					throw new Exception(string.Format("Could not add rect to the newly created atlas. gw={0}, gh={1}", gw, gh));
				}
			}

			glyph.Bounds.X = gx;
			glyph.Bounds.Y = gy;

			currentAtlas.RenderGlyph(device, glyph, FontSources[glyph.FontSourceIndex], BlurAmount, StrokeAmount, PremultiplyAlpha, KernelWidth, KernelHeight);

			glyph.Texture = currentAtlas.Texture;
		}
	}
}