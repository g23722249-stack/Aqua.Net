Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 右側共用區下方的工具列(四個頁籤都看得到):
    '''   [滴管] [複製 ▾] [加入色票 ▾]
    '''   明暗變化條(中間格 = 目前色,左暗右亮)
    '''   對比度預覽(Aa 在白底 / 黑底,WCAG 對比值與等級)
    ''' </summary>
    Friend NotInheritable Class PickerTools
        Inherits Control

        Private Const ButtonHeight As Integer = 28
        Private Const ShadeHeight As Integer = 22
        Private Const ContrastHeight As Integer = 36
        Private Const Gap As Integer = 6
        Private Const ShadeSteps As Integer = 4    ' 中間格左右各幾格

        Private ReadOnly _state As ColorState
        Private ReadOnly _book As PalettePage
        Private ReadOnly _btnPick As New System.Windows.Forms.Button()
        Private ReadOnly _btnCopy As New System.Windows.Forms.Button()
        Private ReadOnly _btnAdd As New System.Windows.Forms.Button()
        Private _hotShade As Integer = -1

        Public Sub New(ByVal state As ColorState, ByVal book As PalettePage)
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _state = state
            _book = book

            _btnPick.Text = "滴管"
            _btnCopy.Text = "複製 ▾"
            _btnAdd.Text = "加入色票 ▾"
            For Each b As System.Windows.Forms.Button In New System.Windows.Forms.Button() {_btnPick, _btnCopy, _btnAdd}
                PickerTheme.Dark.StyleButton(b)
                Controls.Add(b)
            Next
            AddHandler _btnPick.Click, AddressOf OnPickClick
            AddHandler _btnCopy.Click, AddressOf OnCopyClick
            AddHandler _btnAdd.Click, AddressOf OnAddClick
        End Sub

        ''' <summary>工具列所需高度(ColorPicker 排版用)。</summary>
        Public Shared ReadOnly Property PreferredHeight As Integer
            Get
                Return ButtonHeight + Gap + ShadeHeight + Gap + ContrastHeight
            End Get
        End Property

        ''' <summary>寬版(橫跨整個選色器):第一列三個按鈕＋白底／黑底對比,第二列明暗變化。</summary>
        Public Shared ReadOnly Property WideHeight As Integer
            Get
                Return ButtonHeight + Gap + ShadeHeight
            End Get
        End Property

        Private _wide As Boolean

        Public Property Wide As Boolean
            Get
                Return _wide
            End Get
            Set(ByVal value As Boolean)
                If _wide = value Then Return
                _wide = value
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>寬版時三個按鈕佔的寬度,右邊放對比預覽。</summary>
        Private Const WideButtonsWidth As Integer = 52 + 4 + 60 + 4 + 90

        ''' <summary>精簡版(浮動視窗):一列,三個短按鈕＋明暗變化,不顯示對比預覽。</summary>
        Public Shared ReadOnly Property CompactHeight As Integer
            Get
                Return ButtonHeight
            End Get
        End Property

        Private _compact As Boolean
        Private Const CompactButtonsWidth As Integer = 42 + 4 + 60 + 4 + 60

        Public Property Compact As Boolean
            Get
                Return _compact
            End Get
            Set(ByVal value As Boolean)
                If _compact = value Then Return
                _compact = value
                _btnAdd.Text = If(value, "色票 ▾", "加入色票 ▾")
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>明暗變化條的位置:精簡版在按鈕右邊,其他在按鈕列下面。</summary>
        Private Function ShadeStrip() As RectangleF
            If _compact Then
                Dim x As Single = CompactButtonsWidth + Gap
                Return New RectangleF(x, (ButtonHeight - ShadeHeight) / 2.0F, Width - x, ShadeHeight)
            End If
            Return New RectangleF(0, ShadeTop, Width, ShadeHeight)
        End Function

        Public Sub ApplyTheme(ByVal scheme As PickerTheme)
            scheme.StyleButton(_btnPick)
            scheme.StyleButton(_btnCopy)
            scheme.StyleButton(_btnAdd)
            Invalidate()
        End Sub

#Region "版面"

        Protected Overrides Sub OnLayout(ByVal e As LayoutEventArgs)
            MyBase.OnLayout(e)
            ' 「加入色票 ▾」字最長,吃剩下的寬度。
            Dim w1 As Integer = If(_compact, 42, 52), w2 As Integer = 60
            _btnPick.SetBounds(0, 0, w1, ButtonHeight)
            _btnCopy.SetBounds(w1 + 4, 0, w2, ButtonHeight)
            Dim right As Integer = If(_compact, CompactButtonsWidth, If(_wide, WideButtonsWidth, Width))
            _btnAdd.SetBounds(w1 + w2 + 8, 0, Math.Max(40, right - w1 - w2 - 8), ButtonHeight)
        End Sub

        Private ReadOnly Property ShadeTop As Integer
            Get
                Return ButtonHeight + Gap ' 寬版也一樣:按鈕列下面
            End Get
        End Property

        Private ReadOnly Property ContrastTop As Integer
            Get
                If _wide Then Return 0
                Return ShadeTop + ShadeHeight + Gap
            End Get
        End Property

        Private Function ShadeRect(ByVal i As Integer) As RectangleF
            Dim n As Integer = ShadeSteps * 2 + 1
            Dim strip As RectangleF = ShadeStrip()
            Dim w As Single = strip.Width / n
            Return New RectangleF(strip.X + i * w, strip.Y, w, strip.Height)
        End Function

        ''' <summary>
        ''' 明暗變化:以 HSL 亮度為軸,左側往黑、右側往白各等分 ShadeSteps 格。
        ''' 中間格(i = ShadeSteps)就是目前色本身,不經轉換以免誤差。
        ''' </summary>
        Private Function ShadeColor(ByVal i As Integer) As Color
            Dim c As Color = _state.Color
            Dim k As Integer = i - ShadeSteps
            If k = 0 Then Return c
            Dim h, s, l As Double
            ColorMath.RgbToHsl(c, h, s, l)
            ' 用 _state.H 而非算出的 h:灰階時保留使用者原本的色相。
            Dim t As Double = Math.Abs(k) / (ShadeSteps + 1.0)
            Dim nl As Double = If(k < 0, l * (1.0 - t), l + (1.0 - l) * t)
            Return ColorMath.HslToColor(_state.H, s, nl)
        End Function

#End Region

#Region "繪製"

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.Clear(scheme.Back)
            g.SmoothingMode = SmoothingMode.AntiAlias
            DrawShades(g, scheme)
            If _compact Then Return ' 精簡版不顯示對比預覽
            Dim half As Integer = (Width - Gap) \ 2
            If _wide Then
                Dim x0 As Integer = WideButtonsWidth + Gap
                Dim cw As Integer = (Width - x0 - Gap) \ 2
                DrawContrast(g, New Rectangle(x0, 0, cw, ButtonHeight), Color.White, scheme)
                DrawContrast(g, New Rectangle(Width - cw, 0, cw, ButtonHeight), Color.Black, scheme)
            Else
                DrawContrast(g, New Rectangle(0, ContrastTop, half, ContrastHeight), Color.White, scheme)
                DrawContrast(g, New Rectangle(Width - half, ContrastTop, half, ContrastHeight), Color.Black, scheme)
            End If
        End Sub

        Private Sub DrawShades(ByVal g As Graphics, ByVal scheme As PickerTheme)
            Dim strip As RectangleF = ShadeStrip()
            strip.Width -= 1
            Using clip As GraphicsPath = PickerPaint.RoundRect(strip, 4)
                Dim old As Region = g.Clip
                g.SetClip(clip, CombineMode.Intersect)
                For i As Integer = 0 To ShadeSteps * 2
                    Dim r As RectangleF = ShadeRect(i)
                    Using b As New SolidBrush(ShadeColor(i))
                        g.FillRectangle(b, r.X, r.Y, r.Width + 1, r.Height)
                    End Using
                Next
                g.Clip = old
            End Using
            ' 中間格(目前色)與滑過的格子加框
            For Each i As Integer In New Integer() {ShadeSteps, _hotShade}
                If i < 0 Then Continue For
                Dim r As RectangleF = RectangleF.Inflate(ShadeRect(i), -1.5F, -1.5F)
                Using p As New Pen(If(i = ShadeSteps, scheme.HotRing, scheme.Text), 2.0F), path As GraphicsPath = PickerPaint.RoundRect(r, 3)
                    g.DrawPath(p, path)
                End Using
            Next
        End Sub

        ''' <summary>一格對比預覽:背景色上畫「Aa」(目前色),右側寫對比值與 WCAG 等級。</summary>
        Private Sub DrawContrast(ByVal g As Graphics, ByVal r As Rectangle, ByVal background As Color, ByVal scheme As PickerTheme)
            Dim c As Color = _state.Color
            Dim ratio As Double = ColorMath.ContrastRatio(c, background)
            Using bg As New SolidBrush(background), edge As New Pen(scheme.Border),
                  path As GraphicsPath = PickerPaint.RoundRect(New RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), 5)
                g.FillPath(bg, path)
                g.DrawPath(edge, path)
            End Using
            Using big As New Font(Font.FontFamily, 13.0F, FontStyle.Bold), fg As New SolidBrush(c),
                  info As New SolidBrush(If(background.GetBrightness() > 0.5F, Color.FromArgb(90, 90, 90), Color.FromArgb(190, 190, 190))),
                  center As New StringFormat() With {.LineAlignment = StringAlignment.Center},
                  right As New StringFormat() With {.Alignment = StringAlignment.Far}
                g.DrawString("Aa", big, fg, New RectangleF(r.X + 6, r.Y, 40, r.Height), center)
                Dim textArea As New RectangleF(r.X, r.Y + 3, r.Width - 6, r.Height - 6)
                ' 矮的格子(寬版)寫成一行
                Dim sep As String = If(r.Height < 34, "  ", vbLf)
                If r.Height < 34 Then right.LineAlignment = StringAlignment.Center
                g.DrawString(ratio.ToString("0.0") & ":1" & sep & Grade(ratio), Font, info, textArea, right)
            End Using
        End Sub

        ''' <summary>WCAG 2.x:≥7 AAA、≥4.5 AA、≥3 僅大字可用、其餘不足。</summary>
        Private Shared Function Grade(ByVal ratio As Double) As String
            If ratio >= 7.0 Then Return "AAA"
            If ratio >= 4.5 Then Return "AA"
            If ratio >= 3.0 Then Return "大字 AA"
            Return "不足"
        End Function

#End Region

#Region "滑鼠(明暗變化條)"

        Private Function HitShade(ByVal pt As Point) As Integer
            For i As Integer = 0 To ShadeSteps * 2
                If ShadeRect(i).Contains(pt) Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim h As Integer = HitShade(e.Location)
            If h = _hotShade Then Return
            _hotShade = h
            Cursor = If(h >= 0, Cursors.Hand, Cursors.Default)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(ByVal e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hotShade = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(ByVal e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim i As Integer = HitShade(e.Location)
            If e.Button = MouseButtons.Left AndAlso i >= 0 AndAlso i <> ShadeSteps Then
                _state.Color = ShadeColor(i)
            End If
        End Sub

#End Region

#Region "按鈕"

        Private Sub OnPickClick(ByVal sender As Object, ByVal e As EventArgs)
            Dim c As Color
            If EyedropperForm.Pick(Me, c) Then _state.Color = c
        End Sub

        Private Sub OnCopyClick(ByVal sender As Object, ByVal e As EventArgs)
            Dim c As Color = _state.Color
            Dim h, s, l As Double
            ColorMath.RgbToHsl(c, h, s, l)
            Dim formats() As String = {
                ColorMath.ToHex(c),
                String.Format("rgb({0}, {1}, {2})", c.R, c.G, c.B),
                String.Format("hsl({0}, {1}%, {2}%)", Math.Round(_state.H), Math.Round(s * 100), Math.Round(l * 100))}
            Dim menu As New ContextMenuStrip()
            For Each f As String In formats
                Dim text As String = f
                menu.Items.Add("複製  " & text, Nothing, Sub(s2, e2) CopyText(text))
            Next
            ShowMenu(menu, _btnCopy)
        End Sub

        Private Sub CopyText(ByVal text As String)
            Try
                Clipboard.SetText(text)
            Catch ex As System.Runtime.InteropServices.ExternalException
                ' 剪貼簿被其他程式占用
                MessageBox.Show(Me, "剪貼簿暫時無法使用,請再試一次。", "複製", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub

        Private Sub OnAddClick(ByVal sender As Object, ByVal e As EventArgs)
            Dim menu As New ContextMenuStrip()
            For Each p As ColorPalette In _book.UserPalettes()
                Dim target As ColorPalette = p
                menu.Items.Add("加入「" & p.Name & "」", Nothing, Sub(s2, e2) _book.AddCurrentColorTo(target))
            Next
            If menu.Items.Count > 0 Then menu.Items.Add(New ToolStripSeparator())
            menu.Items.Add("新增色票…", Nothing, Sub(s2, e2) _book.NewPaletteFromCurrent(Me))
            ShowMenu(menu, _btnAdd)
        End Sub

        ''' <summary>在按鈕正下方顯示選單,關閉後釋放。</summary>
        Private Shared Sub ShowMenu(ByVal menu As ContextMenuStrip, ByVal anchor As Control)
            AddHandler menu.Closed, Sub(s, e) menu.BeginInvoke(New MethodInvoker(AddressOf menu.Dispose))
            menu.Show(anchor, New Point(0, anchor.Height))
        End Sub

