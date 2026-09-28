Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.Calendar control: a single-month day grid (7x6) with a
' weekday header and a glossy gradient "pill" highlight on the selected day. Pure drawing --
' no bitmap resources (the VB6 control was also fully self-drawn).
Namespace Global.Aqua

    <DefaultEvent("ValueChanged")>
    Public Class Calendar
        Inherits Control

        Private ReadOnly _weekNames As String() = {"日", "一", "二", "三", "四", "五", "六"}

        Private _year As Integer = DateTime.Today.Year
        Private _month As Integer = DateTime.Today.Month
        Private _day As Integer = 0
        Private _weekFontColor As Color = Color.Black
        Private _dayFontColor As Color = Color.Black
        Private _dayBackColor As Color = Color.FromArgb(231, 231, 231)
        Private _selColor As Color = Color.FromArgb(107, 170, 247)

        Private _cells(41) As Integer   ' day number per grid cell (0 = empty)
        ' per-day font colour overrides (VB6: m_lpDays(i).FontColor via SetDayColor); like VB6's
        ' SetDateControlProperty, they are reset whenever the displayed year/month changes.
        Private ReadOnly _dayColors As New Dictionary(Of Integer, Color)()

        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event YearChanged(sender As Object, e As EventArgs)
        Public Event MonthChanged(sender As Object, e As EventArgs)
        Public Event DayChanged(sender As Object, e As EventArgs)
        Public Event DayClick(day As Integer)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = Color.White
            Font = New Font("Times New Roman", 12.0F)
            Size = New Size(320, 220)
            RecalcCells()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <DefaultValue(0)>
        Public Property Day As Integer
            Get
                Return _day
            End Get
            Set(value As Integer)
                Dim d As Integer = value
                If d < 0 Then d = 0
                If d > DaysInMonth() Then d = 0
                If _day = d Then Return
                _day = d
                Invalidate()
                RaiseEvent DayChanged(Me, EventArgs.Empty)
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property Month As Integer
            Get
                Return _month
            End Get
            Set(value As Integer)
                If value < 1 OrElse value > 12 OrElse _month = value Then Return
                ApplyDate(_year, value, _day)
            End Set
        End Property

        Public Property Year As Integer
            Get
                Return _year
            End Get
            Set(value As Integer)
                If value < 1 OrElse value > 9999 OrElse _year = value Then Return
                ApplyDate(value, _month, _day)
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedDate As DateTime?
            Get
                If _day <= 0 Then Return Nothing
                Return New DateTime(_year, _month, _day)
            End Get
        End Property

        <Browsable(False)> Public Property SelColor As Color
            Get
                Return _selColor
            End Get
            Set(v As Color)
                _selColor = v : Invalidate()
            End Set
        End Property
        <Browsable(False)> Public Property DayBackColor As Color
            Get
                Return _dayBackColor
            End Get
            Set(v As Color)
                _dayBackColor = v : Invalidate()
            End Set
        End Property
        <Browsable(False)> Public Property WeekFontColor As Color
            Get
                Return _weekFontColor
            End Get
            Set(v As Color)
                _weekFontColor = v : Invalidate()
            End Set
        End Property
        <Browsable(False)> Public Property DayFontColor As Color
            Get
                Return _dayFontColor
            End Get
            Set(v As Color)
                _dayFontColor = v : Invalidate()
            End Set
        End Property

        ''' <summary>Switch the displayed year/month in one step, keeping the selected day
        ''' (VB6: SetYearMonth). Always raises ValueChanged, like VB6.</summary>
        Public Sub SetYearMonth(ByVal y As Integer, ByVal m As Integer)
            If y < 1 OrElse y > 9999 OrElse m < 1 OrElse m > 12 Then Return
            ApplyDate(y, m, _day, alwaysRaiseValueChanged:=True)
        End Sub

        Public Sub SetYearMonthDay(ByVal y As Integer, ByVal m As Integer, ByVal d As Integer)
            If y < 1 OrElse y > 9999 OrElse m < 1 OrElse m > 12 Then Return
            ApplyDate(y, m, d, alwaysRaiseValueChanged:=True)
        End Sub

        ''' <summary>Font colour for one day of the displayed month (VB6: SetDayColor), e.g. to
        ''' highlight days that have data. Reset when the year/month changes.</summary>
        Public Sub SetDayColor(ByVal day As Integer, ByVal color As Color)
            If day < 1 OrElse day > DaysInMonth() Then Return
            _dayColors(day) = color
            Invalidate()
        End Sub

        ''' <summary>Common tail of Year/Month/SetYearMonth/SetYearMonthDay: raises YearChanged /
        ''' MonthChanged / DayChanged only for the parts that actually changed, then ValueChanged.</summary>
        Private Sub ApplyDate(ByVal y As Integer, ByVal m As Integer, ByVal d As Integer,
                              Optional ByVal alwaysRaiseValueChanged As Boolean = False)
            Dim yChanged As Boolean = (y <> _year)
            Dim mChanged As Boolean = (m <> _month)
            _year = y : _month = m
            If yChanged OrElse mChanged Then _dayColors.Clear()
            RecalcCells()
            If d < 0 OrElse d > DaysInMonth() Then d = 0
            Dim dChanged As Boolean = (d <> _day)
            _day = d
            Invalidate()
            If yChanged Then RaiseEvent YearChanged(Me, EventArgs.Empty)
            If mChanged Then RaiseEvent MonthChanged(Me, EventArgs.Empty)
            If dChanged Then RaiseEvent DayChanged(Me, EventArgs.Empty)
            If alwaysRaiseValueChanged OrElse yChanged OrElse mChanged OrElse dChanged Then
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End If
        End Sub

        '=====================================================================
        ' Model
        '=====================================================================
        Private Function DaysInMonth() As Integer
            Return DateTime.DaysInMonth(_year, _month)
        End Function

        Private Sub RecalcCells()
            Dim firstCol As Integer = CInt(New DateTime(_year, _month, 1).DayOfWeek)   ' 0 = Sunday
            Dim days As Integer = DaysInMonth()
            For i = 0 To 41
                Dim dn As Integer = i - firstCol + 1
                _cells(i) = If(dn >= 1 AndAlso dn <= days, dn, 0)
            Next
        End Sub

        '=====================================================================
        ' Geometry
        '=====================================================================
        Private Function HeaderHeight() As Integer
            Return Font.Height + 4
        End Function

        Private Function CellSize() As Size
            Dim gridTop As Integer = HeaderHeight()
            Dim w As Integer = Width \ 7
            Dim h As Integer = (Height - gridTop) \ 6
            Return New Size(w, h)
        End Function

        Private Function CellRect(ByVal index As Integer) As Rectangle
            Dim cs As Size = CellSize()
            Dim col As Integer = index Mod 7
            Dim row As Integer = index \ 7
            Return New Rectangle(col * cs.Width, HeaderHeight() + row * cs.Height, cs.Width, cs.Height)
        End Function

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(BackColor)

            ' weekday header
            Dim cs As Size = CellSize()
            Dim hFlags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine
            For c = 0 To 6
                Dim hr As New Rectangle(c * cs.Width, 0, cs.Width, HeaderHeight())
                TextRenderer.DrawText(g, _weekNames(c), Font, hr, _weekFontColor, hFlags)
            Next

            ' Each week row is ONE continuous rounded grey bar (rounded ends), matching the
            ' VB6 look (rectangular day cells + elliptic end caps forming a pill-shaped row).
            Dim gridW As Integer = cs.Width * 7
            For r = 0 To 5
                Dim rowTop As Integer = HeaderHeight() + r * cs.Height
                Dim bar As New Rectangle(1, rowTop + 2, gridW - 2, cs.Height - 4)
                If bar.Height <= 0 OrElse bar.Width <= 0 Then Continue For
                Using path As GraphicsPath = RoundedRect(bar, bar.Height \ 2)
                    Using b As New SolidBrush(_dayBackColor)
                        g.FillPath(b, path)
                    End Using
                End Using

                For c = 0 To 6
                    Dim i As Integer = r * 7 + c
                    Dim dn As Integer = _cells(i)
                    If dn = 0 Then Continue For
                    Dim cell As Rectangle = CellRect(i)
                    If dn = _day Then
                        Dim pill As Rectangle = Rectangle.Inflate(cell, -2, -2)
                        DrawPill(g, pill, _selColor)
                        TextRenderer.DrawText(g, dn.ToString(), New Font(Font, FontStyle.Bold), cell, Color.White, hFlags)
                    Else
                        Dim fc As Color
                        If Not _dayColors.TryGetValue(dn, fc) Then fc = _dayFontColor
                        TextRenderer.DrawText(g, dn.ToString(), Font, cell, fc, hFlags)
                    End If
                Next
            Next
        End Sub

        ''' <summary>Glossy pill: rounded rectangle with a vertical gradient darkest in the middle
        ''' (reproduces the VB6 PaintGraduallySelection look). Both ends rounded.</summary>
        Friend Shared Sub DrawPill(g As Graphics, ByVal rect As Rectangle, ByVal baseColor As Color)
            DrawPill(g, rect, baseColor, True, True)
        End Sub

        ''' <summary>Glossy pill with the ends rounded only where requested (VB6 Month rounds only
        ''' the outer end of each column; the middle-facing end stays square).</summary>
        Friend Shared Sub DrawPill(g As Graphics, ByVal rect As Rectangle, ByVal baseColor As Color,
                                   ByVal roundLeft As Boolean, ByVal roundRight As Boolean)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
            Dim radius As Integer = Math.Min(rect.Height, rect.Width) \ 2
            Using path As GraphicsPath = RoundedRectSides(rect, radius, roundLeft, roundRight)
                Using br As New LinearGradientBrush(New Rectangle(rect.X, rect.Y, rect.Width, rect.Height + 1),
                                                    baseColor, baseColor, LinearGradientMode.Vertical)
                    Dim mid As Color = ColorUtil.ShiftChannels(baseColor, -48)
                    Dim blend As New ColorBlend(3)
                    blend.Colors = New Color() {baseColor, mid, baseColor}
                    blend.Positions = New Single() {0.0F, 0.5F, 1.0F}
                    br.InterpolationColors = blend
                    g.FillPath(br, path)
                End Using
            End Using
        End Sub

        ''' <summary>Rounded rect where only the requested ends get the semicircle; the other end is square.</summary>
        Friend Shared Function RoundedRectSides(ByVal r As Rectangle, ByVal radius As Integer,
                                                ByVal roundLeft As Boolean, ByVal roundRight As Boolean) As GraphicsPath
            Dim p As New GraphicsPath()
            Dim d As Integer = radius * 2
            If d <= 0 Then
                p.AddRectangle(r) : Return p
            End If
            If d > r.Width Then d = r.Width
            If d > r.Height Then d = r.Height
            ' top-left
            If roundLeft Then p.AddArc(r.X, r.Y, d, d, 180, 90) Else p.AddLine(r.X, r.Y, r.X, r.Y)
            ' top-right
            If roundRight Then p.AddArc(r.Right - d, r.Y, d, d, 270, 90) Else p.AddLine(r.Right - 1, r.Y, r.Right - 1, r.Y)
            ' bottom-right
            If roundRight Then p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90) Else p.AddLine(r.Right - 1, r.Bottom - 1, r.Right - 1, r.Bottom - 1)
            ' bottom-left
            If roundLeft Then p.AddArc(r.X, r.Bottom - d, d, d, 90, 90) Else p.AddLine(r.X, r.Bottom - 1, r.X, r.Bottom - 1)
            p.CloseFigure()
            Return p
        End Function

        Friend Shared Function RoundedRect(ByVal r As Rectangle, ByVal radius As Integer) As GraphicsPath
            Dim p As New GraphicsPath()
            Dim d As Integer = radius * 2
            If d <= 0 Then
                p.AddRectangle(r) : Return p
            End If
            If d > r.Width Then d = r.Width
            If d > r.Height Then d = r.Height
            p.AddArc(r.X, r.Y, d, d, 180, 90)
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            p.CloseFigure()
            Return p
        End Function

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            For i = 0 To 41
                If _cells(i) > 0 AndAlso CellRect(i).Contains(e.Location) Then
                    Dim dn As Integer = _cells(i)
                    If dn <> _day Then
                        _day = dn
                        Invalidate()
                        RaiseEvent DayChanged(Me, EventArgs.Empty)
                        RaiseEvent ValueChanged(Me, EventArgs.Empty)
                    End If
                    RaiseEvent DayClick(dn)
                    Exit For
                End If
            Next
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
        End Sub

    End Class

End Namespace
