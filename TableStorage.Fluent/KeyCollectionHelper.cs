using System.Collections;

namespace TableStorage.Fluent;

internal readonly struct KeyCollectionHelper(IDictionary<string, object> backingDictionary, string discriminator) : ICollection<string>
{
    private readonly string _discriminator = discriminator;
    private readonly IDictionary<string, object> _backingDictionary = backingDictionary;

    public int Count => _backingDictionary.Count + 1;
    public bool IsReadOnly => true;

    public IEnumerator<string> GetEnumerator()
    {
        yield return _discriminator;
        foreach (string key in _backingDictionary.Keys)
        {
            yield return key;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(string item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(string item) => item == _discriminator || _backingDictionary.ContainsKey(item);

    public void CopyTo(string[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The number of elements in the source collection is greater than the available space from arrayIndex to the end of the destination array.");
        }

        array[arrayIndex++] = _discriminator;
        _backingDictionary.Keys.CopyTo(array, arrayIndex);
    }

    public bool Remove(string item) => throw new NotSupportedException();
}