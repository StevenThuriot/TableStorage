using System.Collections;

namespace TableStorage;

internal static class Extensions
{
    public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int size)
    {
        return source switch
        {
            ICollection<T> genericCollection when genericCollection.Count <= size => [source],
            ICollection collection when collection.Count <= size => [source],
            IReadOnlyCollection<T> readOnlyGenericCollection when readOnlyGenericCollection.Count <= size => [source],
            T[] array => array.Length != 0 ? ArrayChunkIterator(array, size) : [],
            _ => EnumerableChunkIterator(source, size)
        };
    }

    private static IEnumerable<T[]> ArrayChunkIterator<T>(T[] source, int size)
    {
        int index = 0;
        while (index < source.Length)
        {
            T[] chunk = new ReadOnlySpan<T>(source, index, Math.Min(size, source.Length - index)).ToArray();
            index += chunk.Length;
            yield return chunk;
        }
    }

    private static IEnumerable<IEnumerable<T>> EnumerableChunkIterator<T>(IEnumerable<T> source, int size)
    {
        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return GetChunk(enumerator, size);
        }

        static IEnumerable<T> GetChunk(IEnumerator<T> enumerator, int chunkSize)
        {
            do
            {
                yield return enumerator.Current;
            }
            while (--chunkSize > 0 && enumerator.MoveNext());
        }
    }
}