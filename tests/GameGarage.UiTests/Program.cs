using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameGarage.Interface;
using GameGarage.Resources;
using GameGarage.Tools;
using GameGarage.Utilities;

internal static class Program
{
    private static int checks;
    [STAThread]
    public static int Main(string[] args)
    {

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            var originalCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(UiText.Get("AppTitle") == "Game Garage", "English fallback when translation absent");
            CultureInfo.CurrentUICulture = originalCulture;
            Check(!ProductInfo.Version.Contains("2099"), "version is not an expiration date");
            Check(ProductInfo.License == "MIT License", "MIT metadata");
            Check(TextUtilities.ComputeLevenshteinDistance("Windows 11 Pro", "Windows 11 Pro") == 0, "image name exact match");
            Check(TextUtilities.ComputeLevenshteinDistance("kitten", "sitting") == 3, "image name comparison");

            var parsed = WindowsImageUtilities.ParseWindowsImages([
                "Index : 3", "Name : Windows 11 Pro", "Description :", "Size : 12,345 bytes",
                "Index : 7", "Name : Windows 11 Home", "Description : Home"
            ]);
            Check(parsed.Count == 2 && parsed[0].Index == 3 && parsed[1].Index == 7, "noncontiguous WIM indices preserved");
            Check(parsed[0].IsValid(), "empty optional WIM description allowed");
            Check(WindowsImageUtilities.ParseWindowsImages(["Index : nonsense", "Name : Invalid"]).Count == 0, "malformed WIM index ignored");
            Check(WindowsImageUtilities.GetClosestMatchIndex("Windows 11 Home", parsed) == 1, "matching image suggested");

            var main = new MainWindow();
            app.MainWindow = main;
            Check(main.OptimizeDrivesCheckBox.IsChecked == true, "drive optimization selected by default");
            Check(main.VerifyStabilityCheckBox.IsChecked == true, "broad RAM check retained");
            Check(main.StartTuneUpButton.IsEnabled && main.CancelTuneUpButton.Visibility == Visibility.Collapsed, "startup idle and no automatic tools");
            Check(main.WindowStyle != WindowStyle.None && !main.AllowsTransparency, "standard Windows window behavior");
            Check(main.StartTuneUpButton.Content.ToString()!.Replace("_", "") == "Run selected tools", "clear primary action");

            main.VerifyStabilityScanLogListView.Items.Add(new ListViewItem { Content = "fixture output" });
            main.VerifyStabilityLabel.Content = UiText.Get("Passed");
            main.VerifyStabilityProgressBar.Value = 100;
            main.VerifyWindowsFilesLabel.Content = UiText.Get("Inconclusive");
            main.VerifyDrivesLabel.Content = UiText.Get("Ready");
            Render(main, 818, 580, 1.0, args.FirstOrDefault());
            Render(main, 720, 440, 1.5, null);
            Render(main, 720, 440, 2.0, null);

            main.OptimizeDrivesCheckBox.IsChecked = false;
            main.ResetRowsForSession();
            Check(Equals(main.VerifyStabilityLabel.Content, UiText.Get("Queued")) && main.VerifyStabilityScanLogListView.Items.Count == 0, "new session clears stale green results and logs");
            main.FinishQueuedRows(true);
            Check(Equals(main.VerifyStabilityLabel.Content, UiText.Get("Cancelled")), "unstarted selected checks marked cancelled");
            Check(Equals(main.OptimizeDrivesLabel.Content, UiText.Get("NotSelected")), "unselected maintenance is not a previous pass");
            // Simulate an outstanding task, without launching a scan or servicing process.
            var pending = new TaskCompletionSource();
            using var cancellation = new CancellationTokenSource();
            Set(main, "runningTask", pending.Task);
            Set(main, "cancellation", cancellation);
            var closing = new CancelEventArgs();
            typeof(MainWindow).GetMethod("Window_Closing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(main, [main, closing]);
            Check(closing.Cancel && cancellation.IsCancellationRequested, "closing requests cooperative stop and keeps window alive");
            Check(main.SessionStatus.Text.Contains("Cancellation requested"), "pending cancellation visible");
            Set(main, "runningTask", Task.CompletedTask);
            Set(main, "cancellation", null);
            Set(main, "closeWhenFinished", false);

            var about = new AboutWindow();
            Check(about.Version == ProductInfo.Version && about.License == "MIT License", "About window bindings");
            Render(about, 530, 320, 1.0, null);
            var imageDialog = new WindowsImageSelection();
            Check(!imageDialog.OkButton.IsEnabled, "image selection initially disabled");
            Render(imageDialog, 720, 450, 1.0, null);
            var driverDialog = new DeviceDriverWindow([new DeviceDriverInfo { DeviceName = "Fixture device", InfName = "fixture.inf", IsSigned = false }]);
            Check(driverDialog.UnsignedDrivers.Count == 1, "driver dialog binds fixture");
            Render(driverDialog, 720, 420, 1.0, null);

            foreach (var state in Enum.GetValues<VerifierBase.VerifierResult>())
            {
                string key = state switch { VerifierBase.VerifierResult.Scanned => "Passed",
                    VerifierBase.VerifierResult.Repair_Scheduled_On_Reboot => "Scheduled",
                    VerifierBase.VerifierResult.Not_Repaired => "NotRepaired", _ => state.ToString() };
                Check(!UiText.Get(key).StartsWith('['), $"localized status {state}");
            }

            AuditResources();
            driverDialog.Close();
            imageDialog.Close();
            about.Close();
            main.Close();
            Console.WriteLine($"PASS: {checks} UI/resource/image-parser checks; no hardware scan or Windows maintenance invoked.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally { app.Shutdown(); }
    }

    private static void Set(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void Render(Window window, int width, int height, double scale, string? output)
    {
        var visual = (FrameworkElement)window.Content;
        visual.Measure(new Size(width, height));
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(0,0,width,height));
        bitmap.Render(backdrop);
        bitmap.Render(visual);
        Check(bitmap.PixelWidth > 0 && visual.ActualWidth > 0 && visual.ActualWidth <= width, $"offscreen layout {window.GetType().Name} at {scale:P0}");
        if (output is not null)
        {
            string path = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }

    private static void AuditResources()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "GameGarage", "GameGarage", "GameGarage.csproj"))) current = current.Parent;
        if (current is null) throw new InvalidOperationException("Cannot locate source resource fixtures.");
        var appRoot = Path.Combine(current.FullName, "GameGarage", "GameGarage");
        var resources = new ResourceManager("GameGarage.Resources.UiText", typeof(UiText).Assembly);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(appRoot, "Interface"), "*.*", SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".cs") && !path.EndsWith(".xaml")) continue;
            var source = File.ReadAllText(path);
            foreach (Match m in Regex.Matches(source, "loc:Loc ([A-Za-z0-9]+)")) keys.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(source, "UiText\\.(?:Get|Format)\\(\\\"([A-Za-z0-9]+)\\\"")) keys.Add(m.Groups[1].Value);
        }
        foreach (var key in keys) Check(resources.GetString(key, CultureInfo.InvariantCulture) is not null, $"resource exists: {key}");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        checks++;
    }
}
