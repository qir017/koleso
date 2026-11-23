using System.Runtime.InteropServices;

namespace Coleso;

/// <summary>
/// Глобальный низкоуровневый хук мыши. Обработчик вызывается на потоке UI;
/// возврат true «съедает» событие — оно не доходит до приложений.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private IntPtr _hook = IntPtr.Zero;
    private readonly Native.LowLevelMouseProc _proc;

    /// <summary>(сообщение, данные) → true = перехватить.</summary>
    public Func<int, Native.MSLLHOOKSTRUCT, bool>? Handler { get; set; }

    public MouseHook() => _proc = HookProc;

    public bool Install()
    {
        if (_hook != IntPtr.Zero) return true;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Handler != null)
        {
            try
            {
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if (Handler((int)wParam, info))
                    return 1;
            }
            catch { /* не роняем хук */ }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
