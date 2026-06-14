using System.Text.RegularExpressions;

namespace GameAccelerator.Core.Rules;

public class RuleEngine
{
    private readonly IRuleProvider _provider;
    private List<AccelerationRule> _rules = new();

    public RuleEngine(IRuleProvider provider)
    {
        _provider = provider;
        Reload();
    }

    public void Reload()
    {
        _provider.Reload();
        _rules = _provider.GetRules()
            .OrderBy(r => r.Priority)
            .ToList();
    }

    public AccelerationRule? Match(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return null;

        foreach (var rule in _rules)
        {
            if (MatchesPattern(domain, rule.DomainPattern))
                return rule;
        }

        return null;
    }

    private static bool MatchesPattern(string domain, string pattern)
    {
        if (pattern.StartsWith("*"))
        {
            var suffix = pattern[1..]; // remove *
            return domain.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.StartsWith("regex:"))
        {
            var regex = new Regex(pattern[6..], RegexOptions.IgnoreCase);
            return regex.IsMatch(domain);
        }

        return string.Equals(domain, pattern, StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerable<AccelerationRule> GetAllRules() => _rules;

    public IEnumerable<AccelerationRule> GetHostsRedirectRules()
    {
        return _rules.Where(r => r.ProxyStrategy == ProxyStrategy.HostsRedirect);
    }
}
