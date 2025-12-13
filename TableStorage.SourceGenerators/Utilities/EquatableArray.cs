using System.Collections;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// An immutable, equatable array that provides structural equality for source generator caching.
/// Based on the implementation from the .NET Community Toolkit.
/// This implementation follows best practices for incremental source generators by ensuring
/// proper value equality to enable effective caching.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
public readonly struct EquatableArray<T>(T[]? array) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    /// <summary>
    /// The underlying array. Null represents an empty array to optimize memory usage.
    /// </summary>
    private readonly T[]? _array = array;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range.</exception>
    public T this[int index] => AsSpan()[index];

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public int Length => _array?.Length ?? 0;

    /// <summary>
    /// Gets a value indicating whether the array is empty.
    /// </summary>
    public bool IsEmpty => Length is 0;

    /// <summary>
    /// Returns a span representation of the array.
    /// </summary>
    /// <returns>A span over the array elements.</returns>
    public ReadOnlySpan<T> AsSpan() => _array.AsSpan();

    /// <summary>
    /// Determines whether this array is equal to another array by comparing elements.
    /// </summary>
    /// <param name="other">The other array to compare with.</param>
    /// <returns>true if the arrays are equal; otherwise, false.</returns>
    public bool Equals(EquatableArray<T> other)
    {
        // Fast path for identical references (including both null)
        if (ReferenceEquals(_array, other._array))
        {
            return true;
        }

        // Handle null arrays
        if (_array is null)
        {
            return other._array is null;
        }

        if (other._array is null)
        {
            return false;
        }

        // Compare lengths first (fast comparison)
        if (_array.Length != other._array.Length)
        {
            return false;
        }

        // Compare elements using Span for better performance
        ReadOnlySpan<T> thisSpan = _array.AsSpan();
        ReadOnlySpan<T> otherSpan = other._array.AsSpan();

        for (int i = 0; i < thisSpan.Length; i++)
        {
            if (!thisSpan[i].Equals(otherSpan[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether this array is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>true if the arrays are equal; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);    /// <summary>
                                                                                                    /// Computes the hash code for this array based on its elements.
                                                                                                    /// </summary>
                                                                                                    /// <returns>A hash code for the current array.</returns>
    public override int GetHashCode()
    {
        if (_array is null)
        {
            return 0;
        }

        var hashCode = HashCode.Create();

        // Use ReadOnlySpan for better performance
        ReadOnlySpan<T> span = _array.AsSpan();
        foreach (T item in span)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the array.
    /// </summary>
    /// <returns>An enumerator for the array.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return (_array ?? []).AsEnumerable().GetEnumerator();
    }

    /// <summary>
    /// Returns a non-generic enumerator that iterates through the array.
    /// </summary>
    /// <returns>A non-generic enumerator for the array.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Determines whether two arrays are equal.
    /// </summary>
    /// <param name="left">The first array to compare.</param>
    /// <param name="right">The second array to compare.</param>
    /// <returns>true if the arrays are equal; otherwise, false.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two arrays are not equal.
    /// </summary>
    /// <param name="left">The first array to compare.</param>
    /// <param name="right">The second array to compare.</param>
    /// <returns>true if the arrays are not equal; otherwise, false.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts an array to an EquatableArray.
    /// </summary>
    /// <param name="array">The array to convert.</param>
    /// <returns>An EquatableArray wrapping the input array.</returns>
    public static implicit operator EquatableArray<T>(T[] array) => new(array);
}