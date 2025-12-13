using System.Runtime.CompilerServices;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// A hash code builder that provides consistent hashing behavior for netstandard2.0.
/// Based on the implementation from the .NET Community Toolkit.
/// </summary>
internal struct HashCode
{
    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private static readonly uint s_seed = GenerateSeed();

    private uint _v1, _v2, _v3, _v4;
    private uint _queue1, _queue2, _queue3;
    private uint _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCode"/> struct.
    /// </summary>
    /// <returns>A new <see cref="HashCode"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashCode Create()
    {
        HashCode hashCode = default;
        hashCode._v1 = s_seed + Prime1 + Prime2;
        hashCode._v2 = s_seed + Prime2;
        hashCode._v3 = s_seed;
        hashCode._v4 = s_seed - Prime1;
        return hashCode;
    }

    /// <summary>
    /// Adds a single value to the hash code.
    /// </summary>
    /// <typeparam name="T">The type of the value to add.</typeparam>
    /// <param name="value">The value to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add<T>(T value)
    {
        Add(value?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Adds a single hash code to the accumulator.
    /// </summary>
    /// <param name="value">The hash code to add.</param>
    public void Add(int value)
    {
        uint val = (uint)value;
        uint previousLength = _length++;
        uint position = previousLength % 4;
        if (position is 0)
        {
            _queue1 = val;
        }
        else if (position is 1)
        {
            _queue2 = val;
        }
        else if (position is 2)
        {
            _queue3 = val;
        }
        else
        {
            _v1 = Round(_v1, _queue1);
            _v2 = Round(_v2, _queue2);
            _v3 = Round(_v3, _queue3);
            _v4 = Round(_v4, val);
        }
    }

    /// <summary>
    /// Gets the resulting hash code after all values have been added.
    /// </summary>
    /// <returns>The final hash code.</returns>
    public readonly int ToHashCode()
    {
        uint length = _length;
        uint position = length % 4;
        uint hash = length < 4 ? MixEmptyState() : MixState(_v1, _v2, _v3, _v4);

        hash += length * 4;

        if (position > 0)
        {
            hash = QueueRound(hash, _queue1);
            if (position > 1)
            {
                hash = QueueRound(hash, _queue2);
                if (position > 2)
                {
                    hash = QueueRound(hash, _queue3);
                }
            }
        }

        hash = MixFinal(hash);
        return (int)hash;
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1>(T1 value1)
    {
        HashCode hc = Create();
        hc.Add(value1);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1, T2>(T1 value1, T2 value2)
    {
        HashCode hc = Create();
        hc.Add(value1);
        hc.Add(value2);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="value3">The third value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
    {
        HashCode hc = Create();
        hc.Add(value1);
        hc.Add(value2);
        hc.Add(value3);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="value3">The third value.</param>
    /// <param name="value4">The fourth value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
    {
        HashCode hc = Create();
        hc.Add(value1);
        hc.Add(value2);
        hc.Add(value3);
        hc.Add(value4);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="value3">The third value.</param>
    /// <param name="value4">The fourth value.</param>
    /// <param name="value5">The fifth value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
    {
        HashCode hc = Create();
        hc.Add(value1);
        hc.Add(value2);
        hc.Add(value3);
        hc.Add(value4);
        hc.Add(value5);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Combines multiple hash codes into a single hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="value3">The third value.</param>
    /// <param name="value4">The fourth value.</param>
    /// <param name="value5">The fifth value.</param>
    /// <param name="value6">The sixth value.</param>
    /// <returns>The combined hash code.</returns>
    public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
    {
        HashCode hc = Create();
        hc.Add(value1);
        hc.Add(value2);
        hc.Add(value3);
        hc.Add(value4);
        hc.Add(value5);
        hc.Add(value6);
        return hc.ToHashCode();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Round(uint hash, uint input)
    {
        return RotateLeft(hash + (input * Prime2), 13) * Prime1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint QueueRound(uint hash, uint queuedValue)
    {
        return RotateLeft(hash + (queuedValue * Prime3), 17) * Prime4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixState(uint v1, uint v2, uint v3, uint v4)
    {
        return RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixEmptyState()
    {
        return s_seed + Prime5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixFinal(uint hash)
    {
        hash ^= hash >> 15;
        hash *= Prime2;
        hash ^= hash >> 13;
        hash *= Prime3;
        hash ^= hash >> 16;
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int offset)
    {
        return (value << offset) | (value >> (32 - offset));
    }
    private static uint GenerateSeed()
    {
        // Use a fixed seed for deterministic hashing in source generators
        return 0x9E3779B9U;
    }
}