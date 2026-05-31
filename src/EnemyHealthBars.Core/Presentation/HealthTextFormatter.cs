using Auuueser.EnemyHealthBars.Core.Configuration;

namespace Auuueser.EnemyHealthBars.Core.Presentation;

public static class HealthTextFormatter
{
    public static string Format(int currentHealth, int maxHealth, HealthTextFormat format)
    {
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        if (maxHealth < 0)
        {
            maxHealth = 0;
        }

        switch (format)
        {
            case HealthTextFormat.CurrentOnly:
                return currentHealth.ToString();
            case HealthTextFormat.PercentOnly:
                if (maxHealth <= 0)
                {
                    return "0%";
                }

                var percent = (int)System.Math.Round((double)currentHealth * 100d / maxHealth);
                if (percent < 0)
                {
                    percent = 0;
                }
                else if (percent > 100)
                {
                    percent = 100;
                }

                return percent.ToString() + "%";
            default:
                return currentHealth.ToString() + " / " + maxHealth.ToString();
        }
    }
}
