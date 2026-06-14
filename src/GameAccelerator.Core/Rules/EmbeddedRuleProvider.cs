using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameAccelerator.Core.Rules;

public class EmbeddedRuleProvider : IRuleProvider
{
    private List<AccelerationRule> _rules = new();

    public IEnumerable<AccelerationRule> GetRules() => _rules;

    public void Reload()
    {
        _rules = new List<AccelerationRule>();

        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Rules.BuiltIn.") && n.EndsWith(".json"));

        foreach (var name in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (ruleSet?.Rules != null)
                    _rules.AddRange(ruleSet.Rules);
            }
            catch { }
        }

        _rules = _rules.OrderBy(r => r.Priority).ToList();
    }

    private class RuleSet
    {
        public List<AccelerationRule> Rules { get; set; } = new();
    }
}
