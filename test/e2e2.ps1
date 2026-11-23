# Coleso test 2: RMB wheel switch, MMB close, clipboard restore, real notepad paste
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
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, IntPtr win);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, IntPtr win);
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

Get-Process Coleso -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

Start-Process $me
Start-Sleep -Seconds 2

# --- launch real notepad via explicit path ---
Start-Process "$env:SystemRoot\System32\notepad.exe"
Start-Sleep -Milliseconds 1800
$np = [U]::FindWindow("Notepad", [IntPtr]::Zero)
Write-Output "notepad hwnd: $np"
if ($np -eq [IntPtr]::Zero) { Write-Output "NOTEPAD NOT FOUND, ABORT"; exit 1 }
[U]::Focus($np) | Out-Null
Start-Sleep -Milliseconds 300
Write-Output "fg==np: $([U]::GetForegroundWindow() -eq $np)"

$r = New-Object "U+RECT"
[U]::GetWindowRect($np, [ref]$r) | Out-Null
[U]::SetCursorPos($r.L + 320, $r.T + 320) | Out-Null
Start-Sleep -Milliseconds 300

Set-Clipboard -Value "MARKER_ABC"
Start-Sleep -Milliseconds 300

# --- open wheel (double MMB), switch wheel by RMB ---
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)   # middle down #1 (passes through)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)   # middle up #1
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)   # middle down #2 -> open (eaten)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)   # middle up #2 (eaten)
Start-Sleep -Milliseconds 500
[U]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)   # RMB down -> next wheel
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)   # RMB up (eaten)
Start-Sleep -Milliseconds 500
Shot "C:\ai test\coleso\test\5_wheel2.png"

# --- close by MMB ---
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 500
Shot "C:\ai test\coleso\test\6_wheel_closed.png"

# --- reopen (double MMB), scroll x1, paste via LMB ---
Start-Sleep -Milliseconds 300
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)   # middle down #1 (passes through)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)   # middle up #1
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0020, 0, 0, 0, [UIntPtr]::Zero)   # middle down #2 -> open (eaten)
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0040, 0, 0, 0, [UIntPtr]::Zero)   # middle up #2 (eaten)
Start-Sleep -Milliseconds 500
[U]::mouse_event(0x0800, 0, 0, -120, [UIntPtr]::Zero)  # scroll down -> sector 1
Start-Sleep -Milliseconds 300
[U]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)     # LMB down -> paste
Start-Sleep -Milliseconds 60
[U]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)

Start-Sleep -Milliseconds 2500
$edit = [U]::FindWindowEx($np, [IntPtr]::Zero, "Edit", [IntPtr]::Zero)
$txt = [U]::Text($edit)
[IO.File]::WriteAllText("C:\ai test\coleso\test\notepad_text.txt", $txt, [Text.Encoding]::UTF8)
Write-Output "notepad text length: $($txt.Length)"

Start-Sleep -Milliseconds 1500
Write-Output "clipboard: $(Get-Clipboard)"
Shot "C:\ai test\coleso\test\7_pasted.png"
Write-Output "DONE"
