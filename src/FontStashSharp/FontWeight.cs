using System;
using System.Diagnostics.CodeAnalysis;

namespace FontStashSharp;

public struct FontWeight : IComparable, IComparable<FontWeight>, IEquatable<FontWeight>
{
    private readonly ushort _weight;

    public FontWeight(ushort weight)
    {
        _weight = weight;
    }

    public ushort Weight => _weight == default ? (ushort)400 : _weight;

    int IComparable.CompareTo(object obj) => obj is FontWeight fontWeight ? CompareTo(fontWeight) : 0;

    public int CompareTo(FontWeight other) => other.Weight - Weight;

    public override bool Equals([NotNullWhen(true)] object obj) => obj is FontWeight fontWeight && Equals(fontWeight);

    public bool Equals(FontWeight other) => Weight == other.Weight;

    public override int GetHashCode() => Weight.GetHashCode();

    public override string ToString() => Weight.ToString();

    public static bool operator ==(FontWeight left, FontWeight right) => left.Equals(right);

    public static bool operator !=(FontWeight left, FontWeight right) => !left.Equals(right);

    public static implicit operator ushort(FontWeight fontWeight) => fontWeight.Weight;

    public static implicit operator FontWeight(ushort weight) => new FontWeight(weight);

    public static readonly FontWeight Thin = 100;

    public static readonly FontWeight ExtraLight = 200;

    public static readonly FontWeight Light = 300;

    public static readonly FontWeight Normal = 400;

    public static readonly FontWeight Medium = 500;

    public static readonly FontWeight SemiBold = 600;

    public static readonly FontWeight Bold = 700;

    public static readonly FontWeight ExtraBold = 800;

    public static readonly FontWeight Black = 900;
}

