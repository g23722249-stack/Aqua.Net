Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.Month control: a 12-month picker laid out 2 columns x
' 6 rows, with the same glossy gradient "pill" selection as AquaCalendar. Pure drawing --
' no bitmap resources.
Namespace Global.Aqua

    <DefaultEvent("ValueChanged")>
    Public Class Month
        Inherits Control

        Private ReadOnly _names As String() = {"一 月", "二 月", "三 月", "四 月", "五 月", "六 月",
                                                "七 月", "八 月", "九 月", "十 月", "十一月", "十二月"}
        Private ReadOnly _tips As String() = {"January", "February", "March", "April", "May", "June",
                                              "July", "August", "September", "October", "November", "December"}

        Private _month As Integer = 1                 ' 1..12
        Private _foreColor As Color = Color.Black
        Private _monthBackColor As Color = Color.FromArgb(231, 231, 231)
        Private _selColor As Color = Color.FromArgb(107, 170, 247)
        Private ReadOnly _tip As New ToolTip()
        Private _hoverIndex As Integer = -1
        ' per-month text colour overrides (VB6: picMonth(i).ForeColor via SetMonthColor);
        ' Color.Empty = use ForeColor. Setting ForeColor resets them all, as in VB6.
        Private ReadOnly _monthColors(11) As Color

        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event MonthClick(month As Integer)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = Color.White
            Font = New Font("Microsoft JhengHei", 10.0F)
            Size = New Size(220, 210)
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <DefaultValue(1)>
        Public Property Month As Integer
            Get
                Return _month
            End Get
            Set(value As Integer)
                If value < 1 OrElse value > 12 OrElse _month = value Then Return
                _month = value
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)> Public Property SelColor As Color
            Get
                Return _selColor
            End Get
            Set(v As Color)
                _selColor = v : Invalidate()
            End Set
        End Property
        <Browsable(False)> Public Property MonthBackColor As Color
            Get
                Return _monthBackColor
            End Get
            Set(v As Color)
                _monthBackColor = v : Invalidate()
            End Set
        End Property
        <Browsable(False)> Public Shadows Property ForeColor As Color
            Get
                Return _foreColor
            End Get
            Set(v As Color)
                _foreColor = v
                Array.Clear(_monthColors, 0, _monthColors.Length)
                Invalidate()
            End Set
        End Property

        ''' <summary>Text colour for one month (VB6: SetMonthColor), e.g. to highlight months
        ''' that have data. Kept until changed again or ForeColor is set.</summary>
        Public Sub SetMonthColor(ByVal month As Integer, ByVal color As Color)
            If month < 1 OrElse month > 12 Then Return
            _monthColors(month - 1) = color
            Invalidate()
        End Sub

        '=====================================================================
        ' Geometry: 2 columns x 6 rows (index 0..5 left column, 6..11 right column)
        '=====================================================================
        Private Function CellRect(ByVal index As Integer) As Rectangle
            Dim col As Integer = index \ 6           ' 0 = left, 1 = right
            Dim row As Integer = index Mod 6
            Dim cw As Integer = Width \ 2
            Dim ch As Integer = Height \ 6
            Return New Rectangle(col * cw, row * ch, cw, ch)
        End Function

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(BackColor)

            Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            ' Each month is its own pill, but rounded only on the OUTER end of its column
            ' (left column rounds the left end, right column rounds the right end); the
            ' inner end facing the centre gap stays square — matching the VB6 caps.
            For i = 0 To 11
                Dim cell As Rectangle = Rectangle.Inflate(CellRect(i), -3, -2)
                If cell.Height <= 0 OrElse cell.Width <= 0 Then Continue For
                Dim isLeftCol As Boolean = (i \ 6 = 0)
                Dim roundLeft As Boolean = isLeftCol
                Dim roundRight As Boolean = Not isLeftCol
                If (i + 1) = _month Then
                    Calendar.DrawPill(g, cell, _selColor, roundLeft, roundRight)
                    TextRenderer.DrawText(g, _names(i), New Font(Font, FontStyle.Bold), cell, Color.White, flags)
                Else
                    Using p As GraphicsPath = Calendar.RoundedRectSides(cell, Math.Min(cell.Height, cell.Width) \ 2, roundLeft, roundRight)
                        Using b As New SolidBrush(_monthBackColor)
                            g.FillPath(b, p)
                        End Using
                    End Using
                    Dim fc As Color = If(_monthColors(i).IsEmpty, _foreColor, _monthColors(i))
                    TextRenderer.DrawText(g, _names(i), Font, cell, fc, flags)
                End If
            Next
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            For i = 0 To 11
                If CellRect(i).Contains(e.Location) Then
                    If (i + 1) <> _month Then
                        _month = i + 1
                        Invalidate()
                        RaiseEvent ValueChanged(Me, EventArgs.Empty)
                    End If
                    RaiseEvent MonthClick(i + 1)
                    Exit For
                End If
            Next
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim idx As Integer = -1
            For i = 0 To 11
                If CellRect(i).Contains(e.Location) Then
                    idx = i : Exit For
                End If
            Next
            If idx <> _hoverIndex Then
                _hoverIndex = idx
                _tip.SetToolTip(Me, If(idx >= 0, _tips(idx), ""))
            End If
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _tip.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
