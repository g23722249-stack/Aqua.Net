Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.ListBox UserControl (Control\ListBox.ctl): a themed, scrollable list of
' rows, each row a check mark + icon + text + trailing "ext" icon. VB6 built each row as its own
' Aqua.ListItem UserControl (Control\ListItem.ctl); this project already has that exact same
' check+icon+text+exticon row rendering as Cell.vb (built for Grid.vb's cells), so rows here are
' real Cell controls -- one per item, positioned in a single vertical run and shown/hidden as the
' Aqua.ScrollBar overlay moves the visible window, the same non-virtualizing approach VB6 itself
' used (one real child control per item, Visible toggled by scroll position).
'
' Named ItemListBox, not ListBox: this project imports System.Windows.Forms everywhere, and a
' class named Aqua.ListBox would shadow System.Windows.Forms.ListBox throughout it. Same situation,
' same fix, as Button.ctl -> FlashButton.vb and Icon.ctl -> IconBox.vb earlier.
'
' Simplifications vs the VB6 original: ListItem's per-item LeftOfCheckPixels/LeftOfIconPixels/
' LeftOfTextPixels/LeftOfExtIconPixels manual offsets and the 18-step "GraduallySelection" gradient
' selection fill are not carried over -- Cell.vb (already shared with Grid) auto-layouts each row
' left-to-right and fills selection with a flat SelColor, which every other list/grid-style Aqua
' control in this port already uses. Keyboard selection keeps focus on the ItemListBox itself instead
' of moving it row-to-row (VB6 called picListItem(i).SetFocus per row); functionally equivalent,
' simpler, and consistent with how Buttons.vb/TabControl.vb already do keyboard selection here.
Namespace Global.Aqua

    <DefaultEvent("SelectedChanged")>
    Public Class ItemListBox
        Inherits UserControl

        Private Const BorderInset As Integer = 4    ' gc_intKeepBorderSize
        Private Const ScrollThickness As Integer = 18

        Private Shared ReadOnly DefaultSelColor As Color = System.Drawing.Color.FromArgb(8, 73, 198)   ' gc_lngListItemSelColor RGB(8,73,198)

        Private ReadOnly _items As New List(Of Cell)()
        Private ReadOnly _vscroll As New Aqua.ScrollBar()
        Private ReadOnly _hscroll As New Aqua.ScrollBar()

        Private _selectedIndex As Integer = -1
        Private _color As ColorConstants = ColorConstants.Blue
        Private _checkStyle As ItemCheckStyle = ItemCheckStyle.None
        Private _scrollMode As ScrollBarMode = ScrollBarMode.VerticalOnly
        Private _multiChecked As Boolean = True
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _soundEnterFocus As String = ""
        Private _soundExitFocus As String = ""
        Private _soundMouseEnter As String = ""
        Private _soundMouseLeave As String = ""
        Private _focused As Boolean = False
        Private _onePage As Integer = 1
        Private _cornerRect As Rectangle = Rectangle.Empty
        Private _inLayout As Boolean = False

        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ItemClick(sender As Object, index As Integer)
        Public Event Scroll(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event CheckStyleChanged(sender As Object, e As EventArgs)
        Public Event MultiCheckedChanged(sender As Object, e As EventArgs)
        Public Event ScrollModeChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = True
            MyBase.BackColor = SystemColors.Window

            _vscroll.Orientation = OrientationMode.Vertical
            _vscroll.Visible = False
            _vscroll.Enabled = True
            Controls.Add(_vscroll)

            _hscroll.Orientation = OrientationMode.Horizontal
            _hscroll.Visible = False
            _hscroll.Enabled = True
            Controls.Add(_hscroll)

            AddHandler _vscroll.Scroll, AddressOf OnVScroll
            AddHandler _hscroll.Scroll, AddressOf OnHScroll

            UpdateRegion()
            UpdateLayout()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(180, 160)
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Items (ports of AddItem/RemoveItem/Clear/Count/Item/CheckedCount)
        '=====================================================================
        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _items.Count
            End Get
        End Property

        Public ReadOnly Property Item(ByVal index As Integer) As Cell
            Get
                Return _items(index)
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property CheckedCount As Integer
            Get
                Dim n As Integer = 0
                For Each c In _items
                    If c.Checked Then n += 1
                Next
                Return n
            End Get
        End Property

        ''' <summary>Returns the newly added row so callers can set Icon/ExtIcon/Checked on it,
        ''' exactly like VB6's AddItem returning the new ListItem.</summary>
        Public Function AddItem(ByVal value As String, Optional ByVal text As String = "") As Cell
            Dim c As New Cell() With {
                .TabStop = False,
                .Font = Font,
                .BackColor = BackColor,
                .ForeColor = ForeColor,
                .SelColor = DefaultSelColor,
                .CheckStyle = _checkStyle,
                .ColorOfCheck = _color,
                .Enabled = Enabled,
                .Value = value,
                .Text = If(text, "")
            }
            WireCell(c)
            _items.Add(c)
            Controls.Add(c)
            UpdateLayout()
            Return c
        End Function

        Public Sub RemoveItem(ByVal index As Integer)
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            Dim c As Cell = _items(index)
            Controls.Remove(c)
            _items.RemoveAt(index)
            c.Dispose()
            If _selectedIndex >= _items.Count Then _selectedIndex = _items.Count - 1
            UpdateLayout()
        End Sub

        Public Sub Clear()
            For Each c In _items
                Controls.Remove(c)
                c.Dispose()
            Next
            _items.Clear()
            _selectedIndex = -1
            UpdateLayout()
        End Sub

        Private Sub WireCell(ByVal c As Cell)
            AddHandler c.Click, AddressOf OnCellClick
            AddHandler c.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler c.CheckedClick, AddressOf OnCellCheckedClick
            AddHandler c.KeyDown, Sub(sender, e) MyBase.OnKeyDown(CType(e, KeyEventArgs))
            AddHandler c.KeyPress, Sub(sender, e) MyBase.OnKeyPress(CType(e, KeyPressEventArgs))
            AddHandler c.KeyUp, Sub(sender, e) MyBase.OnKeyUp(CType(e, KeyEventArgs))
            AddHandler c.MouseDown, Sub(sender, e) MyBase.OnMouseDown(CType(e, MouseEventArgs))
            AddHandler c.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler c.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))
            AddHandler c.MouseEnter, AddressOf OnCellMouseEnter
            AddHandler c.MouseLeave, AddressOf OnCellMouseLeave
        End Sub

        Private Sub OnCellClick(sender As Object, e As EventArgs)
            Focus()
            Dim c As Cell = TryCast(sender, Cell)
            If c Is Nothing Then Return
            Dim idx As Integer = _items.IndexOf(c)
            If idx < 0 Then Return
            SelectRow(idx)
            RaiseEvent ItemClick(Me, idx)
            MyBase.OnClick(e)
        End Sub

        ''' <summary>Port of picListItem_CheckedChanged: clicking a row's check mark also selects
        ''' that row (through the public SelectedIndex setter, same as VB6).</summary>
        Private Sub OnCellCheckedClick(sender As Object, e As EventArgs)
            Dim c As Cell = TryCast(sender, Cell)
            If c Is Nothing Then Return
            Dim idx As Integer = _items.IndexOf(c)
            If idx < 0 Then Return
            SelectedIndex = idx
        End Sub

        Private Sub OnCellMouseEnter(sender As Object, e As EventArgs)
            SoundUtil.PlaySound(_soundMouseEnter)
            MyBase.OnMouseEnter(e)
        End Sub

        Private Sub OnCellMouseLeave(sender As Object, e As EventArgs)
            SoundUtil.PlaySound(_soundMouseLeave)
            MyBase.OnMouseLeave(e)
        End Sub

        '=====================================================================
        ' Selection
        '=====================================================================
        <Browsable(False)>
        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                SelectRow(value)
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedItem As Cell
            Get
                If _selectedIndex < 0 OrElse _selectedIndex >= _items.Count Then Return Nothing
                Return _items(_selectedIndex)
            End Get
        End Property

        ''' <summary>Read-only: which item is scrolled to the top (VB6: TopIndex).</summary>
        <Browsable(False)>
        Public ReadOnly Property TopIndex As Integer
            Get
                Return If(_vscroll.Enabled, _vscroll.Value - 1, 0)
            End Get
        End Property

        Private Sub SelectRow(ByVal index As Integer)
            _selectedIndex = index
            For i = 0 To _items.Count - 1
                _items(i).Selected = (i = index)
            Next
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        ''' <summary>Theme accent used for the scrollbars and each row's checkbox colour (VB6: Color).</summary>
        <Category("外觀")>
        <DefaultValue(ColorConstants.Blue)>
        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                _vscroll.Color = value
                _hscroll.Color = value
                For Each c In _items
                    c.ColorOfCheck = value
                Next
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ItemCheckStyle.None)>
        Public Property CheckStyle As ItemCheckStyle
            Get
                Return _checkStyle
            End Get
            Set(value As ItemCheckStyle)
                If _checkStyle = value Then Return
                _checkStyle = value
                For Each c In _items
                    c.CheckStyle = value
                Next
                UpdateLayout()
                RaiseEvent CheckStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(True)>
        Public Property MultiChecked As Boolean
            Get
                Return _multiChecked
            End Get
            Set(value As Boolean)
                If _multiChecked = value Then Return
                _multiChecked = value
                RaiseEvent MultiCheckedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ScrollBarMode.VerticalOnly)>
        Public Property ScrollMode As ScrollBarMode
            Get
                Return _scrollMode
            End Get
            Set(value As ScrollBarMode)
                If _scrollMode = value Then Return
                _scrollMode = value
                UpdateLayout()
                RaiseEvent ScrollModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ObtusenessMode.None)>
        Public Property Obtuseness As ObtusenessMode
            Get
                Return _obtuseness
            End Get
            Set(value As ObtusenessMode)
                If _obtuseness = value Then Return
                _obtuseness = value
                UpdateRegion()
                Invalidate()
                RaiseEvent ObtusenessChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                If _borderColor = value Then Return
                _borderColor = value
                Invalidate()
                RaiseEvent BorderColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property BorderFocusColor As Color
            Get
                Return _borderFocusColor
            End Get
            Set(value As Color)
                If _borderFocusColor = value Then Return
                _borderFocusColor = value
                Invalidate()
                RaiseEvent BorderFocusColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        Public Property SoundFileOfEnterFocus As String
            Get
                Return _soundEnterFocus
            End Get
            Set(value As String)
                _soundEnterFocus = value
            End Set
        End Property

        <Category("行為")>
        Public Property SoundFileOfExitFocus As String
            Get
                Return _soundExitFocus
            End Get
            Set(value As String)
                _soundExitFocus = value
            End Set
        End Property

        <Category("行為")>
        Public Property SoundFileOfMouseEnter As String
            Get
                Return _soundMouseEnter
            End Get
            Set(value As String)
                _soundMouseEnter = value
            End Set
        End Property

        <Category("行為")>
        Public Property SoundFileOfMouseLeave As String
            Get
                Return _soundMouseLeave
            End Get
            Set(value As String)
                _soundMouseLeave = value
            End Set
        End Property

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            For Each c In _items
                c.Font = Font
            Next
            UpdateLayout()
        End Sub

        Protected Overrides Sub OnBackColorChanged(e As EventArgs)
            MyBase.OnBackColorChanged(e)
            For Each c In _items
                c.BackColor = BackColor
            Next
            Invalidate()
        End Sub

        Protected Overrides Sub OnForeColorChanged(e As EventArgs)
            MyBase.OnForeColorChanged(e)
            For Each c In _items
                c.ForeColor = ForeColor
            Next
            Invalidate()
        End Sub

        ''' <summary>Unlike the VB6 original -- whose PaintUserControl unconditionally forced both
        ''' scrollbars' Enabled back to False on every resize/property repaint, leaving them stuck
        ''' disabled until the next AddItem/RemoveItem recomputed the real range -- this recomputes
        ''' the real Enabled state from the current item count/range instead of clobbering it.</summary>
        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            For Each c In _items
                c.Enabled = Enabled
            Next
            If Enabled Then
                UpdateLayout()
            Else
                _vscroll.Enabled = False
                _hscroll.Enabled = False
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnter(e As EventArgs)
            MyBase.OnEnter(e)
            _focused = True
            SoundUtil.PlaySound(_soundEnterFocus)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLeave(e As EventArgs)
            MyBase.OnLeave(e)
            _focused = False
            SoundUtil.PlaySound(_soundExitFocus)
            Invalidate()
        End Sub

        '=====================================================================
        ' Keyboard navigation (port of picListItem_KeyDown, minus per-row SetFocus -- see header)
        '=====================================================================
        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            Select Case e.KeyCode
                Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown
                    If _items.Count > 0 Then
                        Dim newIndex As Integer = Math.Max(0, _selectedIndex)
                        Select Case e.KeyCode
                            Case Keys.PageUp : newIndex = 0
                            Case Keys.PageDown : newIndex = _items.Count - 1
                            Case Keys.Up : newIndex = Math.Max(0, _selectedIndex - 1)
                            Case Keys.Down : newIndex = Math.Min(_items.Count - 1, _selectedIndex + 1)
                        End Select
                        If newIndex <> _selectedIndex Then
                            SelectedIndex = newIndex
                            EnsureVisible(newIndex)
                        End If
                    End If
                    e.Handled = True
                Case Keys.Left
                    If _hscroll.Visible AndAlso _hscroll.Value > _hscroll.Minimum Then _hscroll.Value -= 1
                    e.Handled = True
                Case Keys.Right
                    If _hscroll.Visible AndAlso _hscroll.Value < _hscroll.Maximum Then _hscroll.Value += 1
                    e.Handled = True
            End Select
            MyBase.OnKeyDown(e)
        End Sub

        Private Sub EnsureVisible(ByVal index As Integer)
            If Not _vscroll.Visible Then Return
            If index < TopIndex Then
                _vscroll.Value = index + 1
            ElseIf index > TopIndex + _onePage - 1 Then
                _vscroll.Value = Math.Max(_vscroll.Minimum, index - _onePage + 2)
            End If
            UpdateLayout()
        End Sub

        '=====================================================================
        ' Layout / scrolling (ports of SetScrollBarProperty / ResetUserControlPosition / VScrollBar1_Scroll)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            UpdateLayout()
        End Sub

        Private Sub OnVScroll(sender As Object, e As EventArgs)
            UpdateLayout()
            RaiseEvent Scroll(Me, EventArgs.Empty)
        End Sub

        Private Sub OnHScroll(sender As Object, e As EventArgs)
            UpdateLayout()
            RaiseEvent Scroll(Me, EventArgs.Empty)
        End Sub

        Private Function RowHeight() As Integer
            Dim h As Integer = TextRenderer.MeasureText("Ag", Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                                         TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Height + 4
            Return Math.Max(1, h)
        End Function

        ''' <summary>Approximates a row's natural content width (check + icon + text + ext-icon) for
        ''' horizontal-scroll range purposes -- Cell.vb lays its content out to whatever Width it's
        ''' given rather than exposing a "preferred width", so this mirrors that same left-to-right
        ''' math independently rather than reaching into Cell's private layout.</summary>
        Private Function MeasureRowWidth(ByVal c As Cell) As Integer
            Dim w As Integer = 4
            Dim checkImg As Image = CheckMarkResources.GetCheckSurface(c.CheckStyle, c.ColorOfCheck, c.Checked, c.Enabled)
            If checkImg IsNot Nothing Then w += checkImg.Width + 4
            If c.Icon IsNot Nothing Then w += c.Icon.Width + 4
            If Not String.IsNullOrEmpty(c.Text) Then
                w += TextRenderer.MeasureText(c.Text, Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                               TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Width
            End If
            If c.ExtIcon IsNot Nothing Then w += c.ExtIcon.Width + 8
            Return w
        End Function

        Private Sub UpdateLayout()
            If _inLayout Then Return
            _inLayout = True
            Try
                Dim rh As Integer = RowHeight()
                Dim inner As New Rectangle(BorderInset, BorderInset, Math.Max(0, Width - BorderInset * 2), Math.Max(0, Height - BorderInset * 2))

                Dim maxContentWidth As Integer = 0
                For Each c In _items
                    maxContentWidth = Math.Max(maxContentWidth, MeasureRowWidth(c))
                Next

                Dim needV As Boolean = _items.Count * rh > inner.Height
                Dim needH As Boolean = (_scrollMode = ScrollBarMode.Both) AndAlso maxContentWidth > (inner.Width - If(needV, ScrollThickness, 0))
                If needH Then needV = _items.Count * rh > (inner.Height - ScrollThickness)

                Dim contentH As Integer = inner.Height - If(needH, ScrollThickness, 0)
                Dim contentW As Integer = inner.Width - If(needV, ScrollThickness, 0)

                _vscroll.Visible = needV
                If needV Then
                    _vscroll.SetBounds(inner.Right - ScrollThickness, inner.Top, ScrollThickness, contentH)
                    _vscroll.Minimum = 1
                    _onePage = Math.Max(1, contentH \ rh)
                    Dim maxV As Integer = Math.Max(1, _items.Count - _onePage + 1)
                    _vscroll.Maximum = maxV
                    _vscroll.Enabled = maxV > 1
                    Select Case maxV
                        Case 1 To 10 : _vscroll.LargeChange = 3
                        Case 11 To 30 : _vscroll.LargeChange = 5
                        Case Else : _vscroll.LargeChange = 8
                    End Select
                    If _vscroll.Value > _vscroll.Maximum Then _vscroll.Value = _vscroll.Maximum
                Else
                    _onePage = Math.Max(1, contentH \ rh)
                    _vscroll.Value = _vscroll.Minimum
                End If

                _hscroll.Visible = needH
                If needH Then
                    _hscroll.SetBounds(inner.Left, inner.Bottom - ScrollThickness, contentW, ScrollThickness)
                    _hscroll.Minimum = 1
                    Dim charStep As Integer = Math.Max(1, TextRenderer.MeasureText("A", Font).Width)
                    Dim maxOffset As Integer = Math.Max(0, maxContentWidth - contentW)
                    Dim maxH As Integer = Math.Max(1, CInt(Math.Ceiling(maxOffset / CDbl(charStep))) + 1)
                    _hscroll.Maximum = maxH
                    _hscroll.Enabled = maxH > 1
                    If _hscroll.Value > _hscroll.Maximum Then _hscroll.Value = _hscroll.Maximum
                Else
                    _hscroll.Value = _hscroll.Minimum
                End If

                _cornerRect = If(needV AndAlso needH,
                                 New Rectangle(inner.Right - ScrollThickness, inner.Bottom - ScrollThickness, ScrollThickness, ScrollThickness),
                                 Rectangle.Empty)

                Dim topIndex As Integer = If(needV, _vscroll.Value - 1, 0)
                Dim step2 As Integer = Math.Max(1, TextRenderer.MeasureText("A", Font).Width)
                Dim hOffset As Integer = If(needH, (_hscroll.Value - 1) * step2, 0)
                Dim rowWidth As Integer = Math.Max(contentW, maxContentWidth)

                For i = 0 To _items.Count - 1
                    Dim c As Cell = _items(i)
                    Dim rel As Integer = i - topIndex
                    If rel >= 0 AndAlso rel * rh < contentH Then
                        c.Fixed = True
                        c.SetBounds(inner.Left - hOffset, inner.Top + rel * rh, rowWidth, rh)
                        c.Fixed = False
                        c.Visible = True
                    Else
                        c.Visible = False
                    End If
                Next

                If needV OrElse needH Then BringScrollBarsToFront()
                Invalidate()
            Finally
                _inLayout = False
            End Try
        End Sub

        Private Sub BringScrollBarsToFront()
            _hscroll.BringToFront()
            _vscroll.BringToFront()
        End Sub

        '=====================================================================
        ' Region / painting
        '=====================================================================
        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(_obtuseness = ObtusenessMode.None OrElse Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(_obtuseness, Width, Height))
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Not _cornerRect.IsEmpty Then
                Dim mark As Image = CheckMarkResources.GetPicMark()
                If mark IsNot Nothing Then
                    e.Graphics.DrawImage(mark, _cornerRect)
                Else
                    Using b As New SolidBrush(SystemColors.Control)
                        e.Graphics.FillRectangle(b, _cornerRect)
                    End Using
                End If
            End If
            BorderPainter.DrawThemedBorder(e.Graphics, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
