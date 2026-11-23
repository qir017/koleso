# Coleso E2E test: simulates real mouse input and verifies paste into Notepad.
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class U {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int data, UIntPtr extra);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string win);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string win);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, StringBuilder sb);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  public static uint ThreadOf(IntPtr h){ uint p; GetWindowThreadProcessId(h, out p); return p; }
  public static bool Focus(IntPtr h){
    IntPtr fg = GetForegroundWindow();
    uint ft = ThreadOf(fg), mt = GetCurrentThreadId();
    if (ft == 0) return SetForegroundWindow(h);
    AttachThreadInput(mt, ft, true);
    bool r = SetForegroundWindow(h);
    AttachThreadInput(mt, ft, false);
    return r;
  }
  public static string Text(IntPtr edit){
    int len = (int)SendMessage(edit, 0x000E, IntPtr.Zero, (StringBuilder)null);
    if (len <= 0) return "";
    var sb = new StringBuilder(len + 2);
    SendMessage(edit, 0x000D, (IntPtr)(len + 1), sb);
    return sb.ToString();
  }
}
"@

function Shot([string]$path) {
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output "shot: $path"
}

$me = "C:\ai test\coleso\publish\Coleso.exe"
New-Item -ItemType Directory -Force -Path "C:\ai test\coleso\test" | Out-Null

# ---------- PART A: demo overlay visual check ----------
Get-Process Coleso -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

[U]::SetCursorPos(900, 500) | Out-Null
Start-Process $me -ArgumentList "--demo"
Start-Sleep -Seconds 2
Shot "C:\ai test\coleso\test\1_wheel_demo.png"

# switch wheel via RMB (also captured by hook)
[U]::mouse_event(0x0004 + 0x0008, 0, 0, 0, [UIntPtr]::Zero) | Out-Null # placeholder no-op
Start-Sleep -Milliseconds 300

# ---------- PART B: notepad paste E2E ----------
Get-Process Coleso -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

Start-Process $me
Start-Sleep -Seconds 2

Start-Process notepad
Start-Sleep -Milliseconds 1500
$np = [U]::FindWindow("Notepad", $null)
Write-Output "notepad hwnd: $np"
$ok = [U]::Focus($np)
Start-Sleep -Milliseconds 300
Write-Output "focused: $ok / fg==np: $([U]::GetForegroundWindow() -eq $np)"

$r = New-Object "U+RECT"
[U]::GetWindowRect($np, [ref]$r) | Out-Null
[U]::SetCursorPos($r.L + 320, $r.T + 320) | Out-Null
Start-Sleep -Milliseconds 300

# marker in clipboard, should be restored after paste
Set-Clipboard -Value "MARKER_ABC"
Start-Sleep -Milliseconds 300

# open wheel: double middle click (down + up, down + up)
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 700
Shot "C:\ai test\coleso\test\2_wheel_open.png"

# scroll down twice -> sector 2 ("Спасибо большое!")
[U]::mouse_event(0x0800, 0, 0, -120, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 250
[U]::mouse_event(0x0800, 0, 0, -120, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
Shot "C:\ai test\coleso\test\3_wheel_selected.png"

# LMB -> paste
[U]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)

Start-Sleep -Milliseconds 2000

$edit = [U]::FindWindowEx($np, [IntPtr]::Zero, "Edit", $null)
Write-Output "edit hwnd: $edit"
$txt = [U]::Text($edit)
[IO.File]::WriteAllText("C:\ai test\coleso\test\notepad_text.txt", $txt, [Text.Encoding]::UTF8)
Write-Output "notepad text length: $($txt.Length)"

Start-Sleep -Milliseconds 1500
$cl = Get-Clipboard
Write-Output "clipboard after paste: $cl"

Shot "C:\ai test\coleso\test\4_after_paste.png"

# close wheel leftovers: press middle again if still open (demo timer may have closed it)
Write-Output "DONE"
