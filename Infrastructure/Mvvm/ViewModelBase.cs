using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestingSystem.Infrastructure.Mvvm;

/// <summary>Базовий клас ViewModel: повідомлення View про зміну властивостей (INotifyPropertyChanged).</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
