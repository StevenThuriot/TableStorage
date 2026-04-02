namespace TableStorage.Linq;

internal static class ModelInfoCache<T>
{
    private static ModelInfo? s_modelInfo;

    public static ModelInfo GetModelInfo(ITableSetQueryHelper helper)
    {
        if (s_modelInfo is null)
        {
            Interlocked.CompareExchange(ref s_modelInfo, helper.GetModelInfo<T>(), null);
        }

        return s_modelInfo;
    }
}