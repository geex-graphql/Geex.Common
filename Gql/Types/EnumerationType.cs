using System;
using Geex.Common.Abstractions;
using HotChocolate.Types;

namespace Geex.Common.Gql.Types
{
    public class EnumerationType<TEnum, TValue> : EnumType<TEnum> where TEnum : IEnumeration where TValue : IEquatable<TValue>, IComparable<TValue>
    {
    }
    //public class EnumerationType<TEnum, TValue> : ScalarType<TEnum> where TEnum : Enumeration<TEnum, TValue> where TValue : IEquatable<TValue>, IComparable<TValue>
    //{
    //    public EnumerationType() : base(typeof(TEnum).Name)
    //    {
    //    }

    //    public override bool IsInstanceOfType(IValueNode literal)
    //    {
    //        if (literal == null)
    //        {
    //            throw new ArgumentNullException(nameof(literal));
    //        }

    //        return literal is StringValueNode
    //            || literal is IntValueNode
    //            || literal is NullValueNode;
    //    }

    //    public override object? ParseLiteral(IValueNode valueSyntax, bool withDefaults = true)
    //    {
    //        if (valueSyntax == null)
    //        {
    //            throw new ArgumentNullException(nameof(valueSyntax));
    //        }

    //        if (valueSyntax is StringValueNode stringLiteral)
    //        {
    //            dynamic value = stringLiteral.Value;
    //            return Enumeration<TEnum, TValue>.FromValue(value);
    //        }

    //        throw new SerializationException("", new EnumerationType<TEnum, TValue>());
    //    }

    //    public override IValueNode ParseValue(object value)
    //    {
    //        if (value is TEnum s)
    //        {
    //            return new StringValueNode(s.ToString());
    //        }

    //        throw new SerializationException("", new EnumerationType<TEnum, TValue>());
    //    }

    //    public override IValueNode ParseResult(object? resultValue)
    //    {
    //        return this.ParseValue(resultValue);
    //    }

    //    public override object Serialize(object value)
    //    {
    //        if (value is TEnum s)
    //        {
    //            return s;
    //        }

    //        throw new SerializationException("", new EnumerationType<TEnum, TValue>());
    //    }

    //    public override bool TrySerialize(object? runtimeValue, out object? resultValue)
    //    {
    //        try
    //        {
    //            resultValue = this.Serialize(runtimeValue);
    //            return true;
    //        }
    //        catch (Exception)
    //        {
    //            resultValue = null;
    //            return false;
    //        }
    //    }

    //    public override bool TryDeserialize(object? serialized, out object value)
    //    {
    //        dynamic dynamicValue = serialized;
    //        if (serialized is string str)
    //        {
    //            value = Enumeration<TEnum, TValue>.FromValue(dynamicValue);
    //            return true;
    //        }

    //        if (serialized is TEnum)
    //        {
    //            value = serialized;
    //            return true;
    //        }

    //        value = null;
    //        return false;
    //    }
    //}
}
