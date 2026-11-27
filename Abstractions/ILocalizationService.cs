using System.ComponentModel;

namespace TestingSystem.Abstractions;

public enum AppLanguage { UA, EN }

/// <summary>Локалізація «на льоту»: індексатор + PropertyChanged("Item[]") оновлюють XAML-прив'язки {loc:T Key}.</summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    AppLanguage CurrentLanguage { get; }
    void SetLanguage(AppLanguage language);
    string this[string key] { get; }
    string Format(string key, params object[] args);
    event EventHandler? LanguageChanged;
}
