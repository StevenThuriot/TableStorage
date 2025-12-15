namespace TableStorage.Linq;

internal static class ModelInfoCache<T>
{
    private static ModelInfo? s_modelInfo;

    public static ModelInfo GetModelInfo(ITableSetQueryHelper helper)
    {
        s_modelInfo ??= helper.GetModelInfo<T>();
        return s_modelInfo;
    }
}
