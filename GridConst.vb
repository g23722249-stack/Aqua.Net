Option Strict On
Option Explicit On

Imports System.Drawing

' Constants ported verbatim from the VB6 Module\LibConst.bas (grid-related subset).
' The original values are OLE colours stored as &H00BBGGRR Long; ToColor() converts
' them to GDI+ Color so call sites read naturally.
Namespace Global.Aqua

    Friend Module GridConst

        ' --- raw OLE colour Longs, exactly as declared in LibConst.bas ---
        Public Const gc_lngDisableForeColor As Integer = 12435133      ' RGB(189, 190, 189)
        Public Const gc_lngBorderFocusColor As Integer = 16037535      ' RGB(159, 182, 244)
        Public Const gc_lngGridLintColor As Integer = &HC0C0C0         ' RGB(192, 192, 192)
        Public Const gc_lngGridBorderLineColor As Integer = 6514019    ' RGB(99, 101, 99)
        Public Const gc_lngGridOddBackColor As Integer = &HFFF3EF      ' RGB(239, 243, 255)
        Public Const gc_lngGridSelColor As Integer = 14647357          ' RGB(61, 128, 223)
        Public Const gc_lngGridSelLostFocusColor As Integer = 13816018 ' RGB(210, 208, 210)

        ' --- pixel metrics ---
        Public Const gc_intKeepBorderSize As Integer = 4
        Public Const gc_intGridSplitWidth As Integer = 1

        ' --- convenience GDI+ colours (derived, not new values) ---
        Public ReadOnly Property DisableForeColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngDisableForeColor)
            End Get
        End Property

        Public ReadOnly Property BorderFocusColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngBorderFocusColor)
            End Get
        End Property

        Public ReadOnly Property GridLineColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngGridLintColor)
            End Get
        End Property

        Public ReadOnly Property GridBorderLineColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngGridBorderLineColor)
            End Get
        End Property

        Public ReadOnly Property GridOddBackColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngGridOddBackColor)
            End Get
        End Property

        Public ReadOnly Property GridSelColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngGridSelColor)
            End Get
        End Property

        Public ReadOnly Property GridSelLostFocusColor As Color
            Get
                Return ColorUtil.OleToColor(gc_lngGridSelLostFocusColor)
            End Get
        End Property

    End Module

End Namespace
