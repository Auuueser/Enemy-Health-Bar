using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class HealthBarPool
{
    private readonly ModConfig config;
    private readonly Transform parent;
    private readonly Stack<HealthBarView> pooledViews = new();
    private HealthBarStyle currentStyle;

    public HealthBarPool(ModConfig config, Transform parent)
    {
        this.config = config;
        this.parent = parent;
        currentStyle = HealthBarStyle.FromConfig(config);
    }

    public HealthBarView Get()
    {
        if (pooledViews.Count > 0)
        {
            var view = pooledViews.Pop();
            view.ApplyStyle(currentStyle);
            return view;
        }

        return HealthBarView.Create(parent, currentStyle);
    }

    public void Release(HealthBarView view)
    {
        view.SetVisible(false);
        pooledViews.Push(view);
    }

    public void RefreshStyle(HealthBarStyle style)
    {
        currentStyle = style;

        foreach (var view in pooledViews)
        {
            view.ApplyStyle(style);
        }
    }
}
