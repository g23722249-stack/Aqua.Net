Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' ColorPicker 內部用的小零件:頁籤列、漸層滑桿、色塊格、共用繪圖。
Namespace Global.Aqua

    Friend Module PickerPaint

        Public Function RoundRect(ByVal r As RectangleF, ByVal radius As Single) As GraphicsPath
            Dim p As New GraphicsPath()
            Dim d As Single = Math.Min(radius * 2.0F, Math.Min(r.Width, r.Height))
            If d <= 0 Then
                p.AddRectangle(r)
                Return p
            End If
            p.AddArc(r.X, r.Y, d, d, 180, 90)
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            p.CloseFigure()
            Return p
        End Function

        ''' <summary>飽和度(橫)× 明度(縱)方塊。</summary>
        Public Sub DrawSvSquare(ByVal g As Graphics, ByVal rect As Rectangle, ByVal hue As Double)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
            ' 筆刷範圍外擴 1px,避開 GDI+ 漸層邊緣的回捲色線。
            Dim br As Rectangle = Rectangle.Inflate(rect, 1, 1)
            Using hb As New LinearGradientBrush(br, Color.White, ColorMath.HsvToColor(hue, 1, 1), LinearGradientMode.Horizontal)
                g.FillRectangle(hb, rect)
            End Using
            Using vb As New LinearGradientBrush(br, Color.FromArgb(0, 0, 0, 0), Color.Black, LinearGradientMode.Vertical)
                g.FillRectangle(vb, rect)
            End Using
        End Sub

        ''' <summary>參考圖的準星:白圈 + 十字。</summary>
        Public Sub DrawCrosshair(ByVal g As Graphics, ByVal pt As PointF, ByVal radius As Single)
            Using shadowPen As New Pen(Color.FromArgb(90, 0, 0, 0), 3.0F), whitePen As New Pen(Color.White, 1.6F)
                For Each p As Pen In New Pen() {shadowPen, whitePen}
                    g.DrawEllipse(p, pt.X - radius, pt.Y - radius, radius * 2, radius * 2)
                    g.DrawLine(p, pt.X - radius - 3, pt.Y, pt.X - radius * 0.35F, pt.Y)
                    g.DrawLine(p, pt.X + radius * 0.35F, pt.Y, pt.X + radius + 3, pt.Y)
                    g.DrawLine(p, pt.X, pt.Y - radius - 3, pt.X, pt.Y - radius * 0.35F)
                    g.DrawLine(p, pt.X, pt.Y + radius * 0.35F, pt.X, pt.Y + radius + 3)
                Next
            End Using
        End Sub

        ''' <summary>色塊圓點;Color.Empty 畫成空的外框圓(空槽)。</summary>
        Public Sub DrawDot(ByVal g As Graphics, ByVal r As RectangleF, ByVal c As Color, ByVal hot As Boolean, ByVal scheme As PickerTheme)
            If c.IsEmpty Then
                Using p As New Pen(scheme.EmptySlot, 1.2F)
                    g.DrawEllipse(p, r)
                End Using
                Return
            End If
            Using b As New SolidBrush(c)
                g.FillEllipse(b, r)
            End Using
            If Not scheme.IsDark Then
                ' 淺色底上白 / 淺色色塊會糊在背景裡,補一圈細框。
                Using p As New Pen(Color.FromArgb(40, 0, 0, 0), 1.0F)
                    g.DrawEllipse(p, r)
                End Using
            End If
            If hot Then
                Using p As New Pen(scheme.HotRing, 2.0F)
                    g.DrawEllipse(p, RectangleF.Inflate(r, 2, 2))
                End Using
            End If
        End Sub

    End Module

    ''' <summary>分段式頁籤列。</summary>
    Friend NotInheritable Class SegmentBar
        Inherits Control

        Private _items() As String = New String() {}
        Private _selected As Integer
        Private _showToggle As Boolean
        Private _collapsed As Boolean
        Private _hotToggle As Boolean

        Public Event SelectedIndexChanged As EventHandler
        ''' <summary>點了某個頁籤(即使已經是選取的那個);縮小時用來還原尺寸。</summary>
        Public Event SegmentClicked(ByVal sender As Object, ByVal index As Integer)
        ''' <summary>點了右端的縮小 / 還原按鈕。</summary>
        Public Event ToggleClicked As EventHandler

        ''' <summary>右端顯示縮小 / 還原按鈕。</summary>
        Public Property ShowToggle As Boolean
            Get
                Return _showToggle
            End Get
            Set(ByVal value As Boolean)
                _showToggle = value
                Invalidate()
            End Set
        End Property

        ''' <summary>按鈕圖示:False = ︿(按了縮小),True = ﹀(按了還原)。</summary>
        Public Property Collapsed As Boolean
            Get
                Return _collapsed
            End Get
            Set(ByVal value As Boolean)
                _collapsed = value
                Invalidate()
            End Set
        End Property

        Private Function ToggleRect() As RectangleF
            Return New RectangleF(Width - Height, 0, Height, Height)
        End Function

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        Public Property Items As String()
            Get
                Return _items
            End Get
            Set(ByVal value As String())
                _items = If(value, New String() {})
                Invalidate()
            End Set
        End Property

        Public Property SelectedIndex As Integer
            Get
                Return _selected
            End Get
            Set(ByVal value As Integer)
                If value = _selected Then Return
                _selected = value
                Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Function SegmentRect(ByVal i As Integer) As RectangleF
            Dim w As Single = CSng(SegmentsWidth() - 4) / Math.Max(1, _items.Length)
            Return New RectangleF(2 + i * w, 2, w, Height - 4)
        End Function

        Private Function SegmentsWidth() As Integer
            Return If(_showToggle, Width - Height - 4, Width)
        End Function

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(scheme.Back)
            If _showToggle Then DrawToggle(g, scheme)
            Using bg As New SolidBrush(scheme.Panel), path As GraphicsPath = PickerPaint.RoundRect(New RectangleF(0, 0, SegmentsWidth() - 1, Height - 1), 6)
                g.FillPath(bg, path)
                If Not scheme.IsDark Then
                    Using edge As New Pen(scheme.Border)
                        g.DrawPath(edge, path)
                    End Using
                End If
            End Using
            Using sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center},
                  fg As New SolidBrush(scheme.Text), sep As New Pen(scheme.Border)
                For i As Integer = 0 To _items.Length - 1
                    Dim r As RectangleF = SegmentRect(i)
                    If i = _selected Then
                        Using sb As New SolidBrush(scheme.Segment), p As GraphicsPath = PickerPaint.RoundRect(r, 5)
                            g.FillPath(sb, p)
                            If Not scheme.IsDark Then
                                Using edge As New Pen(scheme.HotRing)
                                    g.DrawPath(edge, p)
                                End Using
                            End If
                        End Using
                    ElseIf i > 0 AndAlso i - 1 <> _selected Then
                        g.DrawLine(sep, r.X, r.Y + 5, r.X, r.Bottom - 5)
                    End If
                    g.DrawString(_items(i), Font, fg, r, sf)
                Next
            End Using
        End Sub

        ''' <summary>縮小 / 還原按鈕:圓角方塊裡一個 ︿ 或 ﹀。</summary>
        Private Sub DrawToggle(ByVal g As Graphics, ByVal scheme As PickerTheme)
            Dim r As RectangleF = ToggleRect()
            r.Inflate(-1, -1)
            Using bg As New SolidBrush(If(_hotToggle, scheme.Segment, scheme.Panel)), path As GraphicsPath = PickerPaint.RoundRect(r, 6)
                g.FillPath(bg, path)
                If Not scheme.IsDark OrElse _hotToggle Then
                    Using edge As New Pen(If(_hotToggle, scheme.HotRing, scheme.Border))
                        g.DrawPath(edge, path)
                    End Using
                End If
            End Using
            Dim cx As Single = r.X + r.Width / 2, cy As Single = r.Y + r.Height / 2
            Dim s As Single = r.Height * 0.18F
            Dim dir As Single = If(_collapsed, 1.0F, -1.0F)   ' 縮小後箭頭朝下(按了展開)
            Using p As New Pen(scheme.Text, 1.8F) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round, .LineJoin = LineJoin.Round}
                g.DrawLines(p, New PointF() {New PointF(cx - s * 1.4F, cy - dir * s * 0.6F), New PointF(cx, cy + dir * s * 0.6F),
                                             New PointF(cx + s * 1.4F, cy - dir * s * 0.6F)})
            End Using
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            If _showToggle AndAlso ToggleRect().Contains(e.X, e.Y) Then
                RaiseEvent ToggleClicked(Me, EventArgs.Empty)
                Return
            End If
            For i As Integer = 0 To _items.Length - 1
                If SegmentRect(i).Contains(e.X, e.Y) Then
                    SelectedIndex = i
                    RaiseEvent SegmentClicked(Me, i)
                End If
            Next
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim hot As Boolean = _showToggle AndAlso ToggleRect().Contains(e.X, e.Y)
            If hot <> _hotToggle Then
                _hotToggle = hot
                Invalidate()
            End If
            Cursor = If(hot, Cursors.Hand, Cursors.Default)
        End Sub

        Protected Overrides Sub OnMouseLeave(ByVal e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hotToggle Then
                _hotToggle = False
                Invalidate()
            End If
        End Sub

        ''' <summary>滑鼠在頁籤列上滾動:往下下一個、往上上一個,頭尾循環。</summary>
        Protected Overrides Sub OnMouseWheel(ByVal e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            Dim n As Integer = _items.Length
            If n = 0 OrElse e.Delta = 0 Then Return
            SelectedIndex = (_selected + If(e.Delta < 0, 1, -1) + n) Mod n
        End Sub

        ''' <summary>游標移進來就取得焦點,滾輪訊息才會送到頁籤列(不搶文字輸入框的焦點)。</summary>
        Protected Overrides Sub OnMouseEnter(ByVal e As EventArgs)
            MyBase.OnMouseEnter(e)
            Dim f As Form = FindForm()
            If f IsNot Nothing AndAlso f.ContainsFocus AndAlso Not (TypeOf f.ActiveControl Is System.Windows.Forms.TextBoxBase) Then Focus()
        End Sub

    End Class

    ''' <summary>漸層軌道 + 圓形滑塊。ValueChanged 只在使用者操作時觸發。</summary>
    Friend NotInheritable Class ColorSlider
        Inherits Control

        Private _min As Double = 0
        Private _max As Double = 100
        Private _value As Double
        Private _colors() As Color = New Color() {Color.Black, Color.White}

        Public Event ValueChanged As EventHandler

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        Public Property Maximum As Double
            Get
                Return _max
            End Get
            Set(ByVal value As Double)
                _max = value
                Invalidate()
            End Set
        End Property

        ''' <summary>程式設定,不觸發 ValueChanged。</summary>
        Public Property Value As Double
            Get
                Return _value
            End Get
            Set(ByVal v As Double)
                v = Math.Max(_min, Math.Min(_max, v))
                If v = _value Then Return
                _value = v
                Invalidate()
            End Set
        End Property

        Public Property GradientColors As Color()
            Get
                Return _colors
            End Get
            Set(ByVal value As Color())
                If value Is Nothing OrElse value.Length < 2 Then Return
                _colors = value
                Invalidate()
            End Set
        End Property

        Private ReadOnly Property ThumbRadius As Integer
            Get
                Return Math.Max(4, Math.Min(7, Height \ 2 - 1))
            End Get
        End Property

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(scheme.Back)
            Dim tr As Integer = ThumbRadius
            Dim track As New RectangleF(tr, Height / 2.0F - 3, Math.Max(1, Width - 2 * tr), 6)
            Using lb As New LinearGradientBrush(RectangleF.Inflate(track, 1, 0), _colors(0), _colors(_colors.Length - 1), LinearGradientMode.Horizontal),
                  path As GraphicsPath = PickerPaint.RoundRect(track, 3)
                Dim blend As New ColorBlend(_colors.Length)
                For i As Integer = 0 To _colors.Length - 1
                    blend.Colors(i) = _colors(i)
                    blend.Positions(i) = CSng(i) / (_colors.Length - 1)
                Next
                lb.InterpolationColors = blend
                g.FillPath(lb, path)
            End Using
            Dim frac As Double = If(_max > _min, (_value - _min) / (_max - _min), 0)
            Dim cx As Single = CSng(track.X + frac * track.Width)
            Dim cy As Single = Height / 2.0F
            Dim knob As New RectangleF(cx - tr, cy - tr, tr * 2, tr * 2)
            ' 上淺下深的立體把手:深色主題為淺灰、淺色主題為藍色(參考圖的 Aqua 風格)。
            Dim top As Color = Color.FromArgb(scheme.ThumbFill.R + (255 - scheme.ThumbFill.R) \ 2,
                                              scheme.ThumbFill.G + (255 - scheme.ThumbFill.G) \ 2,
                                              scheme.ThumbFill.B + (255 - scheme.ThumbFill.B) \ 2)
            Using fill As New LinearGradientBrush(RectangleF.Inflate(knob, 0, 1), top, scheme.ThumbFill, LinearGradientMode.Vertical),
                  edge As New Pen(scheme.ThumbEdge)
                g.FillEllipse(fill, knob)
                g.DrawEllipse(edge, knob)
            End Using
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then TrackTo(e.X)
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If e.Button = MouseButtons.Left Then TrackTo(e.X)
        End Sub

        Private Sub TrackTo(ByVal x As Integer)
            Dim tr As Integer = ThumbRadius
            Dim frac As Double = ColorMath.Clamp01((x - tr) / Math.Max(1.0, Width - 2.0 * tr))
            Dim v As Double = _min + frac * (_max - _min)
            If v = _value Then Return
            _value = v
            Invalidate()
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Sub

    End Class

    ''' <summary>固定欄數的圓點色塊格(常用色 / 自訂色)。</summary>
    Friend NotInheritable Class SwatchGrid
        Inherits Control

        Private _colors() As Color = New Color() {}
        Private _columns As Integer = 7
        Private _hot As Integer = -1

        Public Event ColorPicked(ByVal sender As Object, ByVal c As Color)
        ''' <summary>在色塊上按右鍵(僅 Editable 時)。</summary>
        Public Event SlotRightClicked(ByVal sender As Object, ByVal index As Integer, ByVal location As Point)

        ''' <summary>自訂色:色塊可按右鍵編輯。</summary>
        Public Property Editable As Boolean

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        Public Property Colors As Color()
            Get
                Return _colors
            End Get
            Set(ByVal value As Color())
                _colors = If(value, New Color() {})
                Invalidate()
            End Set
        End Property

        Public ReadOnly Property CellSize As Single
            Get
                Return CSng(Width) / _columns
            End Get
        End Property

        Private Function DotRect(ByVal i As Integer) As RectangleF
            Dim cs As Single = CellSize
            Dim pad As Single = Math.Max(3.0F, cs * 0.14F)
            Return New RectangleF((i Mod _columns) * cs + pad, (i \ _columns) * cs + pad, cs - 2 * pad, cs - 2 * pad)
        End Function

        Private Function HitTest(ByVal pt As Point) As Integer
            For i As Integer = 0 To _colors.Length - 1
                If DotRect(i).Contains(pt) AndAlso Not _colors(i).IsEmpty Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.Clear(scheme.Back)
            For i As Integer = 0 To _colors.Length - 1
                PickerPaint.DrawDot(e.Graphics, DotRect(i), _colors(i), i = _hot, scheme)
            Next
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim h As Integer = HitTest(e.Location)
            If h <> _hot Then
                _hot = h
                Cursor = If(h >= 0, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(ByVal e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hot = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(ByVal e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim i As Integer = HitTest(e.Location)
            If i < 0 Then Return
            If e.Button = MouseButtons.Right Then
                If Editable Then RaiseEvent SlotRightClicked(Me, i, e.Location)
            ElseIf e.Button = MouseButtons.Left Then
                RaiseEvent ColorPicked(Me, _colors(i))
            End If
        End Sub

    End Class

End Namespace
