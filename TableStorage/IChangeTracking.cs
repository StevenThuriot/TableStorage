namespace TableStorage;

public interface IChangeTracking
{
    public bool IsChanged();
    public bool IsChanged(string field);
    public void AcceptChanges();
    public void SetChanged();
    public void SetChanged(string field);
    public ITableEntity GetEntity();
}
