using System.Collections;

namespace TableStorage.Fluent;

internal readonly struct ValueCollectionHelper(IDictionary<string, object> backingDictionary, string discriminatorValue) : ICollection<object>
{
    private readonly IDictionary<string, object> _backingDictionary = backingDictionary;
    private readonly string _discriminatorValue = discriminatorValue;

    public int Count => _backingDictionary.Count + 1;
    public bool IsReadOnly => true;

    public IEnumerator<object> GetEnumerator()
    {
        yield return _discriminatorValue;
        foreach (object value in _backingDictionary.Values)
        {
            yield return value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(object item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(object item) => Equals(_discriminatorValue, item) || _backingDictionary.Values.Contains(item);

    public void CopyTo(object[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), "arrayIndex cannot be negative.");
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("The number of elements in the source collection is greater than the available space from arrayIndex to the end of the destination array.");
        }

        array[arrayIndex++] = _discriminatorValue;
        _backingDictionary.Values.CopyTo(array, arrayIndex);
    }

    public bool Remove(object item) => throw new NotSupportedException();
}