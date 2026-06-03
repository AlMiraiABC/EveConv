namespace EveConv.Abstraction.Cache;

/// <summary>
/// Aggregate cache interface combining all cache capabilities.
/// </summary>
public interface ICache : IBasicCache<object>, IListCache<object>, IBatchCache<object>, IEnhanceCache<object>
{
}
