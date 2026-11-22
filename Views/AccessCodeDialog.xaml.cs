using System.Windows;

namespace TestingSystem.Views;

/// <summary>Запит пін-коду доступу до захищеного тесту.</summary>
public partial class AccessCodeDialog : Window
{
    public string Code => CodeBox.Password;

    public AccessCodeDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => CodeBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
