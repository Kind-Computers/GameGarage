using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;

namespace GameGarage.Utilities;

internal static class GUIUtilities
{
    public static void UpdateLogListView(ListView list, string line, bool isNewLine, bool lastLineWasNewLine, bool highlight)
    {
        bool follow = list.SelectedIndex < 0 || list.SelectedIndex == list.Items.Count - 1;
        ListViewItem item;
        if (!lastLineWasNewLine && list.Items.Count > 0)
        {
            item = (ListViewItem)list.Items[list.Items.Count - 1];
            item.Content = line;
        }
        else
        {
            item = new ListViewItem { Content = line };
            list.Items.Add(item);
        }
        item.FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal;
        if (follow)
        {
            list.SelectedIndex = list.Items.Count - 1;
            // Virtualized scrolling; do not force synchronous layout on every output line.
            if (list.IsVisible) list.ScrollIntoView(item);
        }
    }


    public static void CopyText(string text)
    {
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
            if (owner is null) return;
            MessageBox.Show(owner, GameGarage.Resources.UiText.Get("ClipboardError"),
                GameGarage.Resources.UiText.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }
}

internal sealed class ScopedTaskProgress : IDisposable
{
    private readonly TaskbarItemInfo bar;
    private readonly int count;
    private int completed;
    public ScopedTaskProgress(TaskbarItemInfo bar, int count)
    {
        this.bar = bar;
        this.count = Math.Max(count, 1);
        GUIUtilities.OnUi(() => { bar.ProgressValue = 0; bar.ProgressState = TaskbarItemProgressState.Normal; });
    }
    public void TaskCompleted() { completed++; SetTaskProgress(0); }
    public void SetTaskProgress(double percent) =>
        GUIUtilities.OnUi(() => bar.ProgressValue = Math.Clamp((completed + Math.Clamp(percent, 0, 100) / 100) / count, 0, 1));
    public void Dispose() => GUIUtilities.OnUi(() => bar.ProgressState = TaskbarItemProgressState.None);
}

internal sealed class ScopedElementBold : IDisposable
{
    private readonly List<(Control Control, FontWeight Weight)> elements = [];
    public ScopedElementBold(List<Control> controls, Task? until)
    {
        GUIUtilities.OnUi(() => { foreach (var c in controls) { elements.Add((c, c.FontWeight)); c.FontWeight = FontWeights.SemiBold; } });
        if (until is not null) _ = until.ContinueWith(_ => Dispose(), TaskScheduler.Default);
    }
    public void Dispose() => GUIUtilities.OnUi(() => { foreach (var (control, weight) in elements) control.FontWeight = weight; });
}

internal sealed class ScopedElementVisibility : IDisposable
{
    private readonly List<(UIElement Element, Visibility Visibility)> elements = [];
    public ScopedElementVisibility(List<UIElement> controls, Task? until)
    {
        GUIUtilities.OnUi(() => { foreach (var c in controls) { elements.Add((c, c.Visibility)); c.Visibility = Visibility.Visible; } });
        if (until is not null) _ = until.ContinueWith(_ => Dispose(), TaskScheduler.Default);
    }
    public void Dispose() => GUIUtilities.OnUi(() => { foreach (var (element, visibility) in elements) element.Visibility = visibility; });
}
