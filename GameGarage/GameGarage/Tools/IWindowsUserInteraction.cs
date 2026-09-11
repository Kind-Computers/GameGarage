using GameGarage.Core;

namespace GameGarage.Tools;

/// <summary>Windows-specific UI requests remain outside diagnostic implementations.</summary>
public interface IWindowsUserInteraction : IUserInteraction
{
    (string Filename, int Index)? SelectWindowsImage();
    void ShowDrivers(IReadOnlyList<DeviceDriverInfo> drivers);
    void RequestExit();
}

internal sealed class NonInteractiveUserInteraction : IWindowsUserInteraction
{
    public bool Confirm(string title, string message) => false;
    public void Notify(string title, string message, DiagnosticStatus status) { }
    public (string Filename, int Index)? SelectWindowsImage() => null;
    public void ShowDrivers(IReadOnlyList<DeviceDriverInfo> drivers) { }
    public void RequestExit() { }
}
