# TableStorage.Fluent.Extended

Provides fluent entity types for storing multiple entity types in a single Azure Table Storage table. This package enables polymorphic table storage by allowing you to store different entity types in the same table using a discriminator pattern.

This package supports **5 to 16 generic type parameters**. For support of 2 to 4 generic type parameters, see [TableStorage.Fluent](#related-packages).

## Features
- Store multiple entity types in a single table
- Type-safe discriminated union entities
- Support for 5 to 16 different entity types per table
- Three discriminator strategies: `$type`, `PartitionKey`, and `RowKey`
- Implicit conversion operators for seamless type handling
- Pattern matching with `SwitchCase` and `SwitchCaseOrDefault` methods

## Installation

```bash
dotnet add package TableStorage.Core
dotnet add package TableStorage
dotnet add package TableStorage.Fluent.Extended
```

## Usage

### Basic Fluent Entity

Define your entity types using the standard `[TableSet]` attribute:

```csharp
[TableSet]
public partial class Customer
{
    public string Name { get; set; }
    public string Email { get; set; }
}

[TableSet]
public partial class Order
{
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
}
```

Create a table that can store both types using `FluentTableEntity<T1, T2>`:

```csharp
[TableContext]
public partial class MyTableContext
{
    public TableSet<FluentTableEntity<Customer, Order>> MixedEntities { get; set; }
}
```

### Working with Fluent Entities

Store entities using implicit conversion:

```csharp
var customer = new Customer { Name = "John Doe", Email = "john@example.com" };
FluentTableEntity<Customer, Order> fluentEntity = customer; // Implicit conversion

await context.MixedEntities.AddEntityAsync(fluentEntity);
```

Retrieve and work with entities:

```csharp
var entity = await context.MixedEntities.GetEntityOrDefaultAsync(partitionKey, rowKey);

// Check the backing type
var backingType = entity.GetBackingType(); // Returns FluentBackingType.First or Second

// Get the actual type
var actualType = entity.GetActualType(); // Returns typeof(Customer) or typeof(Order)

// Pattern matching with SwitchCase
var result = entity.SwitchCase(
    case1: customer => $"Customer: {customer.Name}",
    case2: order => $"Order: {order.OrderNumber}"
);
```

### Discriminator Strategies

#### 1. `FluentTableEntity<T1, T2>` - Uses `$type` discriminator

The default fluent entity adds a `$type` property to distinguish between entity types:

```csharp
public TableSet<FluentTableEntity<Customer, Order>> Entities { get; set; }
```

#### 2. `FluentPartitionTableEntity<T1, T2>` - Uses PartitionKey as discriminator

Uses the `PartitionKey` to determine the entity type. The partition key will be set to the type name:

```csharp
public TableSet<FluentPartitionTableEntity<Customer, Order>> Entities { get; set; }
```

#### 3. `FluentRowTypeTableEntity<T1, T2>` - Uses RowKey as discriminator

Uses the `RowKey` to determine the entity type. The row key will be set to the type name:

```csharp
public TableSet<FluentRowTypeTableEntity<Customer, Order>> Entities { get; set; }
```

### Multiple Entity Types

Fluent entities support up to 16 different types. Simply add more type parameters:

```csharp
public TableSet<FluentTableEntity<Type1, Type2, Type3, Type4, Type5>> MultiTypeTable { get; set; }
```

### Safe Type Extraction

Use `SwitchCaseOrDefault` for safe handling when you're not sure which type is stored:

```csharp
var value = entity.SwitchCaseOrDefault(
    case1: customer => ProcessCustomer(customer),
    case2: order => ProcessOrder(order),
    defaultCase: () => "Unknown type"
);
```

Directly get the underlying value:

```csharp
var value = entity.GetValue(); // Throws if NotInitialized
var valueOrNull = entity.GetValueOrDefault(); // Returns null if NotInitialized
```

### Implicit Conversions

Fluent entities support implicit conversions in both directions:

```csharp
// To FluentTableEntity
Customer customer = new Customer { Name = "Jane" };
FluentTableEntity<Customer, Order> fluent = customer;

// From FluentTableEntity
Customer retrievedCustomer = fluent; // Implicit cast
```

## Advanced Scenarios

### Querying Mixed Entity Tables

You can query fluent entity tables using standard LINQ operations:

```csharp
await foreach (var entity in context.MixedEntities)
{
    var type = entity.GetBackingType();
    
    switch (type)
    {
        case FluentBackingType.First:
            Customer customer = entity;
            Console.WriteLine($"Customer: {customer.Name}");
            break;
        case FluentBackingType.Second:
            Order order = entity;
            Console.WriteLine($"Order: {order.OrderNumber}");
            break;
    }
}
```

### Use Cases

Fluent entities are ideal for scenarios where:
- You need to store related but different entity types in a single table for efficient partitioning
- You want to maintain a unified query interface across multiple entity types
- You need polymorphic storage without creating separate tables
- You're implementing event sourcing or activity streams with multiple event types

## Related Packages

- **[TableStorage.Fluent](https://www.nuget.org/packages/TableStorage.Fluent)** - Base package supporting 2 to 4 generic type parameters
- [TableStorage](https://www.nuget.org/packages/TableStorage)
- [TableStorage.Core](https://www.nuget.org/packages/TableStorage.Core)
