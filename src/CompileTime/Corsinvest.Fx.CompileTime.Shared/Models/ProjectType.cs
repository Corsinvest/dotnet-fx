namespace Corsinvest.Fx.CompileTime.Models;

/// <summary>
/// Defines the type of project for additional using detection
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Console or library project (default)
    /// </summary>
    Default = 0,

    /// <summary>
    /// ASP.NET Core web application
    /// </summary>
    AspNetCore = 1,

    /// <summary>
    /// Windows Forms application
    /// </summary>
    WinForms = 2,

    /// <summary>
    /// WPF application
    /// </summary>
    WPF = 3,

    /// <summary>
    /// Worker service or hosted service
    /// </summary>
    Worker = 4
}
