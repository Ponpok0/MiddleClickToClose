using System.Runtime.InteropServices;
using System.Text;

namespace MiddleClickToClose;

/// <summary>
/// タスクバー上のクリック判定と、タスクバーボタンに対応するウィンドウの特定・クローズを行う。
/// UI Automation COM API を使ってタスクバーボタンの情報を取得する。
/// </summary>
internal static class TaskbarHelper
{
    // ==============================================================================
    // UI Automation 定数
    // ==============================================================================

    private const int UIA_NamePropertyId = 30005;
    private const int UIA_ProcessIdPropertyId = 30002;
    private const int UIA_ControlTypePropertyId = 30003;

    // ControlType ID
    private const int UIA_ButtonControlTypeId = 50000;
    private const int UIA_ListItemControlTypeId = 50007;

    // 自プロセスID（キャッシュ）
    private static readonly int s_currentPid = Environment.ProcessId;

    // CUIAutomation はアパートメント中立なので static で再利用する
    private static readonly Lazy<IUIAutomation> s_automation =
        new(() => (IUIAutomation)new CUIAutomation());

    // 閉じてはいけないウィンドウのクラス名
    private static readonly HashSet<string> s_systemWindowClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
    };

    // ==============================================================================
    // COM インターフェース定義（UI Automation の最小限サブセット）
    // ==============================================================================

    // CUIAutomation クラス
    [ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
    private class CUIAutomation { }

    // IUIAutomation インターフェース（vtable 順に定義）
    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        [PreserveSig] int CompareElements(IntPtr el1, IntPtr el2, out int areSame);
        [PreserveSig] int CompareRuntimeIds(IntPtr rt1, IntPtr rt2, out int areSame);
        [PreserveSig] int GetRootElement(out IntPtr root);
        [PreserveSig] int ElementFromHandle(IntPtr hwnd, out IntPtr element);
        [PreserveSig] int ElementFromPoint(NativeMethods.POINT pt, out IUIAutomationElement? element);
    }

    // IUIAutomationElement インターフェース（vtable 順に定義）
    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        [PreserveSig] int SetFocus();
        [PreserveSig] int GetRuntimeId(out IntPtr runtimeId);
        [PreserveSig] int FindFirst(int scope, IntPtr condition, out IntPtr found);
        [PreserveSig] int FindAll(int scope, IntPtr condition, out IntPtr found);
        [PreserveSig] int FindFirstBuildCache(int scope, IntPtr condition, IntPtr request, out IntPtr found);
        [PreserveSig] int FindAllBuildCache(int scope, IntPtr condition, IntPtr request, out IntPtr found);
        [PreserveSig] int BuildUpdatedCache(IntPtr request, out IntPtr updated);
        [PreserveSig] int GetCurrentPropertyValue(int propertyId, out object? retVal);
    }

    // ==============================================================================
    // タスクバー判定（フックコールバックから呼ばれるため高速であること）
    // ==============================================================================

    /// <summary>
    /// 指定座標がタスクバー上かどうかを判定する。
    /// メインタスクバーとセカンダリタスクバー（マルチモニター）の両方をチェックする。
    /// </summary>
    internal static bool IsPointOnTaskbar(NativeMethods.POINT pt)
    {
        // メインタスクバー
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero && IsPointInWindow(taskbar, pt))
            return true;

        // セカンダリタスクバー（マルチモニター環境）
        var secondary = IntPtr.Zero;
        while (true)
        {
            secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null);
            if (secondary == IntPtr.Zero) break;
            if (IsPointInWindow(secondary, pt)) return true;
        }

        return false;
    }

    private static bool IsPointInWindow(IntPtr hwnd, NativeMethods.POINT pt)
    {
        return NativeMethods.GetWindowRect(hwnd, out var rect) && rect.Contains(pt);
    }

    // ==============================================================================
    // ウィンドウ特定・クローズ（Task.Run で非同期実行される）
    // ==============================================================================

    /// <summary>
    /// 指定座標のタスクバーボタンに対応するウィンドウを特定し、WM_CLOSE で閉じる。
    /// UI Automation を使ってボタンの名前（＝ウィンドウタイトル）とプロセスIDを取得する。
    /// </summary>
    internal static void TryCloseWindowAtPoint(NativeMethods.POINT pt)
    {
        IUIAutomationElement? element = null;
        try
        {
            var hr = s_automation.Value.ElementFromPoint(pt, out element);
            if (hr != 0 || element == null) return;

            // ControlType を確認（Button または ListItem のみ対象）
            hr = element.GetCurrentPropertyValue(UIA_ControlTypePropertyId, out var ctrlTypeObj);
            if (hr != 0) return;
            if (ctrlTypeObj is int ctrlType
                && ctrlType != UIA_ButtonControlTypeId
                && ctrlType != UIA_ListItemControlTypeId)
            {
                return;
            }

            // ウィンドウタイトル（タスクバーボタンの Name プロパティ）
            hr = element.GetCurrentPropertyValue(UIA_NamePropertyId, out var nameObj);
            if (hr != 0) return;
            var name = nameObj as string;
            if (string.IsNullOrEmpty(name)) return;

            CloseMatchingWindow(name);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MiddleClickToClose] TryCloseWindowAtPoint error: {ex.Message}");
        }
        finally
        {
            if (element != null)
                Marshal.ReleaseComObject(element);
        }
    }

    /// <summary>
    /// タスクバーボタン名に対応するトップレベルウィンドウを探して WM_CLOSE を送る。
    /// タスクバーボタンの Name は「ウィンドウタイトル - アプリ名」や
    /// 「ウィンドウタイトル - アプリ名 - N の実行中ウィンドウ」の形式になるため、
    /// ウィンドウタイトルがボタン名の先頭に含まれるかで判定する。
    /// </summary>
    private static void CloseMatchingWindow(string buttonName)
    {
        IntPtr bestMatch = IntPtr.Zero;
        int bestTitleLen = 0;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;

            var titleLen = NativeMethods.GetWindowTextLength(hwnd);
            if (titleLen == 0) return true;

            var sb = new StringBuilder(titleLen + 1);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();

            // タスクバーボタン名は「<ウィンドウタイトル> - <アプリ名> [- N の実行中ウィンドウ]」
            // ウィンドウタイトルがボタン名の先頭に含まれるか、完全一致かで判定
            if (!buttonName.StartsWith(title, StringComparison.Ordinal)) return true;

            // "タイトル" の後ろが " - " であることを確認（部分一致の誤爆防止）
            // 完全一致の場合はそのまま許可
            if (title.Length < buttonName.Length
                && !buttonName.AsSpan(title.Length).StartsWith(" - "))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out var windowPid);

            // 自プロセスは閉じない
            if (windowPid == (uint)s_currentPid) return true;

            // システムウィンドウは閉じない
            if (IsSystemWindow(hwnd)) return true;

            // 最も長いタイトル一致を優先（最も具体的なマッチ）
            if (title.Length > bestTitleLen)
            {
                bestMatch = hwnd;
                bestTitleLen = title.Length;
            }

            return true; // 全ウィンドウを走査して最良マッチを探す
        }, IntPtr.Zero);

        if (bestMatch != IntPtr.Zero)
        {
            NativeMethods.PostMessage(bestMatch, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static bool IsSystemWindow(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return s_systemWindowClasses.Contains(sb.ToString());
    }
}
