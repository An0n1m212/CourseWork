using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TestingSystem.Infrastructure;

/// <summary>
/// Поиск внутри выпадающего списка: loc:ComboBoxSearch.Enabled="True".
/// ComboBox становится редактируемым; список фильтруется ТОЛЬКО когда пользователь печатает
/// (программная смена текста при загрузке данных список не открывает). Клик в другом месте окна снимает фокус и выделение,
/// Enter выбирает первый найденный пункт, при закрытии списка фильтр сбрасывается.
/// </summary>
public static class ComboBoxSearch
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ComboBoxSearch), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static readonly DependencyProperty LastItemProperty =
        DependencyProperty.RegisterAttached("LastItem", typeof(object), typeof(ComboBoxSearch));
    private static readonly DependencyProperty HookedBoxProperty =
        DependencyProperty.RegisterAttached("HookedBox", typeof(TextBox), typeof(ComboBoxSearch));
    private static readonly DependencyProperty TypingProperty =
        DependencyProperty.RegisterAttached("Typing", typeof(bool), typeof(ComboBoxSearch));
    private static readonly DependencyProperty BusyProperty =
        DependencyProperty.RegisterAttached("Busy", typeof(bool), typeof(ComboBoxSearch));
    private static readonly DependencyProperty WindowProperty =
        DependencyProperty.RegisterAttached("HookedWindow", typeof(Window), typeof(ComboBoxSearch));
    private static readonly DependencyProperty WindowHandlerProperty =
        DependencyProperty.RegisterAttached("WindowHandler", typeof(MouseButtonEventHandler), typeof(ComboBoxSearch));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox cb || e.NewValue is not true) return;

        cb.IsEditable = true;
        cb.IsTextSearchEnabled = false;
        cb.StaysOpenOnEdit = true;

        cb.Loaded += (_, _) => { Hook(cb); HookWindow(cb); };
        cb.Unloaded += (_, _) => UnhookWindow(cb);
        cb.SelectionChanged += (_, _) =>
        {
            if (cb.SelectedItem != null) cb.SetValue(LastItemProperty, cb.SelectedItem);
        };
        cb.DropDownClosed += (_, _) => Restore(cb);
        cb.IsKeyboardFocusWithinChanged += (_, args) =>
        {
            if (args.NewValue is false) { cb.IsDropDownOpen = false; Restore(cb); }
        };
    }

    // ------------------------------------------------------------ текстовое поле внутри ComboBox

    private static void Hook(ComboBox cb)
    {
        cb.ApplyTemplate();
        if (cb.Template.FindName("PART_EditableTextBox", cb) is not TextBox tb) return;
        if (ReferenceEquals(cb.GetValue(HookedBoxProperty), tb)) return;
        cb.SetValue(HookedBoxProperty, tb);

        // «печатает пользователь» — только такие изменения текста запускают поиск
        tb.PreviewTextInput += (_, _) => cb.SetValue(TypingProperty, true);
        DataObject.AddPastingHandler(tb, (_, _) => cb.SetValue(TypingProperty, true));
        tb.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Back or Key.Delete) cb.SetValue(TypingProperty, true);
            else if (e.Key == Key.Enter && cb.IsDropDownOpen)
            {
                if (cb.Items.Count > 0) cb.SelectedItem = cb.Items[0];
                cb.IsDropDownOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && cb.IsDropDownOpen)
            {
                cb.IsDropDownOpen = false;
                e.Handled = true;
            }
        };
        tb.TextChanged += (_, _) => OnTextChanged(cb, tb);

        // клик по тексту открывает список (как по стрелке); при получении фокуса текст выделяется целиком
        tb.PreviewMouseLeftButtonDown += (_, _) => { if (!cb.IsDropDownOpen) cb.IsDropDownOpen = true; };
        tb.GotKeyboardFocus += (_, _) =>
            tb.Dispatcher.BeginInvoke(new Action(tb.SelectAll), DispatcherPriority.Background);
    }

    private static void OnTextChanged(ComboBox cb, TextBox tb)
    {
        if (!(bool)cb.GetValue(TypingProperty) || (bool)cb.GetValue(BusyProperty)) return;
        cb.SetValue(TypingProperty, false);

        string typed = tb.Text;
        tb.Dispatcher.BeginInvoke(new Action(() => ApplyFilter(cb, tb, typed)), DispatcherPriority.Background);
    }

    private static void ApplyFilter(ComboBox cb, TextBox tb, string typed)
    {
        cb.SetValue(BusyProperty, true);
        try
        {
            string q = typed.Trim();
            cb.Items.Filter = q.Length == 0 ? null : o => TextOf(cb, o).Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!cb.IsDropDownOpen) cb.IsDropDownOpen = true;

            // фильтр мог снять выбор и очистить текст — возвращаем то, что набрал пользователь
            if (tb.Text != typed) tb.Text = typed;
            tb.CaretIndex = typed.Length;
        }
        finally { cb.SetValue(BusyProperty, false); }
    }

    /// <summary>Сбрасывает фильтр, возвращает прежний выбор и убирает выделение текста.</summary>
    private static void Restore(ComboBox cb)
    {
        cb.SetValue(BusyProperty, true);
        try
        {
            cb.Items.Filter = null;
            if (cb.SelectedItem == null && cb.GetValue(LastItemProperty) is { } last) cb.SelectedItem = last;

            if (cb.GetValue(HookedBoxProperty) is TextBox tb)
            {
                if (cb.SelectedItem != null) tb.Text = TextOf(cb, cb.SelectedItem);
                tb.SelectionLength = 0;
            }
        }
        finally { cb.SetValue(BusyProperty, false); }
    }

    // ------------------------------------------------------------ клик вне списка снимает фокус

    private static void HookWindow(ComboBox cb)
    {
        var window = Window.GetWindow(cb);
        if (window is null || ReferenceEquals(cb.GetValue(WindowProperty), window)) return;
        UnhookWindow(cb);

        MouseButtonEventHandler handler = (_, e) =>
        {
            if (!cb.IsKeyboardFocusWithin || cb.IsDropDownOpen) return;
            if (!IsInside(cb, e.OriginalSource as DependencyObject)) Keyboard.ClearFocus();
        };
        cb.SetValue(WindowProperty, window);
        cb.SetValue(WindowHandlerProperty, handler);
        window.PreviewMouseDown += handler;
    }

    private static void UnhookWindow(ComboBox cb)
    {
        if (cb.GetValue(WindowProperty) is Window w && cb.GetValue(WindowHandlerProperty) is MouseButtonEventHandler h)
            w.PreviewMouseDown -= h;
        cb.ClearValue(WindowProperty);
        cb.ClearValue(WindowHandlerProperty);
    }

    private static bool IsInside(ComboBox cb, DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, cb)) return true;
            source = (source as FrameworkElement)?.Parent
                     ?? (source as FrameworkElement)?.TemplatedParent
                     ?? (source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : null)
                     ?? LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private static string TextOf(ComboBox cb, object item)
    {
        string path = cb.DisplayMemberPath;
        if (!string.IsNullOrEmpty(path) && item.GetType().GetProperty(path)?.GetValue(item) is { } v)
            return v.ToString() ?? string.Empty;
        return item.ToString() ?? string.Empty;
    }
}
