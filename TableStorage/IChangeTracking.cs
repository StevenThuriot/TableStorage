namespace TableStorage;

public interface IChangeTracking
{
    bool IsChanged();
    bool IsChanged(string field);
    void AcceptChanges();
    void SetChanged();
    void SetChanged(string field);
    ITableEntity GetEntity();
}
