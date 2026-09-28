Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports System.Windows.Forms.Design

' Design-time helper for the in-process WinForms designer (.NET Framework projects): clicking a tab
' head switches the page shown, so every page can be laid out in the designer.
'
' The designer does not hand clicks on to the control's OnMouseDown, not even where GetHitTest says
' the area is "live" (checked on a DesignSurface: GetHitTest True, no MouseDown), so the designer
' switches the page itself when the left button goes down on a tab head, then lets the click go on
' as usual (it still selects the TabControl).
' Visual Studio's out-of-process designer (.NET projects) never loads this class: there the
' TabControl watches the mouse itself (see "Design time" in TabControl.vb).
Namespace Global.Aqua

    Public Class AquaTabControlDesigner
        Inherits ControlDesigner

        Private Const WM_LBUTTONDOWN As Integer = &H201

        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_LBUTTONDOWN Then
                Dim tc As TabControl = TryCast(Control, TabControl)
                If tc IsNot Nothing Then
                    Dim lp As Long = m.LParam.ToInt64()
                    tc.SelectTabAt(New Point(SignedWord(lp), SignedWord(lp >> 16)))   ' client coordinates
                End If
            End If
            MyBase.WndProc(m)
        End Sub

        Private Shared Function SignedWord(ByVal v As Long) As Integer
            Dim w As Integer = CInt(v And &HFFFF)
            Return If(w > &H7FFF, w - &H10000, w)
        End Function

        ' Keeps the header "live" for designers that do forward clicks there (pure predicate: it is
        ' called on every mouse move, so it must not switch anything itself).
        Protected Overrides Function GetHitTest(point As Point) As Boolean
            Dim tc As TabControl = TryCast(Control, TabControl)
            If tc Is Nothing Then Return False
            Return tc.IsOnHeader(tc.PointToClient(point))
        End Function

    End Class

End Namespace
