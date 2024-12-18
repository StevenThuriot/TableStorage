namespace TableStorage;

public interface IChangeTracking
{
    bool IsDirty();
    bool IsDirty(string field);
    void AcceptChanges();
}