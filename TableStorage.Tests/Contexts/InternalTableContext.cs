using TableStorage.Fluent;
using TableStorage.Tests.Models;

namespace TableStorage.Tests.Contexts;

[TableContext]
internal sealed partial class InternalTableContext
{
    public TableSet<FluentPartitionTableEntity<InternalFluentModelA, InternalFluentModelB>> InternalFluentModels { get; set; }
}
