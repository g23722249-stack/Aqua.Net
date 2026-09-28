Option Strict On
Option Explicit On

Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' Shared by AquaForm/iForm: the classic "ReleaseCapture + WM_SYSCOMMAND(SC_MOVE/SC_SIZE + hit-test
' code)" trick for dragging/resizing a borderless window, handed off to the OS's own move/size
' loop. This replaces VB6's SendMessage(..., WM_NCLBUTTONDOWN, HTCAPTION, ...) drag and its manual
' pixel-delta resize loop with the native equivalent -- simpler and gets real OS behaviour (edge
' snapping, etc.) for free.
Namespace Global.Aqua

    Friend Module NativeWindowDrag

        Private Const WM_SYSCOMMAND As Integer = &H112
        Private Const SC_MOVE As Integer = &HF010
        Private Const SC_SIZE As Integer = &HF000

        Public Const HTLEFT As Integer = 10
        Public Const HTRIGHT As Integer = 11
        Public Const HTTOP As Integer = 12
        Public Const HTTOPLEFT As Integer = 13
        Public Const HTTOPRIGHT As Integer = 14
        Public Const HTBOTTOM As Integer = 15
        Public Const HTBOTTOMLEFT As Integer = 16
        Public Const HTBOTTOMRIGHT As Integer = 17

        <DllImport("user32.dll")>
        Private Function ReleaseCapture() As Boolean
        End Function

        <DllImport("user32.dll", CharSet:=CharSet.Auto)>
        Private Function SendMessage(ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
        End Function

        Private Const WM_SETREDRAW As Integer = &HB
        Private Const RDW_INVALIDATE As UInteger = &H1
        Private Const RDW_ERASE As UInteger = &H4
        Private Const RDW_ALLCHILDREN As UInteger = &H80
        Private Const RDW_UPDATENOW As UInteger = &H100

        <DllImport("user32.dll")>
        Private Function RedrawWindow(ByVal hWnd As IntPtr, ByVal lprcUpdate As IntPtr, ByVal hrgnUpdate As IntPtr, ByVal flags As UInteger) As Boolean
        End Function

        ''' <summary>Throttled manual resize (see AquaForm/iForm OnResizeThrottleTick) still leaves a
        ''' visible flicker even with our own chrome back-buffered and child controls' own
        ''' DoubleBuffered flipped on: each Size assignment triggers several separate, individually
        ''' visible repaint steps (Region rebuild, our own chrome, then every child HWND laid out at
        ''' its new position) that Windows is happy to show one at a time as they happen. WM_SETREDRAW
        ''' is the standard fix -- it tells the window (and, via RDW_ALLCHILDREN below, its child
        ''' HWNDs like Button/Label) to stop presenting any painting at all while suspended, so every
        ''' step of the resize happens invisibly; ResumeDrawing then forces exactly one atomic
        ''' repaint of the whole subtree via RedrawWindow instead of letting the intermediate steps
        ''' leak onto the screen individually.</summary>
        Public Sub SuspendDrawing(ByVal handle As IntPtr)
            SendMessage(handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero)
        End Sub

        Public Sub ResumeDrawing(ByVal handle As IntPtr)
            SendMessage(handle, WM_SETREDRAW, New IntPtr(1), IntPtr.Zero)
            RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE Or RDW_ERASE Or RDW_ALLCHILDREN Or RDW_UPDATENOW)
        End Sub

        Public Sub BeginDragMove(ByVal handle As IntPtr)
            ReleaseCapture()
            SendMessage(handle, WM_SYSCOMMAND, CType(SC_MOVE + HTCAPTIONFake, IntPtr), IntPtr.Zero)
        End Sub

        ''' <summary>HTCAPTION (2) is the hit-test code the OS move-loop expects appended to SC_MOVE;
        ''' named distinctly here since it's only ever used in this one combination.</summary>
        Private Const HTCAPTIONFake As Integer = 2

        Public Sub BeginDragResize(ByVal handle As IntPtr, ByVal hitTest As Integer)
            ReleaseCapture()
            SendMessage(handle, WM_SYSCOMMAND, CType(SC_SIZE + hitTest, IntPtr), IntPtr.Zero)
        End Sub

        Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
        Private Const DWMWCP_DONOTROUND As Integer = 1

        <DllImport("dwmapi.dll", PreserveSig:=True)>
        Private Function DwmSetWindowAttribute(ByVal hwnd As IntPtr, ByVal dwAttribute As Integer, ByRef pvAttribute As Integer, ByVal cbAttribute As Integer) As Integer
        End Function

        ''' <summary>Windows 11's DWM auto-rounds every top-level window's corners and draws its own
        ''' edge/shadow to match -- on top of, and not aware of, our own manually-clipped Region.
        ''' Where DWM's rounding radius differs from ours (or the Region cuts a corner away
        ''' entirely, e.g. AquaForm's square bottom corners), its shadow/edge treatment still
        ''' follows the window's real rectangular frame, leaving a faint ghost patch right at the
        ''' corner -- invisible to DrawToBitmap (that only captures this window's own painted
        ''' content, not compositor-level DWM effects), which is why this never showed up in
        ''' automated screenshots but did in a real running window. Opting the window out of DWM's
        ''' own rounding leaves our Region as the only thing shaping the corners.
        ''' Silently no-ops pre-Windows 11 (the attribute is simply unrecognised there).</summary>
        Public Sub DisableDwmCornerRounding(ByVal handle As IntPtr)
            Try
                Dim pref As Integer = DWMWCP_DONOTROUND
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, pref, 4)
            Catch
                ' dwmapi.dll missing/blocked, or the OS predates this attribute -- harmless to skip.
            End Try
        End Sub

    End Module

End Namespace
