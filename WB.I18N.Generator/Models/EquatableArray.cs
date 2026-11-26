using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace WB.I18N.Generator.Models;

/// <summary>让增量管线按元素和顺序比较不可变数组。</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
{
    private readonly ImmutableArray<T> _items;

    /// <summary>保存待比较的不可变元素。</summary>
    public EquatableArray(ImmutableArray<T> items)
    {
        _items = items;
    }

    /// <summary>获取元素，未初始化的数组按空数组处理。</summary>
    public ImmutableArray<T> Items
    {
        get
        {
            if (_items.IsDefault)
            {
                return ImmutableArray<T>.Empty;
            }

            return _items;
        }
    }

    /// <summary>比较元素个数、顺序和各元素的值。</summary>
    public bool Equals(EquatableArray<T> other)
    {
        ImmutableArray<T> left = Items;
        ImmutableArray<T> right = other.Items;
        if (left.Length != right.Length)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (int index = 0; index < left.Length; index++)
        {
            if (!comparer.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>比较当前数组与指定对象的值。</summary>
    public override bool Equals(object? obj)
    {
        if (obj is EquatableArray<T> other)
        {
            return Equals(other);
        }

        return false;
    }

    /// <summary>按元素顺序计算与相等规则一致的哈希值。</summary>
    public override int GetHashCode()
    {
        var comparer = EqualityComparer<T>.Default;
        int hash = 17;
        foreach (T item in Items)
        {
            int itemHash = 0;
            if (item is not null)
            {
                itemHash = comparer.GetHashCode(item);
            }

            hash = unchecked(hash * 31 + itemHash);
        }

        return hash;
    }
}
