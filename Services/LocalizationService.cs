using System;

namespace WormholeAutomationUI.Services;

public static class LocalizationService
{
    public static void ApplyLanguage(string languageCode)
    {
        var dictionary = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"Resources/StringResources.{languageCode}.xaml", UriKind.Relative)
        };

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(dictionary);
    }
}
