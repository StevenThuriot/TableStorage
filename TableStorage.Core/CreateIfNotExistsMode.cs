namespace TableStorage;

/// <summary>
/// Specifies the behavior to use when attempting to create a resource if it does not already exist.
/// </summary>
/// <remarks>Use this enumeration to control whether and how a resource creation operation should be performed
/// when the resource may already exist. The selected mode determines if the operation will attempt creation, and
/// whether it will cache the result of previous attempts.</remarks>
public enum CreateIfNotExistsMode
{
    /// <summary>
    /// Assumes the target already exists; does not attempt to create it.
    /// </summary>
    Disabled = 0,
    /// <summary>
    /// Will try to create the target only once. Results are stored in memory.
    /// </summary>
    Once = 1,
    /// <summary>
    /// Will always try to create the target.
    /// </summary>
    Always = 2
}
