namespace TableStorage;

public sealed class TableOptions
{
    internal TableOptions() { }

    public TableUpdateMode TableMode { get; set; } = TableUpdateMode.Replace;

    public int? PageSize { get; set; }

    public CreateIfNotExistsMode CreateTableIfNotExists { get; set; } = CreateIfNotExistsMode.Always;

    public BulkOperation BulkOperation { get; set; } = BulkOperation.Replace;

    public TransactionSafety TransactionSafety { get; set; } = TransactionSafety.Enabled;

    public int TransactionChunkSize
    {
        get;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Transaction chunk size must be greater than 0.");
            }

            field = value;
        }
    } = 100;

    public bool ChangesOnly { get; set; }
    public bool OptimizeQueries { get; set; } = true;
}