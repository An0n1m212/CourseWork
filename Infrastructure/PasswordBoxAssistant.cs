using System.Windows;
using System.Windows.Controls;

namespace TestingSystem.Infrastructure;

/// <summary>
/// Attached-властивість для прив'язки PasswordBox до ViewModel без посилання на контрол у ViewModel:
///   loc:PasswordBoxAssistant.BoundPassword="{Binding Password, Mode=TwoWay}"
/// </summary>
public static class PasswordBoxAssistant
{
    // Значення за замовчуванням null (а не ""), щоб перша прив'язка порожнього рядка викликала callback і підписала обробник
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword", typeof(string), typeof(PasswordBoxAssistant),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty UpdatingProperty = DependencyProperty.RegisterAttached(
        "Updating", typeof(bool), typeof(PasswordBoxAssistant), new PropertyMetadata(false));

    public static string? GetBoundPassword(DependencyObject d) => (string?)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string? value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;

        box.PasswordChanged -= OnPasswordChanged;
        if (!(bool)box.GetValue(UpdatingProperty))
            box.Password = (string?)e.NewValue ?? string.Empty;   // ViewModel -> View (наприклад, очищення після помилки)
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);                       // View -> ViewModel
        box.SetValue(UpdatingProperty, false);
    }
}
