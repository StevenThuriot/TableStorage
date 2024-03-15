namespace TableStorage;

public sealed class TableOptions
{
    internal TableOptions() { }

    public TableUpdateMode TableMode { get; set; }
    public int? PageSize { get; set; }
    public bool CreateTableIfNotExists { get; set; } = true;
}