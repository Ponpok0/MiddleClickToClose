using System.Drawing;
using System.Windows.Forms;

namespace MiddleClickToClose;

/// <summary>
/// システムトレイに常駐するアプリケーションコンテキスト。
/// ウィンドウは表示せず、NotifyIcon のコンテキストメニューから終了する。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly LowLevelMouseHook _mouseHook;
    private bool _disposed;

    internal TrayApplicationContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MiddleClickToClose - タスクバーのミドルクリックでウィンドウを閉じる",
            Visible = true,
            ContextMenuStrip = CreateContextMenu(),
        };

        _mouseHook = new LowLevelMouseHook();
        _mouseHook.MiddleClickOnTaskbar += OnMiddleClickOnTaskbar;
        _mouseHook.Install();
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("終了(&X)", null, (_, _) => ExitThread());
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
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
