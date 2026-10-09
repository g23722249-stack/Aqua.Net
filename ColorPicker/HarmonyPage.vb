Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 調和頁:色相(角度)× 飽和度(半徑)圓盤,依規則標出配色點;
    ''' 下方色條列出配色結果,點擊即選用。明度沿用目前值。
    ''' </summary>
    Friend NotInheritable Class HarmonyPage
        Inherits Control

        Private Const ComboHeight As Integer = 24
        Private Const StripHeight As Integer = 26
        Private Const Gap As Integer = 6

        Private ReadOnly _state As ColorState
        Private ReadOnly _combo As New System.Windows.Forms.ComboBox()
        Private _rule As ColorHarmonyRule = ColorHarmonyRule.Complementary
        Private _disc As Bitmap
        Private _discKey As String = ""
        Private _dragging As Boolean

        Public Sub New(ByVal state As ColorState)
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _state = state

            _combo.DropDownStyle = ComboBoxStyle.DropDownList
            _combo.FlatStyle = FlatStyle.Flat
            ' 自繪項目:原生下拉框不吃深色底,選取框會一直是白底。
            _combo.DrawMode = DrawMode.OwnerDrawFixed
            AddHandler _combo.DrawItem, AddressOf OnComboDrawItem
            _combo.Items.AddRange(New Object() {"互補", "類似", "三角", "分割互補", "矩形"})
            _combo.SelectedIndex = 0
            AddHandler _combo.SelectedIndexChanged, Sub(s, e) Rule = CType(_combo.SelectedIndex, ColorHarmonyRule)
            Controls.Add(_combo)
        End Sub

        ''' <summary>主題切換時由 ColorPicker 呼叫。</summary>
        Public Sub ApplyTheme(ByVal scheme As PickerTheme)
            scheme.StyleInput(_combo)
            _combo.Invalidate()
            Invalidate()
        End Sub

        Private Sub OnComboDrawItem(ByVal sender As Object, ByVal e As DrawItemEventArgs)
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected AndAlso
                                      (e.State And DrawItemState.ComboBoxEdit) <> DrawItemState.ComboBoxEdit
            Using bg As New SolidBrush(If(selected, scheme.Segment, scheme.Panel)), fg As New SolidBrush(scheme.Text)
                e.Graphics.FillRectangle(bg, e.Bounds)
                If e.Index >= 0 Then
                    e.Graphics.DrawString(_combo.Items(e.Index).ToString(), _combo.Font, fg, e.Bounds.X + 2, e.Bounds.Y + 1)
                End If
            End Using
        End Sub

        Public Property Rule As ColorHarmonyRule
            Get
                Return _rule
            End Get
            Set(ByVal value As ColorHarmonyRule)
                _rule = value
                If _combo.SelectedIndex <> CInt(value) Then _combo.SelectedIndex = CInt(value)
                Invalidate()
            End Set
        End Property

        ''' <summary>各規則相對於基準色相的偏移角度(第一個一律是基準色)。</summary>
        Public Shared Function Offsets(ByVal rule As ColorHarmonyRule) As Double()
            Select Case rule
                Case ColorHarmonyRule.Analogous : Return New Double() {0, -30, 30}
                Case ColorHarmonyRule.Triadic : Return New Double() {0, 120, 240}
                Case ColorHarmonyRule.SplitComplementary : Return New Double() {0, 150, 210}
                Case ColorHarmonyRule.Square : Return New Double() {0, 90, 180, 270}
                Case Else : Return New Double() {0, 180}
            End Select
        End Function

        Public Function HarmonyColors() As Color()
            Dim offs() As Double = Offsets(_rule)
            Dim result(offs.Length - 1) As Color
            For i As Integer = 0 To offs.Length - 1
                result(i) = ColorMath.HsvToColor(_state.H + offs(i), _state.S, _state.V)
            Next
            Return result
        End Function

        Private ReadOnly Property DiscRect As Rectangle
            Get
                Dim top As Integer = ComboHeight + Gap
                Dim avail As Integer = Height - top - StripHeight - Gap
                Dim d As Integer = Math.Max(10, Math.Min(Width, avail))
                Return New Rectangle((Width - d) \ 2, top + (avail - d) \ 2, d, d)
            End Get
        End Property

        Private Function StripRect(ByVal i As Integer, ByVal count As Integer) As RectangleF
            Dim w As Single = CSng(Width) / count
            Return New RectangleF(i * w, Height - StripHeight, w, StripHeight)
        End Function

        Protected Overrides Sub OnLayout(ByVal e As LayoutEventArgs)
            MyBase.OnLayout(e)
            _combo.SetBounds(0, 0, Math.Min(140, Width), ComboHeight)
        End Sub

        ''' <summary>圓盤隨尺寸與明度變動,其餘情況沿用快取。</summary>
        Private Sub EnsureDisc(ByVal d As Integer)
            Dim key As String = d.ToString() & "/" & Math.Round(_state.V, 3).ToString()
            If _disc IsNot Nothing AndAlso key = _discKey Then Return
            If _disc IsNot Nothing Then _disc.Dispose()
            Dim r As Double = d / 2.0
            Dim v As Double = _state.V
            Dim px(d * d - 1) As Integer
            For y As Integer = 0 To d - 1
                For x As Integer = 0 To d - 1
                    Dim dx As Double = x + 0.5 - r, dy As Double = y + 0.5 - r
                    Dim dist As Double = Math.Sqrt(dx * dx + dy * dy)
                    Dim a As Double = ColorMath.Clamp01(r - dist + 0.5)
                    If a > 0 Then px(y * d + x) = ColorMath.HsvToArgb(ColorMath.AngleOf(dx, dy), Math.Min(1.0, dist / r), v, CInt(a * 255))
                Next
            Next
            _disc = ColorMath.MakeBitmap(d, d, px)
            _discKey = key
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.Clear(PickerTheme.Of(Me).Back)
            Dim dr As Rectangle = DiscRect
            EnsureDisc(dr.Width)
            g.DrawImageUnscaled(_disc, dr.X, dr.Y)

            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim cx As Single = dr.X + dr.Width / 2.0F, cy As Single = dr.Y + dr.Height / 2.0F
            Dim radius As Single = CSng(_state.S * dr.Width / 2.0)
            Dim offs() As Double = Offsets(_rule)
            Using line As New Pen(Color.FromArgb(220, 255, 255, 255), 1.4F),
                  ring As New Pen(Color.White, 2.0F), shadow As New Pen(Color.FromArgb(90, 0, 0, 0), 4.0F)
                For i As Integer = 0 To offs.Length - 1
                    Dim p As PointF = ColorMath.PointOnCircle(cx, cy, radius, _state.H + offs(i))
                    g.DrawLine(line, cx, cy, p.X, p.Y)
                Next
                For i As Integer = 0 To offs.Length - 1
                    Dim p As PointF = ColorMath.PointOnCircle(cx, cy, radius, _state.H + offs(i))
                    Dim mr As Single = If(i = 0, 10.0F, 8.0F)
                    g.DrawEllipse(shadow, p.X - mr, p.Y - mr, mr * 2, mr * 2)
                    g.DrawEllipse(ring, p.X - mr, p.Y - mr, mr * 2, mr * 2)
                Next
            End Using

            Dim colors() As Color = HarmonyColors()
            For i As Integer = 0 To colors.Length - 1
                Dim r As RectangleF = StripRect(i, colors.Length)
                Using b As New SolidBrush(colors(i))
                    g.FillRectangle(b, r)
                End Using
            Next
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            If e.Y >= Height - StripHeight Then
                Dim colors() As Color = HarmonyColors()
                For i As Integer = 0 To colors.Length - 1
                    If StripRect(i, colors.Length).Contains(e.X, e.Y) Then _state.Color = colors(i)
                Next
                Return
            End If
            _dragging = DiscRect.Contains(e.Location)
            Track(e.Location)
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If e.Button = MouseButtons.Left Then Track(e.Location)
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
        End Sub

        Private Sub Track(ByVal pt As Point)
            If Not _dragging Then Return
            Dim dr As Rectangle = DiscRect
            Dim dx As Double = pt.X - (dr.X + dr.Width / 2.0), dy As Double = pt.Y - (dr.Y + dr.Height / 2.0)
            Dim s As Double = Math.Sqrt(dx * dx + dy * dy) / (dr.Width / 2.0)
            _state.SetHsv(ColorMath.AngleOf(dx, dy), s, _state.V)
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing AndAlso _disc IsNot Nothing Then
                _disc.Dispose()
                _disc = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
