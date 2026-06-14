namespace GameAccelerator.Core.Rules;

public interface IRuleProvider
{
    IEnumerable<AccelerationRule> GetRules();
    void Reload();
}
