namespace TableStorage;

public class TableOptions
{
    public bool AutoTimestamps { get; set; }
    public TableUpdateMode TableMode { get; set; }
    public int? PageSize { get; set; }
}