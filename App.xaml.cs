using System.Windows;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using TestingSystem.Abstractions;
using TestingSystem.Infrastructure;
using TestingSystem.Models;
using TestingSystem.Persistence;
using TestingSystem.Services;
using TestingSystem.Services.Ai;
using TestingSystem.ViewModels;
using TestingSystem.Views;

namespace TestingSystem;

/// <summary>Назва поточного провайдера сховища (показується в індикаторі підключення).</summary>
public sealed record StorageInfo(string Name);

/// <summary>Набір сервісів застосунку: вікна отримують його повністю і працюють лише з інтерфейсами/сервісами.</summary>
public sealed record AppServices(
    IUserRepository Users,
    IGroupRepository Groups,
    ICategoryRepository Categories,
    ITestRepository Tests,
    ILectureRepository Lectures,
    IResultRepository Results,
    IAuthService Auth,
    ILocalizationService Localization,
    IThemeService Theme,
    GradingEngine Grading,
    TestSessionService Sessions,
    StatisticsService Stats,
    IReportExportService Export,
    IInactivityMonitor Inactivity,
    ISessionLockService SessionLock,
    IAiAssistant AiAssistant,
    IViewModelFactory ViewModels,
    StorageInfo Storage);

public partial class App : Application, ISessionNavigator
{
    private ServiceProvider _provider = null!;
    private AppServices _services = null!;

    /// <summary>
    /// ===================== ТОЧКА ВХОДУ / COMPOSITION ROOT / DI =====================
    /// Єдине місце, де обираються конкретні реалізації.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        // ---------- СХОВИЩЕ: зараз — оперативна пам'ять ----------
        sc.AddSingleton<IUserRepository, InMemoryUserRepository>();
        sc.AddSingleton<IGroupRepository, InMemoryGroupRepository>();
        sc.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
        sc.AddSingleton<ITestRepository, InMemoryTestRepository>();
        sc.AddSingleton<ILectureRepository, InMemoryLectureRepository>();
        sc.AddSingleton<IResultRepository, InMemoryResultRepository>();
        sc.AddSingleton(new StorageInfo("In-Memory"));

        // ---------- ПЕРЕХІД НА SQLite / PostgreSQL / EF Core ----------
        // Змінюються ЛИШЕ ці реєстрації (класи пишуться на тих самих інтерфейсах), наприклад:
        //   sc.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=testing.db"));  // або UseNpgsql(...)
        //   sc.AddScoped<IUserRepository,   EfUserRepository>();
        //   sc.AddScoped<ITestRepository,   EfTestRepository>();
        //   sc.AddScoped<IResultRepository, EfResultRepository>();  ... і т.д.
        //   sc.AddSingleton(new StorageInfo("SQLite"));
        // GradingEngine, TestSessionService, StatisticsService, AuthService і всі вікна залишаються без змін.

        // ---------- сервіси та бізнес-логіка (не залежать від сховища) ----------
        sc.AddSingleton<GradingEngine>();
        sc.AddSingleton<IAuthService, AuthService>();
        sc.AddSingleton<ILocalizationService, LocalizationService>();
        sc.AddSingleton<IThemeService, ThemeService>();
        sc.AddSingleton<TestSessionService>();
        sc.AddSingleton<StatisticsService>();
        sc.AddSingleton<IReportExportService, ReportExportService>();

        // ---------- лекції ----------
        sc.AddSingleton<ILectureService, LectureService>();

        // ---------- AI: провайдери — взаємозамінні стратегії; обирається в налаштуваннях, без змін коду ----------
        sc.AddSingleton(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });   // таймаут задає AiClientRouter
        sc.AddSingleton<IAiSettingsStore, JsonAiSettingsStore>();
        sc.AddSingleton<IAiProvider, MockAiProvider>();              // офлайн-демо без ключа
        sc.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();    // OpenAI, OpenRouter, Ollama, LM Studio...
        sc.AddSingleton<IAiProvider, AnthropicProvider>();           // Anthropic Messages API
        sc.AddSingleton<IAiProvider, GeminiProvider>();              // Google Gemini API
        sc.AddSingleton<IAiClient, AiClientRouter>();
        sc.AddSingleton<IAiAssistant, AiAssistant>();

        // ---------- MVVM-інфраструктура: діалоги, фабрика ViewModel, міст до редактора тестів ----------
        sc.AddSingleton<IDialogService>(_ => new WpfDialogService()
            .Register<LectureEditorViewModel, LectureEditorWindow>()
            .Register<AiSettingsViewModel, AiSettingsWindow>());
        sc.AddSingleton<IViewModelFactory, ViewModelFactory>();
        sc.AddSingleton<ITestEditorLauncher, WpfTestEditorLauncher>();

        // ---------- блокування сеансу після бездіяльності (кожна залежність — окрема абстракція) ----------
        sc.AddSingleton(new InactivitySettings(Timeout: TimeSpan.FromMinutes(30), MaxUnlockAttempts: 5));
        //   Для перевірки поставте, наприклад, TimeSpan.FromSeconds(20) — екран блокування з'явиться через 20 секунд.
        sc.AddSingleton<IClock, SystemClock>();
        sc.AddSingleton<IActivitySource, WpfInputActivitySource>();
        sc.AddTransient<ITicker>(_ => new DispatcherTicker(TimeSpan.FromSeconds(5)));
        sc.AddSingleton<IInactivityMonitor, InactivityMonitor>();
        sc.AddSingleton<ILockScreenPresenter, WpfLockScreenPresenter>();
        sc.AddSingleton<ISessionNavigator>(_ => this);          // навігацію виконує App
        sc.AddSingleton<ISessionLockService, SessionLockService>();

        sc.AddSingleton<AppServices>();

        _provider = sc.BuildServiceProvider();

        // Демо-дані (для реальної БД — прибрати або викликати лише для порожньої бази)
        DemoDataSeeder.Seed(
            _provider.GetRequiredService<IUserRepository>(),
            _provider.GetRequiredService<IGroupRepository>(),
            _provider.GetRequiredService<ICategoryRepository>(),
            _provider.GetRequiredService<ITestRepository>(),
            _provider.GetRequiredService<IResultRepository>(),
            _provider.GetRequiredService<ILectureRepository>(),
            _provider.GetRequiredService<GradingEngine>());

        _services = _provider.GetRequiredService<AppServices>();

        TExtension.Service = _services.Localization;   // потрібно лише XAML-розширенню {loc:T ...}
        _services.Theme.SetTheme(AppTheme.Light);

        ShowLogin();
    }

    public void ShowLogin() => new LoginWindow(_services, ShowWorkspace).Show();

    /// <summary>
    /// ISessionNavigator: примусовий вихід (наприклад, з екрана блокування). Завершує сеанс, показує вхід
    /// і закриває всі робочі вікна та діалоги — спочатку дочірні, потім решту. Екран блокування закриває сам себе.
    /// </summary>
    public void NavigateToLogin()
    {
        _services.Auth.Logout();

        var toClose = Windows.OfType<Window>()
            .Where(w => w is not SessionLockWindow && w is not LoginWindow)
            .OrderByDescending(w => w.Owner != null)
            .ToList();

        ShowLogin();
        foreach (var window in toClose) window.Close();
    }

    /// <summary>Маршрутизація за роллю: учень — кабінет учня, вчитель і адмін — робоче місце викладача.</summary>
    private void ShowWorkspace(User user)
    {
        Window w = user.Role == Role.Student
            ? new StudentWindow(_services, ShowLogin)
            : new TeacherWindow(_services, ShowLogin);
        w.Show();
    }
}
