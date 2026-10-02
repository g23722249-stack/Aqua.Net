Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.Grid UserControl (Control\Grid.ctl): the data grid
' that assembles the Headers bar, a display-culled set of CellRow controls and two Aqua
' ScrollBars inside a themed border. Data is entered through Cell(col, row); rows are
' created on demand (one CellRow control per row, as in VB6) and only the on-view rows
' are positioned/shown when scrolling.
Namespace Global.Aqua

    <DefaultEvent("ItemClick")>
    Public Class Grid
        Inherits Control
        Implements IMessageFilter

        Private Const KeepBorder As Integer = 4      ' gc_intKeepBorderSize
        Private Const ScrollThickness As Integer = 18

        Private ReadOnly _headers As New Headers()
        Private _columns As GridColumnCollection
        Private ReadOnly _vscroll As New ScrollBar()
        Private ReadOnly _hscroll As New ScrollBar()
        Private ReadOnly _rows As New List(Of CellRow)()

        Private _count As Integer = 0
        Private _onePage As Integer = 1
        Private _selIndex As Integer = -1
        Private _col As Integer = -1
        Private _row As Integer = -1
        Private _selMode As GridSelectionMode = GridSelectionMode.Row
        Private _color As ColorConstants = ColorConstants.Blue
        Private _active As Boolean = True
        Private _focus As Boolean = False
        Private _parhelia As Boolean = False
        Private _updating As Integer = 0
        Private _hOffset As Integer = 0
        Private _hStep As Integer = 16
        Private _inLayout As Boolean = False

        Private _borderColor As Color = GridConst.GridBorderLineColor
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _oddBackColor As Color = GridConst.GridOddBackColor
        Private _evenBackColor As Color = System.Drawing.Color.White
        Private _selColor As Color = GridConst.GridSelColor

        ' --- events (subset of the VB6 Grid surface, most-used first) ---
        Public Event HeaderClick(sender As Object, index As Integer)
        Public Event ItemClick(col As Integer, row As Integer)
        Public Event ItemDblClick(col As Integer, row As Integer)
        Public Event ItemCheckedClick(col As Integer, row As Integer)
        Public Event ItemIconClick(col As Integer, row As Integer)
        Public Event ItemExtIconClick(col As Integer, row As Integer)
        Public Event ItemCheckedChanged(col As Integer, row As Integer)
        Public Event ItemTextChanged(col As Integer, row As Integer)
        Public Event ItemValueChanged(col As Integer, row As Integer)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event BeforeSort(index As Integer, ByRef cancel As Boolean)
        Public Event AfterSort(index As Integer)
        Public Event Scroll(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = System.Drawing.Color.White
            MyBase.ForeColor = System.Drawing.Color.Black

            Controls.Add(_headers)
            Controls.Add(_vscroll)
            Controls.Add(_hscroll)
            _vscroll.Orientation = OrientationMode.Vertical
            _hscroll.Orientation = OrientationMode.Horizontal
            _vscroll.Color = _color
            _hscroll.Color = _color

            AddHandler _headers.HeaderClick, Sub(s, i) RaiseEvent HeaderClick(Me, i)
            AddHandler _headers.SortRequested, AddressOf OnHeaderSort
            AddHandler _headers.BeforeSort, Sub(i As Integer, ByRef c As Boolean) RaiseEvent BeforeSort(i, c)
            AddHandler _headers.AfterSort, Sub(i) RaiseEvent AfterSort(i)
            AddHandler _headers.ItemSplitComplete, AddressOf OnHeaderSplitComplete
            AddHandler _vscroll.Scroll, Sub(s, e) ScrollRows()
            AddHandler _hscroll.Scroll, Sub(s, e) ScrollHorizontally()
        End Sub

        '=====================================================================
        ' Flicker-free scrolling + mouse-wheel over the whole grid
        '=====================================================================
        ''' <summary>WS_EX_COMPOSITED double-buffers the entire control (incl. child rows),
        ''' so moving many CellRows while scrolling no longer flickers.</summary>
        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const WS_EX_COMPOSITED As Integer = &H2000000
                Dim cp As CreateParams = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or WS_EX_COMPOSITED
                Return cp
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            Application.AddMessageFilter(Me)
        End Sub

        Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
            Application.RemoveMessageFilter(Me)
            MyBase.OnHandleDestroyed(e)
        End Sub

        ''' <summary>Route the wheel to the grid whenever the cursor is over it, regardless of focus.</summary>
        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            Const WM_MOUSEWHEEL As Integer = &H20A
            If m.Msg <> WM_MOUSEWHEEL Then Return False
            If Not IsHandleCreated OrElse Not Visible OrElse Not _vscroll.Enabled Then Return False
            If Not RectangleToScreen(ClientRectangle).Contains(Cursor.Position) Then Return False

            Dim hw As Integer = CInt((m.WParam.ToInt64() >> 16) And &HFFFF)
            If hw >= &H8000 Then hw -= &H10000   ' sign-extend the 16-bit wheel delta
            Dim notches As Integer = hw \ 120
            If notches = 0 Then Return True
            Dim step_ As Integer = 3
            Dim nv As Integer = _vscroll.Value - notches * step_   ' wheel up (delta>0) scrolls up
            If nv < _vscroll.Minimum Then nv = _vscroll.Minimum
            If nv > _vscroll.Maximum Then nv = _vscroll.Maximum
            _vscroll.Value = nv
            Return True
        End Function

        '=====================================================================
        ' Public API
        '=====================================================================
        ''' <summary>The column headers, editable in the designer. Changing the list rebuilds the header bar
        ''' (Header.Clear + AdditionHeader per column + SetHeaderProperty).</summary>
        <Category("行為"), Description("欄位標題（文字、寬度、對齊、排序、圖示）。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        <Editor(GetType(System.ComponentModel.Design.CollectionEditor), GetType(System.Drawing.Design.UITypeEditor))>
        Public ReadOnly Property Columns As GridColumnCollection
            Get
                If _columns Is Nothing Then _columns = New GridColumnCollection(Me)
                Return _columns
            End Get
        End Property

        Friend Sub ApplyColumns()
            _headers.Clear()
            For Each c As GridColumn In Columns
                _headers.AdditionHeader(c.Icon, c.Text, c.Alignment, c.Width, c.Sort, c.SortOrder)
            Next
            SetHeaderProperty()
        End Sub

        ''' <summary>The column-header bar (VB6 Grid.Header). Columns can also be added from code via its
        ''' AdditionHeader (Columns is the designer's way).</summary>
        <Browsable(False)>
        Public ReadOnly Property Header As Aqua.Headers
            Get
                Return _headers
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property Col As Integer
            Get
                Return _col
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property Row As Integer
            Get
                Return _row
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property RowCount As Integer
            Get
                Return _count
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property ColCount As Integer
            Get
                Return _headers.Count
            End Get
        End Property

        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                _vscroll.Color = value
                _hscroll.Color = value
                _headers.Color = value
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property SelectionMode As GridSelectionMode
            Get
                Return _selMode
            End Get
            Set(value As GridSelectionMode)
                _selMode = value
            End Set
        End Property

        <Browsable(False)>
        Public Property SelColor As Color
            Get
                Return _selColor
            End Get
            Set(value As Color)
                _selColor = value
                For Each r In _rows
                    r.SelColor = value
                Next
            End Set
        End Property

        <Browsable(False)>
        Public Property OddBackColor As Color
            Get
                Return _oddBackColor
            End Get
            Set(value As Color)
                _oddBackColor = value
                RecolorRows()
            End Set
        End Property

        <Browsable(False)>
        Public Property EvenBackColor As Color
            Get
                Return _evenBackColor
            End Get
            Set(value As Color)
                _evenBackColor = value
                RecolorRows()
            End Set
        End Property

        <Browsable(False)>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                _borderColor = value
                Invalidate()
            End Set
        End Property

        <Browsable(False)>
        Public Property ActiveControl As Boolean
            Get
                Return _active
            End Get
            Set(value As Boolean)
                _active = value
                _vscroll.ActiveControl = value
                _hscroll.ActiveControl = value
            End Set
        End Property

        ''' <summary>When True, a focus halo (parhelia) is drawn inside the border while focused.</summary>
        <DefaultValue(False)>
        Public Property Parhelia As Boolean
            Get
                Return _parhelia
            End Get
            Set(value As Boolean)
                If _parhelia = value Then Return
                _parhelia = value
                Invalidate()
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedIndex As Integer
            Get
                Return _selIndex
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedItem As CellRow
            Get
                If _selIndex >= 0 AndAlso _selIndex < _rows.Count Then Return _rows(_selIndex)
                Return Nothing
            End Get
        End Property

        <Browsable(False)>
        Public Property TopIndex As Integer
            Get
                If Not _vscroll.Enabled Then Return 0
                Return _vscroll.Value - 1
            End Get
            Set(value As Integer)
                If Not _vscroll.Enabled OrElse value < 0 OrElse value >= _vscroll.Maximum Then Return
                _vscroll.Value = value + 1
            End Set
        End Property

        ''' <summary>The CellRow for a data row (VB6 Grid.Item).</summary>
        Public ReadOnly Property Item(ByVal row As Integer) As CellRow
            Get
                If row < 0 OrElse row >= _count Then Throw New ArgumentOutOfRangeException(NameOf(row))
                Return _rows(row)
            End Get
        End Property

        ''' <summary>Get (creating rows as needed) the cell at column/row (VB6 Grid.Cell).</summary>
        Public ReadOnly Property Cell(ByVal col As Integer, ByVal row As Integer) As Cell
            Get
                If col > _headers.Count - 1 OrElse col < 0 Then Throw New ArgumentOutOfRangeException(NameOf(col))
                EnsureRow(row)
                If row + 1 > _count Then _count = row + 1
                If _updating = 0 Then
                    UpdateScrollBars()
                    ScrollRows()
                End If
                Return _rows(row).Item(col)
            End Get
        End Property

        ''' <summary>Rebuild the header layout after columns were added (VB6 SetHeaderProperty).</summary>
        Public Sub SetHeaderProperty()
            _headers.SetHeaderProperty()
            Relayout()
        End Sub

        Public Sub Clear()
            For Each r In _rows
                Controls.Remove(r)
                r.Dispose()
            Next
            _rows.Clear()
            _count = 0
            _selIndex = -1
            _col = -1
            _row = -1
            UpdateScrollBars()
            Invalidate()
        End Sub

        Public Shadows Sub Refresh()
            Relayout()
            MyBase.Refresh()
        End Sub

        '=====================================================================
        ' Row creation / colouring
        '=====================================================================
        Private Sub EnsureRow(ByVal row As Integer)
            While _rows.Count <= row
                Dim r As New CellRow()
                r.Index = _rows.Count
                r.SelColor = _selColor
                r.Font = Font
                r.ForeColor = ForeColor
                Controls.Add(r)
                r.Build(_headers)
                _rows.Add(r)
                WireRow(r)
                ColorRow(r)
            End While
            BringChromeToFront()   ' keep scrollbars above the newly added rows
        End Sub

        Private Sub ColorRow(ByVal r As CellRow)
            r.BackColor = If((r.Index + 1) Mod 2 = 0, _evenBackColor, _oddBackColor)
        End Sub

        Private Sub RecolorRows()
            For Each r In _rows
                ColorRow(r)
            Next
        End Sub

        '=====================================================================
        ' Layout
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Relayout()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            _headers.Font = Font
            For Each r In _rows
                r.Font = Font
            Next
            Relayout()
        End Sub

        Private Function RowHeight() As Integer
            Dim th As Integer = TextRenderer.MeasureText("Ag", Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                                         TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Height
            Return th + 4
        End Function

        Private Sub Relayout()
            If _inLayout Then Return
            If Width <= 0 OrElse Height <= 0 Then Return
            _inLayout = True
            Try
                Dim inner As New Rectangle(KeepBorder, KeepBorder, Width - KeepBorder * 2, Height - KeepBorder * 2)
                Dim headerH As Integer = RowHeight()

                ' header spans the full inner width; the last column stretches to fill
                _headers.SetBounds(inner.Left, inner.Top, inner.Width, headerH)

                Dim contentW As Integer = _headers.Width               ' natural width after stretch
                Dim needH As Boolean = contentW > inner.Width
                Dim rowsTop As Integer = inner.Top + headerH
                Dim hH As Integer = If(needH, ScrollThickness, 0)
                Dim rowsAreaH As Integer = inner.Bottom - rowsTop - hH
                Dim rowsAreaW As Integer = inner.Width - ScrollThickness

                _vscroll.SetBounds(inner.Right - ScrollThickness, rowsTop, ScrollThickness, rowsAreaH)
                _hscroll.Visible = needH
                If needH Then
                    _hscroll.SetBounds(inner.Left, inner.Bottom - hH, rowsAreaW, hH)
                End If

                Dim rh As Integer = RowHeight()
                _onePage = Math.Max(1, rowsAreaH \ rh)

                ' horizontal scroll range
                Dim maxOffset As Integer = Math.Max(0, contentW - rowsAreaW)
                If needH Then
                    _hscroll.Minimum = 1
                    _hscroll.Maximum = Math.Max(1, CInt(Math.Ceiling(maxOffset / CDbl(_hStep))) + 1)
                    _hscroll.Enabled = _hscroll.Maximum > _hscroll.Minimum
                End If

                UpdateScrollBars()
                ScrollRows()
                BringChromeToFront()
            Finally
                _inLayout = False
            End Try
            Invalidate()
        End Sub

        ''' <summary>
        ''' Keep the header bar and scrollbars in front of the row controls. Rows are added
        ''' to Controls after the chrome, so in WinForms z-order they would otherwise sit on
        ''' top of (and hide) the scrollbars, since the last column stretches full width.
        ''' </summary>
        Private Sub BringChromeToFront()
            _headers.BringToFront()
            _hscroll.BringToFront()
            _vscroll.BringToFront()   ' called last => front-most
        End Sub

        Private Sub UpdateScrollBars()
            _vscroll.Minimum = 1
            Dim maxV As Integer = _count - _onePage + 1
            If maxV < 1 Then maxV = 1
            _vscroll.Maximum = maxV
            _vscroll.Enabled = _vscroll.Maximum > _vscroll.Minimum
            Select Case _vscroll.Maximum
                Case 1 To 10 : _vscroll.LargeChange = 3
                Case 20 To 30 : _vscroll.LargeChange = 5
                Case Else : _vscroll.LargeChange = 8
            End Select
        End Sub

        ''' <summary>Position/cull rows for the current vertical scroll value (port of VScrollBar1_Scroll).</summary>
        Private Sub ScrollRows()
            If _rows.Count = 0 Then Return
            Dim inner As New Rectangle(KeepBorder, KeepBorder, Width - KeepBorder * 2, Height - KeepBorder * 2)
            Dim rowsTop As Integer = inner.Top + _headers.Height
            Dim rh As Integer = RowHeight()
            Dim top As Integer = If(_vscroll.Enabled, _vscroll.Value - 1, 0)

            For i = 0 To _rows.Count - 1
                Dim r As CellRow = _rows(i)
                If i >= _count Then
                    r.Visible = False
                    Continue For
                End If
                Dim rel As Integer = i - top
                If rel >= 0 AndAlso rel <= _onePage Then
                    r.Left = inner.Left - _hOffset
                    r.Top = rowsTop + rel * rh
                    r.Visible = True
                Else
                    r.Visible = False
                End If
            Next
            RaiseEvent Scroll(Me, EventArgs.Empty)
        End Sub

        Private Sub ScrollHorizontally()
            Dim rowsAreaW As Integer = Width - KeepBorder * 2 - ScrollThickness
            Dim maxOffset As Integer = Math.Max(0, _headers.Width - rowsAreaW)
            _hOffset = Math.Min(maxOffset, (_hscroll.Value - 1) * _hStep)
            Dim inner As New Rectangle(KeepBorder, KeepBorder, Width - KeepBorder * 2, Height - KeepBorder * 2)
            _headers.Left = inner.Left - _hOffset
            ScrollRows()
        End Sub

        '=====================================================================
        ' Row event wiring (selection + bubbling)
        '=====================================================================
        Private Sub WireRow(ByVal r As CellRow)
            AddHandler r.ItemClick, AddressOf OnRowItemClick
            AddHandler r.ItemDblClick, Sub(idx, c) RaiseEvent ItemDblClick(c, idx)
            AddHandler r.ItemCheckedClick, Sub(idx, c) RaiseEvent ItemCheckedClick(c, idx)
            AddHandler r.ItemIconClick, Sub(idx, c) RaiseEvent ItemIconClick(c, idx)
            AddHandler r.ItemExtIconClick, Sub(idx, c) RaiseEvent ItemExtIconClick(c, idx)
            AddHandler r.ItemCheckedChanged, Sub(idx, c) RaiseEvent ItemCheckedChanged(c, idx)
            AddHandler r.ItemTextChanged, Sub(idx, c) RaiseEvent ItemTextChanged(c, idx)
            AddHandler r.ItemValueChanged, Sub(idx, c) RaiseEvent ItemValueChanged(c, idx)
            AddHandler r.ItemKeyDown, AddressOf OnRowItemKeyDown
        End Sub

        Private Sub OnRowItemClick(ByVal index As Integer, ByVal col As Integer)
            If _count <= 0 OrElse index >= _count Then Return
            Select Case _selMode
                Case GridSelectionMode.Row
                    If _selIndex >= 0 AndAlso _selIndex < _rows.Count Then
                        _rows(_selIndex).Selected = False
                        _rows(_selIndex).SelColor = _selColor
                    End If
                    _selIndex = index
                    _rows(_selIndex).Selected = True
                Case GridSelectionMode.Cell
                    If _selIndex >= 0 AndAlso _selIndex < _rows.Count Then
                        For i = 0 To _rows(_selIndex).Count - 1
                            _rows(_selIndex).Item(i).Selected = False
                            _rows(_selIndex).Item(i).SelColor = _selColor
                        Next
                    End If
                    _selIndex = index
                    _rows(_selIndex).Item(col).Selected = True
            End Select
            _col = col : _row = index
            RaiseEvent ItemClick(col, index)
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        ''' <summary>Keyboard navigation (port of crItem_ItemKeyDown, Row/Cell modes).</summary>
        Private Sub OnRowItemKeyDown(ByVal index As Integer, ByVal col As Integer, ByVal keyCode As Integer, ByVal shift As Integer)
            Select Case _selMode
                Case GridSelectionMode.Row
                    Select Case keyCode
                        Case Keys.Up
                            If index > 0 Then SelectRow(index - 1, col)
                        Case Keys.Down
                            If index < _count - 1 Then SelectRow(index + 1, col)
                        Case Keys.PageUp
                            SelectRow(0, col) : _vscroll.Value = _vscroll.Minimum
                        Case Keys.PageDown
                            SelectRow(_count - 1, col) : _vscroll.Value = _vscroll.Maximum
                    End Select
                Case GridSelectionMode.Cell
                    Select Case keyCode
                        Case Keys.Up
                            If index > 0 Then SelectCell(index - 1, col)
                        Case Keys.Down
                            If index < _count - 1 Then SelectCell(index + 1, col)
                        Case Keys.Left
                            If col > 0 Then SelectCell(index, col - 1)
                        Case Keys.Right
                            If col < _headers.Count - 1 Then SelectCell(index, col + 1)
                    End Select
            End Select
        End Sub

        Private Sub SelectRow(ByVal newRow As Integer, ByVal col As Integer)
            If _selIndex >= 0 AndAlso _selIndex < _rows.Count Then
                _rows(_selIndex).Selected = False
                _rows(_selIndex).SelColor = _selColor
            End If
            _rows(newRow).Selected = True
            _rows(newRow).Focus()
            _col = col : _row = newRow : _selIndex = newRow
            EnsureVisible(newRow)
        End Sub

        Private Sub SelectCell(ByVal newRow As Integer, ByVal newCol As Integer)
            If _selIndex >= 0 AndAlso _selIndex < _rows.Count AndAlso _col >= 0 Then
                _rows(_selIndex).Item(_col).Selected = False
                _rows(_selIndex).Item(_col).SelColor = _selColor
            End If
            _rows(newRow).Item(newCol).Selected = True
            _rows(newRow).SetCellFocus(newCol)
            _col = newCol : _row = newRow : _selIndex = newRow
            EnsureVisible(newRow)
        End Sub

        Private Sub EnsureVisible(ByVal row As Integer)
            If Not _vscroll.Enabled Then Return
            Dim top As Integer = _vscroll.Value - 1
            If row < top Then
                _vscroll.Value = row + 1
            ElseIf row > top + _onePage - 1 Then
                _vscroll.Value = Math.Min(_vscroll.Maximum, row - _onePage + 2)
            End If
        End Sub

        '=====================================================================
        ' Sorting (port of Headers1_Sort + SortingGrid, ADO replaced by managed sort)
        '=====================================================================
        Private Sub OnHeaderSort(ByVal index As Integer, ByVal order As SortOrder)
            If _count <= 0 Then Return

            Dim orderIdx As Integer() = Enumerable_Range(0, _count)
            Array.Sort(orderIdx, Function(a, b)
                                     Dim ta As String = _rows(a).Item(index).Text
                                     Dim tb As String = _rows(b).Item(index).Text
                                     Dim cmp As Integer = String.Compare(ta, tb, StringComparison.CurrentCulture)
                                     Return If(order = SortOrder.Descending, -cmp, cmp)
                                 End Function)

            ' snapshot cell state in the new order, then write back into rows 0..count-1
            Dim snap(_count - 1)() As CellSnapshot
            For newRow = 0 To _count - 1
                Dim src As CellRow = _rows(orderIdx(newRow))
                Dim cols(src.Count - 1) As CellSnapshot
                For c = 0 To src.Count - 1
                    cols(c) = CellSnapshot.Capture(src.Item(c))
                Next
                snap(newRow) = cols
            Next
            For newRow = 0 To _count - 1
                Dim dst As CellRow = _rows(newRow)
                For c = 0 To dst.Count - 1
                    snap(newRow)(c).ApplyTo(dst.Item(c))
                Next
            Next

            ScrollRows()
        End Sub

        Private Shared Function Enumerable_Range(ByVal start As Integer, ByVal count As Integer) As Integer()
            Dim a(count - 1) As Integer
            For i = 0 To count - 1
                a(i) = start + i
            Next
            Return a
        End Function

        Private Structure CellSnapshot
            Public Text As String
            Public Value As String
            Public Fore As Color
            Public Sel As Color
            Public Align As AlignmentConstants
            Public CheckStyle As ItemCheckStyle
            Public ColorOfCheck As ColorConstants
            Public Checked As Boolean
            Public Selected As Boolean
            Public Enabled As Boolean
            Public Icon As Image
            Public ExtIcon As Image

            Public Shared Function Capture(ByVal c As Cell) As CellSnapshot
                Dim s As CellSnapshot
                s.Text = c.Text : s.Value = c.Value : s.Fore = c.ForeColor : s.Sel = c.SelColor
                s.Align = c.Alignment : s.CheckStyle = c.CheckStyle : s.ColorOfCheck = c.ColorOfCheck
                s.Checked = c.Checked : s.Selected = c.Selected : s.Enabled = c.Enabled
                s.Icon = c.Icon : s.ExtIcon = c.ExtIcon
                Return s
            End Function

            Public Sub ApplyTo(ByVal c As Cell)
                c.Fixed = True
                c.Text = Text : c.Value = Value : c.ForeColor = Fore : c.SelColor = Sel
                c.Alignment = Align : c.CheckStyle = CheckStyle : c.ColorOfCheck = ColorOfCheck
                c.Checked = Checked : c.Selected = Selected : c.Enabled = Enabled
                c.Icon = Icon : c.ExtIcon = ExtIcon
                c.Fixed = False
                c.Refresh()
            End Sub
        End Structure

        Private Sub OnHeaderSplitComplete(ByVal index As Integer, ByVal left1 As Integer, ByVal width1 As Integer, ByVal left2 As Integer, ByVal width2 As Integer)
            For Each r In _rows
                r.Split(index, left1, width1, left2, width2)
            Next
            Relayout()
        End Sub

        '=====================================================================
        ' Focus + border painting (port of DrawControlBorder)
        '=====================================================================
        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            _focus = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _focus = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            MyBase.OnPaintBackground(e)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim lineColor As Color = If(_focus, _borderFocusColor, _borderColor)

            ' main 3px themed border
            Using p As New Pen(lineColor, 3)
                g.DrawRectangle(p, 1, 1, Width - 3, Height - 3)
            End Using

            ' outer lighter edge (port of the RGB-shifted secondary line)
            Dim outer As Color = If(_focus, ColorUtil.ShiftChannels(lineColor, 10),
                                             ColorUtil.ShiftChannels(lineColor, 123, 122, 123))
            Using p As New Pen(outer, 1)
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1)
            End Using

            ' top highlight
            Dim topHi As Color = If(_focus, ColorUtil.ShiftChannels(lineColor, -14),
                                             ColorUtil.ShiftChannels(lineColor, 46, 44, 46))
            Using p As New Pen(topHi, 1)
                g.DrawLine(p, 0, 0, Width - 1, 0)
            End Using

            ' parhelia: a soft focus halo just inside the border (port of DrawControlParhelia)
            If _parhelia AndAlso _focus Then
                Dim glow As Color = _borderFocusColor
                For k As Integer = 0 To 3
                    Dim alpha As Integer = 90 - k * 22
                    If alpha < 0 Then alpha = 0
                    Using p As New Pen(System.Drawing.Color.FromArgb(alpha, glow), 1)
                        Dim inset As Integer = 3 + k
                        g.DrawRectangle(p, inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2)
                    End Using
                Next
            End If
        End Sub

        '=====================================================================
        ' Batch fill / data binding (additions beyond the VB6 API)
        '=====================================================================
        ''' <summary>Suspend layout/scroll recomputation for fast bulk population.</summary>
        Public Sub BeginUpdate()
            _updating += 1
            SuspendLayout()
        End Sub

        ''' <summary>Resume after BeginUpdate and do a single relayout.</summary>
        Public Sub EndUpdate()
            If _updating > 0 Then _updating -= 1
            ResumeLayout(False)
            If _updating = 0 Then
                UpdateScrollBars()
                Relayout()
            End If
        End Sub

        ''' <summary>Append a row with the given column texts; returns the new row index.</summary>
        Public Function AddRow(ParamArray texts As String()) As Integer
            Dim r As Integer = _count
            Dim n As Integer = Math.Min(If(texts, New String() {}).Length, _headers.Count)
            For c = 0 To _headers.Count - 1
                Dim cell As Cell = Me.Cell(c, r)
                If c < n Then cell.Text = texts(c)
            Next
            Return r
        End Function

        ''' <summary>Replace all data from a 2-D string array [rows, cols].</summary>
        Public Sub LoadStrings(ByVal data As String(,))
            BeginUpdate()
            Try
                Clear()
                If data Is Nothing Then Return
                Dim rows As Integer = data.GetLength(0)
                Dim cols As Integer = Math.Min(data.GetLength(1), _headers.Count)
                For rIdx = 0 To rows - 1
                    For cIdx = 0 To cols - 1
                        Me.Cell(cIdx, rIdx).Text = If(data(rIdx, cIdx), "")
                    Next
                Next
            Finally
                EndUpdate()
            End Try
        End Sub

        ''' <summary>
        ''' Bind to a DataTable: its columns become headers (if none are defined yet) and its
        ''' rows fill the grid. Existing rows are cleared. This is data as read-only display.
        ''' </summary>
        Public Sub Bind(ByVal table As System.Data.DataTable)
            BeginUpdate()
            Try
                Clear()
                If table Is Nothing Then Return
                If _headers.Count = 0 Then
                    For Each dc As System.Data.DataColumn In table.Columns
                        _headers.AdditionHeader(Nothing, dc.Caption, AlignmentConstants.LeftJustify, 120, True, SortOrder.None)
                    Next
                    _headers.SetHeaderProperty()
                End If
                Dim cols As Integer = Math.Min(table.Columns.Count, _headers.Count)
                For rIdx = 0 To table.Rows.Count - 1
                    Dim dr As System.Data.DataRow = table.Rows(rIdx)
                    For cIdx = 0 To cols - 1
                        Dim v As Object = dr(cIdx)
                        Me.Cell(cIdx, rIdx).Text = If(v Is Nothing OrElse v Is DBNull.Value, "", v.ToString())
                    Next
                Next
            Finally
                EndUpdate()
            End Try
        End Sub

    End Class

End Namespace
