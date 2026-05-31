using System;
using System.Collections.Generic;
using System.Text;

namespace Auuueser.EnemyHealthBars.Core.GameData;

public sealed class EnemyMaxHealthLookup
{
    private readonly Dictionary<string, int> maxHealthByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unresolvedNames = new(StringComparer.OrdinalIgnoreCase);

    public EnemyMaxHealthLookup(EnemyHealthDefault[] defaults)
    {
        if (defaults == null)
        {
            throw new ArgumentNullException(nameof(defaults));
        }

        foreach (var item in defaults)
        {
            if (!string.IsNullOrWhiteSpace(item.EnemyName) && item.MaxHealth > 0)
            {
                maxHealthByName[item.EnemyName] = item.MaxHealth;
            }
        }
    }

    public int StoreResolvedStrictMaxHealth(string? key, int resolvedMaxHealth)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        if (resolvedMaxHealth > 0)
        {
            unresolvedNames.Remove(key);
            maxHealthByName[key] = resolvedMaxHealth;
            return resolvedMaxHealth;
        }

        unresolvedNames.Add(key);
        return 0;
    }

    public bool TryGetStrictMaxHealth(string? key, out int maxHealth)
    {
        maxHealth = 0;
        return !string.IsNullOrWhiteSpace(key) && maxHealthByName.TryGetValue(key, out maxHealth);
    }

    public bool IsUnresolved(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && unresolvedNames.Contains(key);
    }

    public int UnresolvedCount => unresolvedNames.Count;

    public string FormatUnresolvedNamesForDiagnostics(int maxNames)
    {
        if (unresolvedNames.Count == 0)
        {
            return "none";
        }

        if (maxNames < 1)
        {
            maxNames = 1;
        }

        var builder = new StringBuilder();
        var written = 0;
        foreach (var name in unresolvedNames)
        {
            if (written >= maxNames)
            {
                break;
            }

            if (written > 0)
            {
                builder.Append(", ");
            }

            builder.Append(name);
            written++;
        }

        if (unresolvedNames.Count > written)
        {
            builder.Append(", +");
            builder.Append(unresolvedNames.Count - written);
            builder.Append(" more");
        }

        return builder.ToString();
    }
}
