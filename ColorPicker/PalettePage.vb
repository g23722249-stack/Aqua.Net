Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 色票頁:上方色票清單(可捲動),下方「新增色票」「匯入色票」。
    ''' 使用者色票存在 文件\PainterTool\Palettes,可加色、移除色、改名、刪除。
    ''' </summary>
    Friend NotInheritable Class PalettePage
        Inherits Control

        Private Const ButtonHeight As Integer = 28

        Private ReadOnly _state As ColorState
        Private ReadOnly _palettes As List(Of ColorPalette)
        Private ReadOnly _list As PaletteList
        Private ReadOnly _btnAdd As New System.Windows.Forms.Button()
        Private ReadOnly _btnImport As New System.Windows.Forms.Button()

        Public Event ColorPicked(ByVal sender As Object, ByVal c As Color)

        Public Sub New(ByVal state As ColorState, ByVal palettes As List(Of ColorPalette))
            _state = state
            _palettes = palettes
            _list = New PaletteList(palettes)
            AddHandler _list.ColorPicked, Sub(s, c) RaiseEvent ColorPicked(Me, c)
            AddHandler _list.AddColorRequested, AddressOf OnAddColorRequested
            AddHandler _list.ContextRequested, AddressOf OnContextRequested
            Controls.Add(_list)

            _btnAdd.Text = "新增色票"
            _btnImport.Text = "匯入色票"
            For Each b As System.Windows.Forms.Button In New System.Windows.Forms.Button() {_btnAdd, _btnImport}
                PickerTheme.Dark.StyleButton(b)
                Controls.Add(b)
            Next
            AddHandler _btnAdd.Click, AddressOf OnAddPalette
            AddHandler _btnImport.Click, AddressOf OnImportPalette
        End Sub

        ''' <summary>主題切換時由 ColorPicker 呼叫。</summary>
        Public Sub ApplyTheme(ByVal scheme As PickerTheme)
            scheme.StyleButton(_btnAdd)
            scheme.StyleButton(_btnImport)
            Invalidate(True)
        End Sub

        Public Sub RefreshPalettes()
            _list.RefreshPalettes()
        End Sub

        Protected Overrides Sub OnLayout(ByVal e As LayoutEventArgs)
            MyBase.OnLayout(e)
            Dim half As Integer = (Width - 6) \ 2
            _list.SetBounds(0, 0, Width, Math.Max(10, Height - ButtonHeight - 6))
            _btnAdd.SetBounds(0, Height - ButtonHeight, half, ButtonHeight)
            _btnImport.SetBounds(Width - half, Height - ButtonHeight, half, ButtonHeight)
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            e.Graphics.Clear(PickerTheme.Of(Me).Back)
        End Sub

#Region "使用者操作"

        Private Sub OnAddPalette(ByVal sender As Object, ByVal e As EventArgs)
            NewPaletteFromCurrent(Me)
        End Sub

        ''' <summary>
        ''' 以目前顏色建立一組新色票(詢問名稱並存檔)。
        ''' 色票頁的「新增色票」與右側工具列的「加入色票 → 新增色票…」共用。
        ''' </summary>
        Public Sub NewPaletteFromCurrent(ByVal owner As Control)
            Dim name As String = NamePrompt.Ask(owner, "新增色票", "色票名稱:", "我的色票 " & (UserPalettes().Count + 1))
            If name Is Nothing Then Return
            Dim p As New ColorPalette(name, _state.Color)
            If TrySave(p) Then
                _palettes.Add(p)
                _list.RefreshPalettes()
                _list.ScrollToEnd()
            End If
        End Sub

        ''' <summary>使用者色票(可加色的那些),依清單順序。</summary>
        Public Function UserPalettes() As List(Of ColorPalette)
            Dim result As New List(Of ColorPalette)()
            For Each p As ColorPalette In _palettes
                If p.IsUserPalette Then result.Add(p)
            Next
            Return result
        End Function

        Private Sub OnImportPalette(ByVal sender As Object, ByVal e As EventArgs)
            Using dlg As New OpenFileDialog()
                dlg.Title = "匯入色票"
                dlg.Filter = "色票檔 (*.gpl;*.txt;*.hex)|*.gpl;*.txt;*.hex|所有檔案 (*.*)|*.*"
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    Dim p As ColorPalette = PaletteStore.Import(dlg.FileName)
                    _palettes.Add(p)
                    _list.RefreshPalettes()
                    _list.ScrollToEnd()
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                                          TypeOf ex Is InvalidDataException
                    MessageBox.Show(Me, "無法匯入色票:" & ex.Message, "匯入色票", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                End Try
            End Using
        End Sub

        Private Sub OnAddColorRequested(ByVal sender As Object, ByVal p As ColorPalette)
            AddCurrentColorTo(p)
        End Sub

        ''' <summary>把目前顏色加到使用者色票尾端並存檔(色條的「+」格與工具列共用)。</summary>
        Public Sub AddCurrentColorTo(ByVal p As ColorPalette)
            p.Colors.Add(_state.Color)
            If TrySave(p) Then
                _list.RefreshPalettes()
            Else
                p.Colors.RemoveAt(p.Colors.Count - 1)
            End If
        End Sub

        ''' <summary>使用者色票的右鍵選單;colorIndex = -1 表示點在標題上。</summary>
        Private Sub OnContextRequested(ByVal p As ColorPalette, ByVal colorIndex As Integer, ByVal location As Point)
            Dim menu As New ContextMenuStrip()
            If colorIndex >= 0 Then
                menu.Items.Add("移除此顏色", Nothing, Sub(s, e)
                                                       Dim removed As Color = p.Colors(colorIndex)
                                                       p.Colors.RemoveAt(colorIndex)
                                                       If Not TrySave(p) Then p.Colors.Insert(colorIndex, removed)
                                                       _list.RefreshPalettes()
                                                   End Sub)
            End If
            menu.Items.Add("重新命名…", Nothing, Sub(s, e)
                                                 Dim n As String = NamePrompt.Ask(Me, "重新命名色票", "色票名稱:", p.Name)
                                                 If n Is Nothing Then Return
                                                 Dim old As String = p.Name
                                                 p.Name = n
                                                 If Not TrySave(p) Then p.Name = old
                                                 _list.RefreshPalettes()
                                             End Sub)
            menu.Items.Add("刪除色票", Nothing, Sub(s, e)
                                                If MessageBox.Show(Me, "確定刪除色票「" & p.Name & "」?", "刪除色票",
                                                                   MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
                                                Try
                                                    PaletteStore.DeletePalette(p)
                                                    _palettes.Remove(p)
                                                    _list.RefreshPalettes()
                                                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                                                    MessageBox.Show(Me, "無法刪除色票:" & ex.Message, "刪除色票", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                                End Try
                                            End Sub)
            AddHandler menu.Closed, Sub(s, e) menu.BeginInvoke(New MethodInvoker(AddressOf menu.Dispose))
            menu.Show(_list, location)
        End Sub

        Private Function TrySave(ByVal p As ColorPalette) As Boolean
            Try
                PaletteStore.SavePalette(p)
                Return True
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                MessageBox.Show(Me, "無法儲存色票:" & ex.Message, "色票", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End Try
        End Function

#End Region

#Region "內建色票"

        ''' <summary>內建色票,依序:預設、提升、暖色、冷色、海洋、森林、大地、鮮花、人像、粉彩。</summary>
        Public Shared Function DefaultPalettes() As List(Of ColorPalette)
            Dim list As New List(Of ColorPalette)()
            list.Add(Ramp("預設", Color.Black, Color.White, 12))
            list.Add(Ramp("提升", Color.FromArgb(18, 58, 140), Color.FromArgb(120, 200, 245), 12))
            list.Add(Ramp("暖色", Color.FromArgb(178, 34, 34), Color.FromArgb(250, 228, 80), 12))
            list.Add(Ramp("冷色", Color.FromArgb(128, 40, 200), Color.FromArgb(40, 230, 240), 12))
            list.Add(Ramp("海洋", Color.FromArgb(95, 224, 232), Color.FromArgb(14, 104, 116), 12))
            list.Add(FromHex("森林", "#0B3D20", "#14532D", "#1F6F3A", "#2D8A4E", "#3FA45B", "#5DBB63",
                             "#7BC67E", "#9ACD8A", "#C5D6A8", "#8F9779", "#6B8E23", "#556B2F"))
            list.Add(FromHex("大地", "#3B2414", "#5C3A21", "#7B4B2A", "#8B5A2B", "#A0522D", "#B5793F",
                             "#C69C6D", "#D2B48C", "#DEC8A0", "#A39B84", "#8A7F5A", "#6E6650"))
            list.Add(FromHex("鮮花", "#C2185B", "#E91E63", "#F06292", "#F8BBD0", "#FF7043", "#FFB74D",
                             "#FFEB3B", "#FFF59D", "#E1BEE7", "#BA68C8", "#9575CD", "#7986CB"))
            list.Add(FromHex("人像", "#FFE0BD", "#FFDBAC", "#F8D5C2", "#F1C27D", "#E0AC69", "#D4A373",
                             "#C68642", "#A86B4C", "#8D5524", "#6F4E37", "#5A3825", "#3B2219"))
            list.Add(FromHex("粉彩", "#FFB3BA", "#FFDFBA", "#FFFFBA", "#FDFFB6", "#CAFFBF", "#BAFFC9",
                             "#BAE1FF", "#A2D2FF", "#BDE0FE", "#CDB4DB", "#D7BAFF", "#FFC8DD"))
            Return list
        End Function

        Private Shared Function Ramp(ByVal name As String, ByVal a As Color, ByVal b As Color, ByVal n As Integer) As ColorPalette
            Dim p As New ColorPalette() With {.Name = name}
            For i As Integer = 0 To n - 1
                Dim t As Double = i / CDbl(n - 1)
                p.Colors.Add(Color.FromArgb(CInt(a.R + (CInt(b.R) - a.R) * t),
                                            CInt(a.G + (CInt(b.G) - a.G) * t),
                                            CInt(a.B + (CInt(b.B) - a.B) * t)))
            Next
            Return p
        End Function

        Private Shared Function FromHex(ByVal name As String, ByVal ParamArray hex() As String) As ColorPalette
            Dim p As New ColorPalette() With {.Name = name}
            For Each h As String In hex
                Dim c As Color
                If ColorMath.TryParseHex(h, c) Then p.Colors.Add(c)
            Next
            Return p
        End Function

#End Region

    End Class

    ''' <summary>色票清單本體:分組色條、自繪細捲軸(配合深色外觀)、滾輪捲動。</summary>
    Friend NotInheritable Class PaletteList
        Inherits Control

        Private Const HeaderHeight As Integer = 20
        Private Const StripHeight As Integer = 22
        Private Const GroupGap As Integer = 4
        Private Const BarWidth As Integer = 6

        Private ReadOnly _palettes As List(Of ColorPalette)
        Private ReadOnly _collapsed As New List(Of ColorPalette)()
        Private _offset As Integer
        Private _barDragY As Integer = -1
        Private _barDragOffset As Integer

        Public Event ColorPicked(ByVal sender As Object, ByVal c As Color)
        Public Event AddColorRequested(ByVal sender As Object, ByVal p As ColorPalette)
        Public Event ContextRequested(ByVal p As ColorPalette, ByVal colorIndex As Integer, ByVal location As Point)

        Public Sub New(ByVal palettes As List(Of ColorPalette))
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            _palettes = palettes
        End Sub

        Public Sub RefreshPalettes()
            _collapsed.RemoveAll(Function(p) Not _palettes.Contains(p))
            ClampOffset()
            Invalidate()
        End Sub

        Public Sub ScrollToEnd()
            _offset = MaxOffset
            Invalidate()
        End Sub

#Region "版面"

        Private ReadOnly Property ContentHeight As Integer
            Get
                Dim h As Integer = 0
                For Each p As ColorPalette In _palettes
                    h += HeaderHeight + If(_collapsed.Contains(p), 0, StripHeight) + GroupGap
                Next
                Return h
            End Get
        End Property

        Private ReadOnly Property MaxOffset As Integer
            Get
                Return Math.Max(0, ContentHeight - Height)
            End Get
        End Property

        Private ReadOnly Property ContentWidth As Integer
            Get
                Return Width - If(MaxOffset > 0, BarWidth + 4, 0) - 2
            End Get
        End Property

        Private Sub ClampOffset()
            _offset = Math.Max(0, Math.Min(MaxOffset, _offset))
        End Sub

        ''' <summary>使用者色票在尾端多一格「+」,用來加入目前顏色。</summary>
        Private Shared Function CellCount(ByVal p As ColorPalette) As Integer
            Return p.Colors.Count + If(p.IsUserPalette, 1, 0)
        End Function

        Private Shared Function CellRect(ByVal strip As RectangleF, ByVal i As Integer, ByVal count As Integer) As RectangleF
            ' 色數少時(使用者色票)每格不超過 strip 高度的 2 倍,避免一格拉滿整條。
            Dim w As Single = Math.Min(strip.Width / Math.Max(1, count), If(count < 12, strip.Height * 2, Single.MaxValue))
            ' 寬度 +1 讓相鄰色格之間不留縫。
            Return New RectangleF(strip.X + i * w, strip.Y, w + 1, strip.Height)
        End Function

        Private ReadOnly Property ThumbRect As Rectangle
            Get
                Dim content As Integer = Math.Max(1, ContentHeight)
                Dim th As Integer = Math.Max(20, CInt(Height * CDbl(Height) / content))
                Dim ty As Integer = If(MaxOffset = 0, 0, CInt((Height - th) * CDbl(_offset) / MaxOffset))
                Return New Rectangle(Width - BarWidth, ty, BarWidth, th)
            End Get
        End Property

#End Region

#Region "繪製"

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim scheme As PickerTheme = PickerTheme.Of(Me)
            g.Clear(scheme.Back)
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim y As Integer = -_offset
            Using heading As New SolidBrush(scheme.Heading), sub2 As New SolidBrush(scheme.SubText), rule As New Pen(scheme.Border)
                For Each p As ColorPalette In _palettes
                    Dim collapsed As Boolean = _collapsed.Contains(p)
                    g.DrawString(If(collapsed, "▸", "▾"), Font, sub2, 0, y + 2)
                    g.DrawString(p.Name, Font, heading, 16, y + 2)
                    If Not scheme.IsDark Then
                        ' 淺色主題比照參考圖:分組標題下方一條細線。
                        g.DrawLine(rule, 1, y + HeaderHeight - 2, ContentWidth, y + HeaderHeight - 2)
                    End If
                    y += HeaderHeight
                    If Not collapsed Then
                        DrawStrip(g, p, New RectangleF(1, y, ContentWidth, StripHeight), scheme)
                        y += StripHeight
                    End If
                    y += GroupGap
                Next
            End Using

            If MaxOffset > 0 Then
                Using track As New SolidBrush(scheme.Panel), thumb As New SolidBrush(scheme.ScrollThumb),
                      tp As GraphicsPath = PickerPaint.RoundRect(New RectangleF(Width - BarWidth, 0, BarWidth, Height), 3),
                      hp As GraphicsPath = PickerPaint.RoundRect(ThumbRect, 3)
                    g.FillPath(track, tp)
                    g.FillPath(thumb, hp)
                End Using
            End If
        End Sub

        Private Shared Sub DrawStrip(ByVal g As Graphics, ByVal p As ColorPalette, ByVal strip As RectangleF, ByVal scheme As PickerTheme)
            Dim count As Integer = CellCount(p)
            If count = 0 Then Return
            Using clip As GraphicsPath = PickerPaint.RoundRect(strip, 4)
                Dim old As Region = g.Clip
                g.SetClip(clip, CombineMode.Intersect)
                For i As Integer = 0 To p.Colors.Count - 1
                    Using b As New SolidBrush(p.Colors(i))
                        g.FillRectangle(b, CellRect(strip, i, count))
                    End Using
                Next
                g.Clip = old
            End Using
            If p.IsUserPalette Then
                Dim r As RectangleF = CellRect(strip, count - 1, count)
                r = New RectangleF(r.X + 2, r.Y + 1, r.Width - 4, r.Height - 2)
                Using dash As New Pen(scheme.EmptySlot) With {.DashStyle = DashStyle.Dash},
                      plus As New Pen(scheme.Text, 1.5F), path As GraphicsPath = PickerPaint.RoundRect(r, 4)
                    g.DrawPath(dash, path)
                    Dim cx As Single = r.X + r.Width / 2, cy As Single = r.Y + r.Height / 2
                    g.DrawLine(plus, cx - 5, cy, cx + 5, cy)
                    g.DrawLine(plus, cx, cy - 5, cx, cy + 5)
                End Using
            End If
        End Sub

#End Region

#Region "滑鼠"

        Protected Overrides Sub OnMouseWheel(ByVal e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            _offset -= Math.Sign(e.Delta) * (HeaderHeight + StripHeight + GroupGap)
            ClampOffset()
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()   ' 讓滾輪事件送到這裡
            If MaxOffset > 0 AndAlso e.X >= Width - BarWidth - 2 Then
                If e.Button = MouseButtons.Left Then
                    _barDragY = e.Y
                    _barDragOffset = _offset
                End If
                Return
            End If

            Dim y As Integer = -_offset
            For Each p As ColorPalette In _palettes
                Dim collapsed As Boolean = _collapsed.Contains(p)
                If e.Y >= y AndAlso e.Y < y + HeaderHeight Then
                    If e.Button = MouseButtons.Left Then
                        If collapsed Then _collapsed.Remove(p) Else _collapsed.Add(p)
                        ClampOffset()
                        Invalidate()
                    ElseIf e.Button = MouseButtons.Right AndAlso p.IsUserPalette Then
                        RaiseEvent ContextRequested(p, -1, e.Location)
                    End If
                    Return
                End If
                y += HeaderHeight
                If Not collapsed Then
                    Dim strip As New RectangleF(1, y, ContentWidth, StripHeight)
                    Dim count As Integer = CellCount(p)
                    For i As Integer = 0 To count - 1
                        If Not CellRect(strip, i, count).Contains(e.X, e.Y) Then Continue For
                        If i >= p.Colors.Count Then
                            If e.Button = MouseButtons.Left Then RaiseEvent AddColorRequested(Me, p)
                        ElseIf e.Button = MouseButtons.Left Then
                            RaiseEvent ColorPicked(Me, p.Colors(i))
                        ElseIf e.Button = MouseButtons.Right AndAlso p.IsUserPalette Then
                            RaiseEvent ContextRequested(p, i, e.Location)
                        End If
                        Return
                    Next
                    y += StripHeight
                End If
                y += GroupGap
            Next
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _barDragY < 0 OrElse e.Button <> MouseButtons.Left Then Return
            Dim track As Integer = Math.Max(1, Height - ThumbRect.Height)
            _offset = _barDragOffset + CInt((e.Y - _barDragY) * CDbl(MaxOffset) / track)
            ClampOffset()
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _barDragY = -1
        End Sub

        Protected Overrides Sub OnResize(ByVal e As EventArgs)
            MyBase.OnResize(e)
            ClampOffset()
        End Sub

#End Region

    End Class

    ''' <summary>輸入色票名稱的小對話框。取消回傳 Nothing。</summary>
    <System.ComponentModel.DesignerCategory("Code")>
    Friend NotInheritable Class NamePrompt
        Inherits Form
        Implements IThemeHost

        Private ReadOnly _box As New System.Windows.Forms.TextBox()
        Private ReadOnly _scheme As PickerTheme

        Private Sub New(ByVal title As String, ByVal caption As String, ByVal initial As String, ByVal scheme As PickerTheme)
            _scheme = scheme
            Text = title
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            StartPosition = FormStartPosition.CenterParent
            BackColor = scheme.Back
            ForeColor = scheme.Text
            Font = New Font("Microsoft JhengHei UI", 9.0F)
            ClientSize = New Size(300, 104)

            Dim lbl As New System.Windows.Forms.Label() With {.Text = caption, .AutoSize = True, .Location = New Point(12, 12)}
            _box.SetBounds(12, 34, 276, 24)
            _box.Text = initial
            _box.BorderStyle = BorderStyle.FixedSingle
            scheme.StyleInput(_box)
            Dim ok As New System.Windows.Forms.Button() With {.Text = "確定", .DialogResult = DialogResult.OK}
            Dim cancel As New System.Windows.Forms.Button() With {.Text = "取消", .DialogResult = DialogResult.Cancel}
            ok.SetBounds(124, 68, 78, 26)
            cancel.SetBounds(210, 68, 78, 26)
            scheme.StyleButton(ok)
            scheme.StyleButton(cancel)
            Controls.AddRange(New Control() {lbl, _box, ok, cancel})
            AcceptButton = ok
            CancelButton = cancel
        End Sub

        Private ReadOnly Property Scheme As PickerTheme Implements IThemeHost.Scheme
            Get
                Return _scheme
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(ByVal e As EventArgs)
            MyBase.OnHandleCreated(e)
            TitleBarTheme.Apply(Me, _scheme)
        End Sub

        ''' <summary>配色沿用 owner 所屬的 ColorPicker 主題。</summary>
        Public Shared Function Ask(ByVal owner As IWin32Window, ByVal title As String, ByVal caption As String, ByVal initial As String) As String
            Using f As New NamePrompt(title, caption, initial, PickerTheme.Of(TryCast(owner, Control)))
                If f.ShowDialog(owner) <> DialogResult.OK Then Return Nothing
                Dim n As String = f._box.Text.Trim()
                Return If(n.Length = 0, Nothing, n)
            End Using
        End Function

    End Class

End Namespace
