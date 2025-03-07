namespace TableStorage;

public sealed class TableOptions
{
    internal TableOptions() { }

    public TableUpdateMode TableMode { get; set; } = TableUpdateMode.Merge;

    public int? PageSize { get; set; }

    public bool CreateTableIfNotExists { get; set; } = true;

    public BulkOperation BulkOperation { get; set; } = BulkOperation.Replace;

    public TransactionSafety TransactionSafety { get; set; } = TransactionSafety.Enabled;

    private int _transactionChunkSize = 100;
    public int TransactionChunkSize
    {
        get => _transactionChunkSize;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Transaction chunk size must be greater than 0.");
            }

            _transactionChunkSize = value;
        }
    }

    public bool ChangesOnly { get; set; }
}