#End Region

    End Class

    ''' <summary>
    ''' 滴管:先擷取整個虛擬螢幕(含多螢幕)成靜態畫面,再以全螢幕視窗蓋上讓使用者點選。
    ''' 游標旁顯示放大鏡(11×11 像素放大 9 倍)與色碼;左鍵 / Enter 選取、右鍵 / Esc 取消、方向鍵逐像素微調。
    ''' </summary>
    <DesignerCategory("Code")>
    Friend NotInheritable Class EyedropperForm
        Inherits Form

        Private Const Span As Integer = 11      ' 放大鏡取樣邊長(像素,奇數)
        Private Const Zoom As Integer = 9
        Private Const LoupeSize As Integer = Span * Zoom
        Private Const LabelHeight As Integer = 22
        Private Const Offset As Integer = 24     ' 放大鏡與游標的距離

        Private ReadOnly _shot As Bitmap
        Private ReadOnly _cursor As DropperCursor
        Private _mouse As New Point(-10000, -10000)

        Private Sub New(ByVal shot As Bitmap, ByVal bounds As Rectangle)
            _shot = shot
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
            StartPosition = FormStartPosition.Manual
            ShowInTaskbar = False
            TopMost = True
            KeyPreview = True
            DoubleBuffered = True
            _cursor = DropperCursor.TryCreate()
            Cursor = If(_cursor IsNot Nothing, _cursor.Cursor, Cursors.Cross)
            BackColor = Color.Black
            Me.Bounds = bounds
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing AndAlso _cursor IsNot Nothing Then
                Cursor = Cursors.Default
                _cursor.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

        Public Property PickedColor As Color

        ''' <summary>
        ''' 開啟滴管。owner 位於 ColorPickerDialog 時,擷取前先把對話框變透明,
        ''' 才吸得到被對話框蓋住的內容;內嵌在其他表單時不隱藏(通常要吸同視窗的影像)。
        ''' </summary>
        Public Shared Function Pick(ByVal owner As Control, ByRef result As Color) As Boolean
            Dim top As Form = owner.FindForm()
            Dim hideOwner As Boolean = TypeOf top Is ColorPickerDialog
            Dim oldOpacity As Double = If(top IsNot Nothing, top.Opacity, 1.0)
            Try
                If hideOwner Then
                    top.Opacity = 0
                    top.Update()
                    Application.DoEvents()
                    Threading.Thread.Sleep(200)   ' 等桌面合成器真的把對話框畫掉
                End If

                Dim vs As Rectangle = SystemInformation.VirtualScreen
                Using shot As New Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppRgb)
                    Using g As Graphics = Graphics.FromImage(shot)
                        g.CopyFromScreen(vs.Location, Point.Empty, vs.Size)
                    End Using
                    Using f As New EyedropperForm(shot, vs)
                        If f.ShowDialog(owner) <> DialogResult.OK Then Return False
                        result = f.PickedColor
                        Return True
                    End Using
                End Using
            Catch ex As System.ComponentModel.Win32Exception
                MessageBox.Show(owner, "無法擷取螢幕畫面:" & ex.Message, "滴管", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            Finally
                If hideOwner Then top.Opacity = oldOpacity
            End Try
        End Function

        Private Function LoupeRect(ByVal m As Point) As Rectangle
            Dim x As Integer = m.X + Offset, y As Integer = m.Y + Offset
            If x + LoupeSize > ClientSize.Width Then x = m.X - Offset - LoupeSize
            If y + LoupeSize + LabelHeight > ClientSize.Height Then y = m.Y - Offset - LoupeSize - LabelHeight
            Return New Rectangle(x, y, LoupeSize, LoupeSize + LabelHeight)
        End Function

        Private Function ColorAt(ByVal m As Point) As Color
            Dim x As Integer = Math.Max(0, Math.Min(_shot.Width - 1, m.X))
            Dim y As Integer = Math.Max(0, Math.Min(_shot.Height - 1, m.Y))
            Return Color.FromArgb(255, _shot.GetPixel(x, y))
        End Function

        Protected Overrides Sub OnPaintBackground(ByVal e As PaintEventArgs)
            ' 整張由 OnPaint 畫,不先清背景以免閃爍。
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.DrawImage(_shot, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel)

            Dim r As Rectangle = LoupeRect(_mouse)
            If Not r.IntersectsWith(e.ClipRectangle) Then Return
            Dim lens As New Rectangle(r.X, r.Y, LoupeSize, LoupeSize)
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            Using back As New SolidBrush(Color.FromArgb(40, 40, 40))
                g.FillRectangle(back, lens)   ' 螢幕邊緣外的取樣區域
            End Using
            g.DrawImage(_shot, lens, New Rectangle(_mouse.X - Span \ 2, _mouse.Y - Span \ 2, Span, Span), GraphicsUnit.Pixel)
            g.PixelOffsetMode = PixelOffsetMode.Default

            Dim cell As New Rectangle(lens.X + (Span \ 2) * Zoom, lens.Y + (Span \ 2) * Zoom, Zoom, Zoom)
            Using outer As New Pen(Color.Black, 3), inner As New Pen(Color.White, 1)
                g.DrawRectangle(outer, lens)
                g.DrawRectangle(inner, lens)
                g.DrawRectangle(outer, cell)
                g.DrawRectangle(inner, cell)
            End Using

            Dim c As Color = ColorAt(_mouse)
            Dim label As New Rectangle(r.X, lens.Bottom, LoupeSize, LabelHeight)
            Using lb As New SolidBrush(Color.FromArgb(230, 30, 30, 32)), sw As New SolidBrush(c), fg As New SolidBrush(Color.White),
                  f As New Font("Consolas", 9.0F), sf As New StringFormat() With {.LineAlignment = StringAlignment.Center}
                g.FillRectangle(lb, label)
                g.FillRectangle(sw, label.X + 4, label.Y + 4, LabelHeight - 8, LabelHeight - 8)
                g.DrawString(ColorMath.ToHex(c), f, fg, New RectangleF(label.X + LabelHeight, label.Y, LoupeSize - LabelHeight, LabelHeight), sf)
            End Using
        End Sub

        Private Sub MoveTo(ByVal pt As Point)
            Dim old As Rectangle = LoupeRect(_mouse)
            _mouse = pt
            Invalidate(Rectangle.Inflate(old, 3, 3))
            Invalidate(Rectangle.Inflate(LoupeRect(_mouse), 3, 3))
        End Sub

        Protected Overrides Sub OnShown(ByVal e As EventArgs)
            MyBase.OnShown(e)
            Activate()
            MoveTo(PointToClient(System.Windows.Forms.Cursor.Position))
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            MoveTo(e.Location)
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                PickedColor = ColorAt(e.Location)
                DialogResult = DialogResult.OK
            ElseIf e.Button = MouseButtons.Right Then
                DialogResult = DialogResult.Cancel
            End If
        End Sub

        Protected Overrides Sub OnKeyDown(ByVal e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Dim dx As Integer = 0, dy As Integer = 0
            Select Case e.KeyCode
                Case Keys.Escape : DialogResult = DialogResult.Cancel
                Case Keys.Enter
                    PickedColor = ColorAt(_mouse)
                    DialogResult = DialogResult.OK
                Case Keys.Left : dx = -1
                Case Keys.Right : dx = 1
                Case Keys.Up : dy = -1
                Case Keys.Down : dy = 1
            End Select
            If dx <> 0 OrElse dy <> 0 Then
                Dim p As Point = System.Windows.Forms.Cursor.Position
                System.Windows.Forms.Cursor.Position = New Point(p.X + dx, p.Y + dy)   ' 觸發 MouseMove 更新放大鏡
                e.Handled = True
            End If
        End Sub

    End Class

    ''' <summary>
    ''' 滴管游標:十字準心(熱點在中心)+ 右上方斜放的滴管圖。
    ''' 系統十字游標是反色(XOR)游標,無法疊畫,故自行畫外觀相同的十字(黑線白邊,深淺底都看得清楚)。
    ''' 尺寸跟隨系統游標大小(100% 縮放為 32×32,高 DPI 等比放大)。
    ''' </summary>
    Public NotInheritable Class DropperCursor
        Implements IDisposable

        <StructLayout(LayoutKind.Sequential)>
        Private Structure ICONINFO
            Public fIcon As Boolean
            Public xHotspot As Integer
            Public yHotspot As Integer
            Public hbmMask As IntPtr
            Public hbmColor As IntPtr
        End Structure

        <DllImport("user32.dll")>
        Private Shared Function GetIconInfo(ByVal hIcon As IntPtr, ByRef info As ICONINFO) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function CreateIconIndirect(ByRef info As ICONINFO) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function DestroyIcon(ByVal hIcon As IntPtr) As Boolean
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(ByVal hObject As IntPtr) As Boolean
        End Function

        Private _handle As IntPtr
        Private _cursor As Cursor

        Private Sub New()
        End Sub

        Public ReadOnly Property Cursor As Cursor
            Get
                Return _cursor
            End Get
        End Property

        ''' <summary>建立失敗(例如 GDI 資源不足)回傳 Nothing,呼叫端退回系統十字游標。</summary>
        Public Shared Function TryCreate() As DropperCursor
            Dim size As Integer = Math.Max(32, SystemInformation.CursorSize.Width)
            Dim hotspot As Integer = size \ 2
            Dim hIcon As IntPtr = IntPtr.Zero
            Try
                Using bmp As Bitmap = Render(size)
                    hIcon = bmp.GetHicon()
                End Using
                Dim info As ICONINFO
                If Not GetIconInfo(hIcon, info) Then Return Nothing
                Try
                    info.fIcon = False          ' False = 游標(才會使用熱點)
                    info.xHotspot = hotspot
                    info.yHotspot = hotspot
                    Dim hCursor As IntPtr = CreateIconIndirect(info)
                    If hCursor = IntPtr.Zero Then Return Nothing
                    Return New DropperCursor() With {._handle = hCursor, ._cursor = New Cursor(hCursor)}
                Finally
                    DeleteObject(info.hbmMask)
                    DeleteObject(info.hbmColor)
                End Try
            Catch ex As Exception When TypeOf ex Is ExternalException OrElse TypeOf ex Is ArgumentException
                Return Nothing
            Finally
                If hIcon <> IntPtr.Zero Then DestroyIcon(hIcon)
            End Try
        End Function

        ''' <summary>畫出游標圖(size × size,熱點在正中央)。</summary>
        Public Shared Function Render(ByVal size As Integer) As Bitmap
            Dim s As Single = size / 32.0F           ' 以 32×32 為設計基準
            Dim c As Single = size \ 2
            Dim bmp As New Bitmap(size, size, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.Clear(Color.Transparent)

                ' --- 十字準心:白邊黑線,中心即熱點 ---
                Dim arm As Single = 9 * s
                Using halo As New Pen(Color.White, 3 * s), line As New Pen(Color.Black, 1 * s)
                    halo.StartCap = LineCap.Square : halo.EndCap = LineCap.Square
                    For Each p As Pen In New Pen() {halo, line}
                        g.DrawLine(p, c - arm, c + 0.5F, c + arm + 1, c + 0.5F)
                        g.DrawLine(p, c + 0.5F, c - arm, c + 0.5F, c + arm + 1)
                    Next
                End Using

                ' --- 滴管:尖端在準心右上,往右上 45° 延伸(白管身 + 黑套環 + 黑吸球) ---
                g.SmoothingMode = SmoothingMode.AntiAlias
                Dim state As GraphicsState = g.Save()
                g.TranslateTransform(c + 3 * s, c - 3 * s)
                g.RotateTransform(-45)                ' 本地 x 軸 = 螢幕右上方
                Using tube As New GraphicsPath(), outline As New Pen(Color.Black, 1.2F * s),
                      halo As New Pen(Color.White, 2.4F * s) With {.LineJoin = LineJoin.Round},
                      white As New SolidBrush(Color.White), black As New SolidBrush(Color.Black),
                      bulb As GraphicsPath = PickerPaint.RoundRect(New RectangleF(9.4F * s, -2.4F * s, 5.6F * s, 4.8F * s), 2.4F * s),
                      collar As New GraphicsPath()
                    ' 管身(含尖端)
                    tube.AddPolygon(New PointF() {
                        New PointF(0, 0),
                        New PointF(2 * s, -1.4F * s), New PointF(8 * s, -1.4F * s),
                        New PointF(8 * s, 1.4F * s), New PointF(2 * s, 1.4F * s)})
                    collar.AddRectangle(New RectangleF(8 * s, -2.6F * s, 1.8F * s, 5.2F * s))
                    ' 先畫白邊:黑色吸球 / 套環在深色底上才看得到(同十字的白邊做法)
                    For Each part As GraphicsPath In New GraphicsPath() {tube, collar, bulb}
                        g.DrawPath(halo, part)
                    Next
                    g.FillPath(white, tube)
                    g.DrawPath(outline, tube)
                    g.FillPath(black, collar)
                    g.FillPath(black, bulb)
                End Using
                g.Restore(state)
            End Using
            Return bmp
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _cursor IsNot Nothing Then
                _cursor.Dispose()
                _cursor = Nothing
            End If
            If _handle <> IntPtr.Zero Then
                DestroyIcon(_handle)              ' New Cursor(IntPtr) 不擁有 handle,需自行釋放
                _handle = IntPtr.Zero
            End If
        End Sub

    End Class

End Namespace
