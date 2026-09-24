using System.Drawing;
using System.Windows.Forms;

namespace MiddleClickToClose;

/// <summary>
/// システムトレイに常駐するアプリケーションコンテキスト。
/// ウィンドウは表示せず、NotifyIcon のコンテキストメニューから終了する。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string TrayIconResourceName = "MiddleClickToClose.app.ico";

    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly LowLevelMouseHook _mouseHook;
    private bool _disposed;

    internal TrayApplicationContext()
    {
        _trayIcon = LoadTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = Strings.TrayTooltip,
            Visible = true,
            ContextMenuStrip = CreateContextMenu(),
        };

        _mouseHook = new LowLevelMouseHook();
        _mouseHook.MiddleClickOnTaskbar += OnMiddleClickOnTaskbar;
        _mouseHook.Install();
    }

    /// <summary>
    /// 埋め込みリソースから、トレイの表示サイズに合う画像を選んでアイコンを読み込む。
    /// </summary>
    /// <remarks>
    /// Icon.ExtractAssociatedIcon は 32px の画像しか返さず、トレイでは縮小されてぼやけるため使わない。
    /// </remarks>
    private static Icon LoadTrayIcon()
    {
        using var stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream(TrayIconResourceName)
            ?? throw new InvalidOperationException(Strings.EmbeddedResourceNotFound(TrayIconResourceName));
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.ExitMenu, null, (_, _) => ExitThread());
        return menu;
    }

    private void OnMiddleClickOnTaskbar(object? sender, NativeMethods.POINT pt)
    {
        TaskbarHelper.TryCloseWindowAtPoint(pt);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _mouseHook.MiddleClickOnTaskbar -= OnMiddleClickOnTaskbar;
                _mouseHook.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _trayIcon.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
