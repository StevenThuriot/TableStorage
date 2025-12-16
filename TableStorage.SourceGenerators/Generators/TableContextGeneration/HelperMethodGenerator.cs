using System.Text;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates helper methods for creating table and blob sets.
/// </summary>
internal static class HelperMethodGenerator
{
    /// <summary>
    /// Generates all helper methods based on available capabilities.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateHelperMethods(StringBuilder sb, bool hasTables, bool hasBlobs)
    {
        if (hasBlobs)
        {
            GenerateBlobHelperMethods(sb);
        }

        if (hasTables)
        {
            GenerateTableHelperMethods(sb);
        }
    }

    private static void GenerateBlobHelperMethods(StringBuilder sb)
    {
        sb.Append(@"

        public global::TableStorage.BlobSet<T> GetBlobSet<T>(string tableName)
            where T : class, global::TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName, global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.BlobSet<T> GetBlobSet<T>(string tableName, global::System.Func<global::System.Type, global::TableStorage.ModelInfo> infoProvider)
            where T : class, global::TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName, infoProvider ?? global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.AppendBlobSet<T> GetAppendBlobSet<T>(string tableName)
            where T : class, global::TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateAppendSet<T>(tableName, global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.AppendBlobSet<T> GetAppendBlobSet<T>(string tableName, global::System.Func<global::System.Type, global::TableStorage.ModelInfo> infoProvider)
            where T : class, global::TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateAppendSet<T>(tableName, infoProvider ?? global::TableStorage.ModelInfoProvider.GetInfo);
        }");
    }

    private static void GenerateTableHelperMethods(StringBuilder sb)
    {
        sb.Append(@"

        public global::TableStorage.TableSet<T> GetTableSet<T>(string tableName)
            where T : class, global::Azure.Data.Tables.ITableEntity, new()
        {
            return _creator.CreateSet<T>(tableName, global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.TableSet<T> GetTableSet<T>(string tableName, global::System.Func<global::System.Type, global::TableStorage.ModelInfo> infoProvider)
            where T : class, global::Azure.Data.Tables.ITableEntity, new()
        {
            return _creator.CreateSet<T>(tableName, infoProvider ?? global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.TableSet<T> GetTableSetWithChangeTracking<T>(string tableName)
            where T : class, global::Azure.Data.Tables.ITableEntity, global::TableStorage.IChangeTracking, new()
        {
            return _creator.CreateSetWithChangeTracking<T>(tableName, global::TableStorage.ModelInfoProvider.GetInfo);
        }

        public global::TableStorage.TableSet<T> GetTableSetWithChangeTracking<T>(string tableName, global::System.Func<global::System.Type, global::TableStorage.ModelInfo> infoProvider)
            where T : class, global::Azure.Data.Tables.ITableEntity, global::TableStorage.IChangeTracking, new()
        {
            return _creator.CreateSetWithChangeTracking<T>(tableName, infoProvider ?? global::TableStorage.ModelInfoProvider.GetInfo);
        }");
    }
}