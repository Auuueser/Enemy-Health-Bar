using System.Collections.Generic;

namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class TransientIdRetentionSet<T>
    where T : notnull
{
    private readonly Dictionary<T, int> missingScanCounts = new();
    private readonly HashSet<T> retainedIds = new();
    private readonly List<T> missingIds = new();
    private readonly List<T> removedIds = new();
    private readonly int missingScanRetention;

    public TransientIdRetentionSet(int missingScanRetention)
    {
        this.missingScanRetention = missingScanRetention < 1 ? 1 : missingScanRetention;
    }

    public ISet<T> Update(ISet<T> seenIds)
    {
        retainedIds.Clear();
        missingIds.Clear();
        removedIds.Clear();

        foreach (var seenId in seenIds)
        {
            missingScanCounts[seenId] = 0;
            retainedIds.Add(seenId);
        }

        foreach (var pair in missingScanCounts)
        {
            if (seenIds.Contains(pair.Key))
            {
                continue;
            }

            var missingScanCount = pair.Value + 1;
            if (missingScanCount >= missingScanRetention)
            {
                removedIds.Add(pair.Key);
            }
            else
            {
                missingIds.Add(pair.Key);
                retainedIds.Add(pair.Key);
            }
        }

        for (var i = 0; i < missingIds.Count; i++)
        {
            var missingId = missingIds[i];
            missingScanCounts[missingId]++;
        }

        for (var i = 0; i < removedIds.Count; i++)
        {
            missingScanCounts.Remove(removedIds[i]);
        }

        return retainedIds;
    }

    public void Clear()
    {
        missingScanCounts.Clear();
        retainedIds.Clear();
        missingIds.Clear();
        removedIds.Clear();
    }
}
