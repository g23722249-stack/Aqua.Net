Option Strict On
Option Explicit On

Imports System.Drawing

' Shared themed 3-line border + focus "Parhelia" glow, ported from the VB6 DrawControlBorder /
' DrawControlParhelia pair that was duplicated verbatim across EditBox.ctl, TextBox.ctl and
' MultiLineTextBox.ctl. Grid.vb already carries its own inline copy of the same algorithm
' (see its OnPaint); this module lets TextBox.vb reuse it without re-deriving the constants.
Namespace Global.Aqua

    Friend Module BorderPainter

        ''' <summary>Draws the 3px themed border, its lighter outer edge/top highlight, and (when focused) the Parhelia glow.</summary>
        Public Sub DrawThemedBorder(ByVal g As Graphics, ByVal width As Integer, ByVal height As Integer,
                                    ByVal borderColor As Color, ByVal focusColor As Color,
                                    ByVal focused As Boolean, ByVal parhelia As Boolean)
            If width <= 0 OrElse height <= 0 Then Return
            Dim lineColor As Color = If(focused, focusColor, borderColor)

            Using p As New Pen(lineColor, 3)
                g.DrawRectangle(p, 1, 1, width - 3, height - 3)
            End Using

            Dim outer As Color = If(focused, ColorUtil.ShiftChannels(lineColor, 10),
                                             ColorUtil.ShiftChannels(lineColor, 123, 122, 123))
            Using p As New Pen(outer, 1)
                g.DrawRectangle(p, 0, 0, width - 1, height - 1)
            End Using

            Dim topHi As Color = If(focused, ColorUtil.ShiftChannels(lineColor, -14),
                                             ColorUtil.ShiftChannels(lineColor, 46, 44, 46))
            Using p As New Pen(topHi, 1)
                g.DrawLine(p, 0, 0, width - 1, 0)
            End Using

            If parhelia AndAlso focused Then DrawParheliaGlow(g, width, height, focusColor)
        End Sub

        ''' <summary>
        ''' Just the focus glow (port of DrawControlParhelia's ShowParhelia ring), for controls like
        ''' iTextBox that paint their own themed surface picture instead of drawing border lines.
        ''' </summary>
        Public Sub DrawParheliaGlow(ByVal g As Graphics, ByVal width As Integer, ByVal height As Integer, ByVal glowColor As Color)
            For k As Integer = 0 To 3
                Dim alpha As Integer = Math.Max(0, 90 - k * 22)
                Using p As New Pen(Color.FromArgb(alpha, glowColor), 1)
                    Dim inset As Integer = 3 + k
                    Dim rw As Integer = width - 1 - inset * 2
                    Dim rh As Integer = height - 1 - inset * 2
                    If rw > 0 AndAlso rh > 0 Then g.DrawRectangle(p, inset, inset, rw, rh)
                End Using
            Next
        End Sub

    End Module

End Namespace
