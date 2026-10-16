Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' 「色環」與「經典」兩頁:都只讀寫共用的 ColorState。
Namespace Global.Aqua

    ''' <summary>色環頁:外圈選色相,內接方塊選飽和度 × 明度。</summary>
    Friend NotInheritable Class HueRingPage
        Inherits Control

        Private Const RingRatio As Double = 0.8      ' 內半徑 / 外半徑
        Private Const SquareRatio As Double = 0.68   ' 方塊半邊長 / 內半徑(略小於 1/√2 留間隙)

        Private ReadOnly _state As ColorState
        Private _ring As Bitmap
        Private _drag As Integer   ' 0 無、1 外圈、2 方塊

        Public Sub New(ByVal state As ColorState)
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _state = state
        End Sub

        Private ReadOnly Property Diameter As Integer
            Get
                Return Math.Max(10, Math.Min(Width, Height) - 2)
            End Get
        End Property

        Private ReadOnly Property Center As PointF
            Get
                Return New PointF(Width / 2.0F, Height / 2.0F)
            End Get
        End Property

        Private ReadOnly Property SquareRect As Rectangle
            Get
                Dim half As Integer = CInt(Diameter / 2.0 * RingRatio * SquareRatio)
                Dim c As PointF = Center
                Return New Rectangle(CInt(c.X) - half, CInt(c.Y) - half, half * 2, half * 2)
            End Get
        End Property

        ''' <summary>色環只隨尺寸變動,重繪時沿用快取。</summary>
        Private Sub EnsureRing()
            Dim d As Integer = Diameter
            If _ring IsNot Nothing AndAlso _ring.Width = d Then Return
            If _ring IsNot Nothing Then _ring.Dispose()
            Dim rOut As Double = d / 2.0, rIn As Double = rOut * RingRatio
            Dim px(d * d - 1) As Integer
            For y As Integer = 0 To d - 1
                For x As Integer = 0 To d - 1
                    Dim dx As Double = x + 0.5 - rOut, dy As Double = y + 0.5 - rOut
                    Dim dist As Double = Math.Sqrt(dx * dx + dy * dy)
                    Dim a As Double = ColorMath.Clamp01(Math.Min(rOut - dist, dist - rIn) + 0.5)
                    If a > 0 Then px(y * d + x) = ColorMath.HsvToArgb(ColorMath.AngleOf(dx, dy), 1, 1, CInt(a * 255))
                Next
            Next
            _ring = ColorMath.MakeBitmap(d, d, px)
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.Clear(scheme.Back)
            EnsureRing()
            Dim c As PointF = Center
            Dim rOut As Single = Diameter / 2.0F
            g.DrawImageUnscaled(_ring, CInt(c.X - rOut), CInt(c.Y - rOut))

            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim rIn As Single = CSng(rOut * RingRatio)
            Using inner As New SolidBrush(scheme.Panel)
                g.FillEllipse(inner, c.X - rIn + 1, c.Y - rIn + 1, (rIn - 1) * 2, (rIn - 1) * 2)
            End Using
            Dim sq As Rectangle = SquareRect
            PickerPaint.DrawSvSquare(g, sq, _state.H)

            ' 外圈上的色相把手
            Dim band As Single = rOut - rIn
            Dim hp As PointF = ColorMath.PointOnCircle(c.X, c.Y, (rOut + rIn) / 2.0F, _state.H)
            Dim hr As Single = band / 2.0F - 1
            Using p As New Pen(Color.White, 2.0F), sh As New Pen(Color.FromArgb(90, 0, 0, 0), 4.0F)
                g.DrawEllipse(sh, hp.X - hr, hp.Y - hr, hr * 2, hr * 2)
                g.DrawEllipse(p, hp.X - hr, hp.Y - hr, hr * 2, hr * 2)
            End Using

            PickerPaint.DrawCrosshair(g, New PointF(CSng(sq.X + _state.S * sq.Width), CSng(sq.Y + (1 - _state.V) * sq.Height)), 9)
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            Dim c As PointF = Center
            Dim dist As Double = Math.Sqrt((e.X - c.X) ^ 2 + (e.Y - c.Y) ^ 2)
            If dist >= Diameter / 2.0 * RingRatio - 2 AndAlso dist <= Diameter / 2.0 + 2 Then
                _drag = 1
            ElseIf Rectangle.Inflate(SquareRect, 4, 4).Contains(e.Location) Then
                _drag = 2
            Else
                _drag = 0
            End If
            Track(e.Location)
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If e.Button = MouseButtons.Left Then Track(e.Location)
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _drag = 0
        End Sub

        Private Sub Track(ByVal pt As Point)
            Select Case _drag
                Case 1
                    Dim c As PointF = Center
                    _state.SetHsv(ColorMath.AngleOf(pt.X - c.X, pt.Y - c.Y), _state.S, _state.V)
                Case 2
                    Dim sq As Rectangle = SquareRect
                    _state.SetHsv(_state.H, (pt.X - sq.X) / CDbl(sq.Width), 1 - (pt.Y - sq.Y) / CDbl(sq.Height))
            End Select
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing AndAlso _ring IsNot Nothing Then
                _ring.Dispose()
                _ring = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

    ''' <summary>經典頁:整塊飽和度 × 明度方塊(色相由右側滑桿調整)。</summary>
    Friend NotInheritable Class ClassicPage
        Inherits Control

        Private ReadOnly _state As ColorState

        Public Sub New(ByVal state As ColorState)
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _state = state
        End Sub

        Private ReadOnly Property Area As Rectangle
            Get
                Return New Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1))
            End Get
        End Property

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.Clear(PickerTheme.Of(Me).Back)
            Dim a As Rectangle = Area
            PickerPaint.DrawSvSquare(g, a, _state.H)
            g.SmoothingMode = SmoothingMode.AntiAlias
            PickerPaint.DrawCrosshair(g, New PointF(CSng(a.X + _state.S * a.Width), CSng(a.Y + (1 - _state.V) * a.Height)), 10)
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then Track(e.Location)
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If e.Button = MouseButtons.Left Then Track(e.Location)
        End Sub

        Private Sub Track(ByVal pt As Point)
            Dim a As Rectangle = Area
            _state.SetHsv(_state.H, (pt.X - a.X) / CDbl(a.Width), 1 - (pt.Y - a.Y) / CDbl(a.Height))
        End Sub

    End Class

End Namespace
