Option Strict On
Option Explicit On

Imports System.Runtime.InteropServices

' P/Invoke used to keep the Aqua-themed scrollbar overlay in TextBox.vb synced with the real
' Win32 multiline Edit control that WinForms' own TextBox wraps -- the same technique the VB6
' MultiLineTextBox.ctl used (EM_LINESCROLL / GetScrollInfo on its own Text1.hwnd) still applies
' 1:1 in .NET because System.Windows.Forms.TextBox is a thin wrapper over the same native control.
Namespace Global.Aqua

    Friend Module NativeEdit

        Public Const EM_LINESCROLL As Integer = &HB6
        Public Const SB_HORZ As Integer = 0
        Public Const SB_VERT As Integer = 1
        Private Const SIF_RANGE As Integer = &H1
        Private Const SIF_PAGE As Integer = &H2
        Private Const SIF_POS As Integer = &H4
        Private Const SIF_ALL As Integer = SIF_RANGE Or SIF_PAGE Or SIF_POS

        <StructLayout(LayoutKind.Sequential)>
        Public Structure SCROLLINFO
            Public cbSize As Integer
            Public fMask As Integer
            Public nMin As Integer
            Public nMax As Integer
            Public nPage As Integer
            Public nPos As Integer
            Public nTrackPos As Integer
        End Structure

        <DllImport("user32.dll", CharSet:=CharSet.Auto)>
        Private Function SendMessage(ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Function GetScrollInfo(ByVal hWnd As IntPtr, ByVal nBar As Integer, ByRef lpsi As SCROLLINFO) As Boolean
        End Function

        ''' <summary>Scrolls the native multiline edit by (dx columns, dy lines) -- port of the VB6 EM_LINESCROLL calls.</summary>
        Public Sub LineScroll(ByVal handle As IntPtr, ByVal dx As Integer, ByVal dy As Integer)
            If handle = IntPtr.Zero Then Return
            SendMessage(handle, EM_LINESCROLL, CType(dx, IntPtr), CType(dy, IntPtr))
        End Sub

        ''' <summary>Reads the native scrollbar range/page/pos for one axis (SB_VERT/SB_HORZ) -- port of GetScrollInfo use in SetScrollBarRange.</summary>
        Public Function GetScrollState(ByVal handle As IntPtr, ByVal bar As Integer) As SCROLLINFO
            Dim si As New SCROLLINFO()
            If handle = IntPtr.Zero Then Return si
            si.cbSize = Marshal.SizeOf(GetType(SCROLLINFO))   ' non-generic overload: the generic Of T one needs .NET 4.5.1+, this project also targets net35
            si.fMask = SIF_ALL
            GetScrollInfo(handle, bar, si)
            Return si
        End Function

    End Module

End Namespace
