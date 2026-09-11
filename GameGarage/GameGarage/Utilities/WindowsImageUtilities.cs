using GameGarage.Resources;
using System.Globalization;
using System.IO;

namespace GameGarage.Utilities;

internal static class WindowsImageUtilities
{
    public static int GetClosestMatchIndex(string name, List<WindowsImageInfo> images) =>
        images.Count == 0 ? -1 : images.Select((image, index) => (index, distance: TextUtilities.ComputeLevenshteinDistance(name, image.Name)))
            .MinBy(item => item.distance).index;

    public static List<WindowsImageInfo> GetWindowsImages(string filename, CancellationToken cancellation = default)
    {
        var wrapper = new ProcessWrapper("dism.exe", true, cancellation);
        var output = wrapper.RunProcessAndCollectOutput(["/English", "/Get-WimInfo", "/WimFile:" + filename], "", out int exitCode);
        cancellation.ThrowIfCancellationRequested();
        if (exitCode != 0) throw new IOException(UiText.Format("ImageExitError", exitCode));
        return ParseWindowsImages(output.Select(item => item.Line));
    }

    internal static List<WindowsImageInfo> ParseWindowsImages(IEnumerable<string> lines)
    {
        List<WindowsImageInfo> images = [];
        WindowsImageInfo? current = null;
        foreach (var line in lines)
        {
            int separator = line.IndexOf(':');
            if (separator < 0) continue;
            var label = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (label.Equals("Index", StringComparison.OrdinalIgnoreCase))
            {
                if (current?.IsValid() == true) images.Add(current);
                current = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index > 0
                    ? new WindowsImageInfo { Index = index } : null;
            }
            else if (current is not null)
            {
                if (label.Equals("Name", StringComparison.OrdinalIgnoreCase)) current.Name = value;
                else if (label.Equals("Description", StringComparison.OrdinalIgnoreCase)) current.Description = value;
                else if (label.Equals("Size", StringComparison.OrdinalIgnoreCase)) current.Size = value;
            }
        }
        if (current?.IsValid() == true) images.Add(current);
        return images;
    }
}
public sealed class WindowsImageInfo
{
    public int Index { get; set; } = -1;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public bool IsValid() => Index > 0 && !string.IsNullOrWhiteSpace(Name);
    public override string ToString() => UiText.Format("ImageInfo", Index, Name, Description, Size, Environment.NewLine);
}
