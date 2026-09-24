using System.Runtime.InteropServices;

namespace MiddleClickToClose;

/// <summary>
/// WH_MOUSE_LL グローバルフックでミドルクリックを監視する。
/// タスクバー上でのミドルクリックを検出してイベントを発火し、
/// デフォルト動作（新規ウィンドウを開く等）を抑制する。
/// </summary>
internal sealed class LowLevelMouseHook : IDisposable
{
    // GC に回収されないようフィールドに保持
    private readonly NativeMethods.LowLevelMouseProc _hookProc;
    private IntPtr _hookHandle;
    private bool _suppressNextMiddleUp;
    private bool _disposed;

    internal event EventHandler<NativeMethods.POINT>? MiddleClickOnTaskbar;

    internal LowLevelMouseHook()
    {
        _hookProc = HookCallback;
    }

    internal void Install()
    {
        // .NET Core/5+ では GetModuleHandle("user32") が確実
        var hMod = NativeMethods.GetModuleHandle("user32");
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            hMod,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"[MiddleClickToClose] SetWindowsHookEx failed: error={error}");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;

                if (msg == NativeMethods.WM_MBUTTONDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    if (TaskbarHelper.IsPointOnTaskbar(hookStruct.pt))
                    {
                        _suppressNextMiddleUp = true;
                        // フックコールバックをブロックしないよう Task.Run で非同期発火
                        var pt = hookStruct.pt;
                        Task.Run(() => MiddleClickOnTaskbar?.Invoke(this, pt));
                        // デフォルト動作（新規ウィンドウを開く等）を抑制
                        return (IntPtr)1;
                    }
                }
                else if (msg == NativeMethods.WM_MBUTTONUP && _suppressNextMiddleUp)
                {
                    // 対応する MBUTTONUP も抑制
                    _suppressNextMiddleUp = false;
                    return (IntPtr)1;
                }
            }
        }
        catch
        {
            // フックコールバック内で例外を投げるとフックが外れるため握りつぶす
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}
