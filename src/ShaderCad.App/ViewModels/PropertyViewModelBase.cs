using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShaderCad.App.ViewModels;

/// <summary>
/// Avalonia用の基本的なViewModel基底クラス
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Inspectorに表示されるプロパティの基底クラス
/// </summary>
public abstract class PropertyViewModelBase : ViewModelBase
{
    public string DisplayName { get; }

    protected PropertyViewModelBase(string displayName)
    {
        DisplayName = displayName;
    }
}
