using System.Windows;
using TestingSystem.Models;
using TestingSystem.ViewModels;

namespace TestingSystem.Views;

/// <summary>Робоче місце викладача (ролі Teacher і Admin).</summary>
public partial class TeacherWindow : Window
{
    private readonly AppServices _services;

    public TeacherWindow(AppServices services, Action onLogout)
    {
        InitializeComponent();
        _services = services;

        // Обмеження прав за роллю: вікно недоступне учням
        if (!services.Auth.IsInRole(Role.Teacher, Role.Admin))
            throw new InvalidOperationException("Access denied: Teacher or Admin role required.");

        // Кожен View оновлює дані під час показу (подія Loaded), тому вкладки завжди актуальні
        TestsTab.Content = new TestsManagementView(services);
        // Нові вкладки — на MVVM: ViewModel створює фабрика з DI, View лише відображає
        LecturesTab.Content = new LecturesView(services.ViewModels.Create<LecturesViewModel>());
        AiTab.Content = new AiAssistantView(services.ViewModels.Create<AiAssistantViewModel>());
        ReviewTab.Content = new ReviewView(services);
        AnalyticsTab.Content = new AnalyticsView(services);
        // Керування групами та категоріями — лише для адміна (викладачу вкладки приховані, View не створюються)
        if (services.Auth.IsInRole(Role.Admin))
        {
            GroupsTab.Content = new GroupsView(services);
            CategoriesTab.Content = new CategoriesView(services);
        }
        else
        {
            GroupsTab.Visibility = Visibility.Collapsed;
            CategoriesTab.Visibility = Visibility.Collapsed;
        }
        ProfileTab.Content = new ProfileView(services);

        Header.Init(services);
        Header.ProfileRequested += (_, _) => Tabs.SelectedItem = ProfileTab;
        Header.LogoutRequested += (_, _) =>
        {
            _services.Auth.Logout();
            onLogout();
            Close();
        };
    }
}
