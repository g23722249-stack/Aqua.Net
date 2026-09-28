Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.CellRow UserControl (Control\CellRow.ctl): one grid row,
' hosting a horizontal run of Cell controls. The 1px gaps between cells and the 1px
' outer margin expose the row's BackColor (grid-line colour) as the vertical grid lines.
' Cell events are re-raised as ItemXxx(index, col, ...) where index is this row's Index,
' so the Grid can wire them exactly as the VB6 crItem_ItemXxx handlers did.
Namespace Global.Aqua

    Public Class CellRow
        Inherits Control

        Private Const OutDrop As Integer = 1     ' gc_intGridOutDropWidth
        Private Const SplitWidth As Integer = 1  ' gc_intGridSplitWidth

        Private ReadOnly _cells As New List(Of Cell)()
        Private _selected As Boolean = False
        Private _fixed As Boolean = False
        Private _inResize As Boolean = False

        Private _rowIndex As Integer = 0
        Private _itemTop As Integer = 0
        Private _itemLeft As Integer = 0
        Private _itemOnView As Boolean = True

        ' template colours/selcolor kept so new cells inherit them (VB6 read Cell1(0))
        Private _cellBackColor As Color = Color.White
        Private _cellSelColor As Color = GridConst.GridSelColor

        ' --- events consumed by the Grid (index = this row's Index) ---
        Public Event ItemEnterFocus(index As Integer, col As Integer)
        Public Event ItemExitFocus(index As Integer, col As Integer)
        Public Event ItemCheckedClick(index As Integer, col As Integer)
        Public Event ItemIconClick(index As Integer, col As Integer)
        Public Event ItemExtIconClick(index As Integer, col As Integer)
        Public Event ItemAlignmentChanged(index As Integer, col As Integer)
        Public Event ItemBackColorChanged(index As Integer, col As Integer)
        Public Event ItemForeColorChanged(index As Integer, col As Integer)
        Public Event ItemCheckStyleChanged(index As Integer, col As Integer)
        Public Event ItemCheckedChanged(index As Integer, col As Integer)
        Public Event ItemIconChanged(index As Integer, col As Integer)
        Public Event ItemExtIconChanged(index As Integer, col As Integer)
        Public Event ItemSelectedChanged(index As Integer, col As Integer)
        Public Event ItemEnabledChanged(index As Integer, col As Integer)
        Public Event ItemSelColorChanged(index As Integer, col As Integer)
        Public Event ItemTextChanged(index As Integer, col As Integer)
        Public Event ItemValueChanged(index As Integer, col As Integer)
        Public Event ItemColorOfCheckChanged(index As Integer, col As Integer)
        Public Event ItemFontChanged(index As Integer, col As Integer)
        Public Event ItemClick(index As Integer, col As Integer)
        Public Event ItemDblClick(index As Integer, col As Integer)
        Public Event ItemKeyDown(index As Integer, col As Integer, keyCode As Integer, shift As Integer)
        Public Event ItemKeyPress(index As Integer, col As Integer, keyAscii As Integer)
        Public Event ItemKeyUp(index As Integer, col As Integer, keyCode As Integer, shift As Integer)
        Public Event ItemMouseEnter(index As Integer, col As Integer)
        Public Event ItemMouseHover(index As Integer, col As Integer)
        Public Event ItemMouseLeave(index As Integer, col As Integer)
        Public Event ItemMousePress(index As Integer, col As Integer, button As Integer, shift As Integer)
        Public Event ItemMouseWheel(index As Integer, col As Integer, value As Integer)
        Public Event ItemMouseDown(index As Integer, col As Integer, button As Integer, shift As Integer, x As Single, y As Single)
        Public Event ItemMouseMove(index As Integer, col As Integer, button As Integer, shift As Integer, x As Single, y As Single)
        Public Event ItemMouseUp(index As Integer, col As Integer, button As Integer, shift As Integer, x As Single, y As Single)

        Public Sub New()
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = GridConst.GridLineColor
            TabStop = False
        End Sub

        '=====================================================================
        ' Identity / virtual-scroll bookkeeping
        '=====================================================================
        ''' <summary>This row's index within the Grid; stamped into every re-raised Item event.</summary>
        <Browsable(False)>
        Public Property Index As Integer
            Get
                Return _rowIndex
            End Get
            Set(value As Integer)
                _rowIndex = value
            End Set
        End Property

        <Browsable(False)>
        Public Property ItemTop As Integer
            Get
                Return _itemTop
            End Get
            Set(value As Integer)
                _itemTop = value
            End Set
        End Property

        <Browsable(False)>
        Public Property ItemLeft As Integer
            Get
                Return _itemLeft
            End Get
            Set(value As Integer)
                _itemLeft = value
            End Set
        End Property

        <Browsable(False)>
        Public Property ItemOnView As Boolean
            Get
                Return _itemOnView
            End Get
            Set(value As Boolean)
                _itemOnView = value
            End Set
        End Property

        <Browsable(False)>
        Public Property Fixed As Boolean
            Get
                Return _fixed
            End Get
            Set(value As Boolean)
                _fixed = value
            End Set
        End Property

        '=====================================================================
        ' Collection access
        '=====================================================================
        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _cells.Count
            End Get
        End Property

        Public ReadOnly Property Item(ByVal index As Integer) As Cell
            Get
                If index < 0 OrElse index >= _cells.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
                Return _cells(index)
            End Get
        End Property

        Public Function ItemWidth(ByVal index As Integer) As Integer
            If index < 0 OrElse index >= _cells.Count Then Return 0
            Return _cells(index).Width
        End Function

        Public Function ItemHeight(ByVal index As Integer) As Integer
            If index < 0 OrElse index >= _cells.Count Then Return 0
            Return _cells(index).Height
        End Function

        '=====================================================================
        ' Build / add / clear (ports of Build, AddItem, Clear)
        '=====================================================================
        ''' <summary>Ensure this row has one cell per header column, sized/aligned to match.</summary>
        Public Sub Build(ByVal headers As Headers)
            If headers Is Nothing Then Return

            While _cells.Count < headers.Count
                AddCell()
            End While
            While _cells.Count > headers.Count
                RemoveLastCell()
            End While

            For i = 0 To headers.Count - 1
                Dim c As Cell = _cells(i)
                c.Fixed = True
                c.Width = headers.ItemRunWidth(i)
                c.Fixed = False
                c.Alignment = headers.Alignment(i)
            Next

            LayoutCells()
            For Each c In _cells
                c.Visible = True
            Next
        End Sub

        ''' <summary>Append a cell with the given text (port of AddItem).</summary>
        Public Function AddItem(ByVal text As String, Optional ByVal width As Integer = 0) As Cell
            Dim c As Cell = AddCell()
            c.Text = text
            c.Value = text
            c.Fixed = True
            c.Width = If(width <= 0, 60, width)
            c.Fixed = False
            LayoutCells()
            c.Visible = True
            Return c
        End Function

        Public Sub Clear()
            While _cells.Count > 0
                RemoveLastCell()
            End While
        End Sub

        Private Function AddCell() As Cell
            Dim c As New Cell()
            c.TabStop = False
            c.BackColor = _cellBackColor
            c.SelColor = _cellSelColor
            c.Selected = _selected
            c.Font = Font
            WireCell(c, _cells.Count)
            _cells.Add(c)
            Controls.Add(c)
            Return c
        End Function

        Private Sub RemoveLastCell()
            Dim last As Integer = _cells.Count - 1
            If last < 0 Then Return
            Dim c As Cell = _cells(last)
            Controls.Remove(c)
            _cells.RemoveAt(last)
            c.Dispose()
        End Sub

        '=====================================================================
        ' Layout (port of SetUserControlPosition)
        '=====================================================================
        Private Sub LayoutCells()
            If _cells.Count = 0 Then Return
            _inResize = True
            Try
                Dim x As Integer = OutDrop
                Dim h As Integer = _cells(0).Height
                For Each c In _cells
                    c.Top = 0
                    c.Left = x
                    c.Height = h
                    x += c.Width + SplitWidth
                Next
                Dim totalW As Integer = OutDrop * 2 + x - OutDrop - SplitWidth  ' outdrop + widths + (n-1) splits + outdrop
                Width = totalW
                Height = h
            Finally
                _inResize = False
            End Try
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            If _inResize OrElse _fixed Then Return
            If _cells.Count > 0 Then Height = _cells(0).Height
        End Sub

        '=====================================================================
        ' Colour / font / selection propagation
        '=====================================================================
        <Browsable(False)>
        Public Shadows Property BackColor As Color
            Get
                If _cells.Count > 0 Then Return _cells(0).BackColor
                Return _cellBackColor
            End Get
            Set(value As Color)
                _cellBackColor = value
                For Each c In _cells
                    c.BackColor = value
                Next
                ' the container keeps the grid-line colour so gaps read as grid lines
                MyBase.BackColor = If(_cells.Count > 0, GridConst.GridLineColor, value)
            End Set
        End Property

        <Browsable(False)>
        Public Shadows Property ForeColor As Color
            Get
                If _cells.Count > 0 Then Return _cells(0).ForeColor
                Return MyBase.ForeColor
            End Get
            Set(value As Color)
                For Each c In _cells
                    c.ForeColor = value
                Next
            End Set
        End Property

        <Browsable(False)>
        Public Property SelColor As Color
            Get
                If _cells.Count > 0 Then Return _cells(0).SelColor
                Return _cellSelColor
            End Get
            Set(value As Color)
                _cellSelColor = value
                For Each c In _cells
                    c.SelColor = value
                Next
            End Set
        End Property

        Public Shadows Property Font As Font
            Get
                Return MyBase.Font
            End Get
            Set(value As Font)
                MyBase.Font = value
                For Each c In _cells
                    c.Font = value
                Next
            End Set
        End Property

        <Browsable(False)>
        Public Property Selected As Boolean
            Get
                Return _selected
            End Get
            Set(value As Boolean)
                If _selected = value Then Return
                _selected = value
                For Each c In _cells
                    c.Selected = value
                Next
            End Set
        End Property

        Public Shadows Sub Refresh()
            For Each c In _cells
                c.Refresh()
            Next
            MyBase.Refresh()
        End Sub

        '=====================================================================
        ' Column split / focus / row swap
        '=====================================================================
        ''' <summary>Resize columns index and index+1 during a header split (port of Split).</summary>
        Public Sub Split(ByVal index As Integer, ByVal left1 As Integer, ByVal width1 As Integer, ByVal left2 As Integer, ByVal width2 As Integer)
            If _cells.Count <= 1 Then Return
            If index < 0 OrElse index + 1 >= _cells.Count Then Return
            _cells(index + 1).Width = width2
            _cells(index + 1).Fixed = True
            _cells(index + 1).Left = left2
            _cells(index + 1).Fixed = False
            _cells(index).Width = width1
            LayoutCells()
        End Sub

        Public Sub SetCellFocus(ByVal index As Integer)
            If index < 0 OrElse index >= _cells.Count Then Return
            _cells(index).Focus()
        End Sub

        ''' <summary>Swap every cell's content with another row's cells (port of Switch, used by sort).</summary>
        Public Sub Switch(ByVal other As CellRow)
            If other Is Nothing Then Return
            Dim n As Integer = Math.Min(_cells.Count, other.Count)
            For i = 0 To n - 1
                _cells(i).Switch(other.Item(i))
            Next
        End Sub

        '=====================================================================
        ' Event bubbling: Cell.* -> ItemXxx(Me.Index, col, ...)
        '=====================================================================
        Private Sub WireCell(ByVal c As Cell, ByVal col As Integer)
            AddHandler c.AlignmentChanged, Sub() RaiseEvent ItemAlignmentChanged(_rowIndex, col)
            AddHandler c.BackColorChanged, Sub() RaiseEvent ItemBackColorChanged(_rowIndex, col)
            AddHandler c.CheckedChanged, Sub() RaiseEvent ItemCheckedChanged(_rowIndex, col)
            AddHandler c.CheckStyleChanged, Sub() RaiseEvent ItemCheckStyleChanged(_rowIndex, col)
            AddHandler c.Click, Sub() RaiseEvent ItemClick(_rowIndex, col)
            AddHandler c.ColorOfCheckChanged, Sub() RaiseEvent ItemColorOfCheckChanged(_rowIndex, col)
            AddHandler c.DoubleClick, Sub() RaiseEvent ItemDblClick(_rowIndex, col)
            AddHandler c.EnabledChanged, Sub() RaiseEvent ItemEnabledChanged(_rowIndex, col)
            AddHandler c.GotFocus, Sub() RaiseEvent ItemEnterFocus(_rowIndex, col)
            AddHandler c.LostFocus, Sub() RaiseEvent ItemExitFocus(_rowIndex, col)
            AddHandler c.ExtIconChanged, Sub() RaiseEvent ItemExtIconChanged(_rowIndex, col)
            AddHandler c.ExtIconClick, Sub() RaiseEvent ItemExtIconClick(_rowIndex, col)
            AddHandler c.FontChanged, Sub() RaiseEvent ItemFontChanged(_rowIndex, col)
            AddHandler c.ForeColorChanged, Sub() RaiseEvent ItemForeColorChanged(_rowIndex, col)
            AddHandler c.IconChanged, Sub() RaiseEvent ItemIconChanged(_rowIndex, col)
            AddHandler c.IconClick, Sub() RaiseEvent ItemIconClick(_rowIndex, col)
            AddHandler c.CheckedClick, Sub() RaiseEvent ItemCheckedClick(_rowIndex, col)
            AddHandler c.SelColorChanged, Sub() RaiseEvent ItemSelColorChanged(_rowIndex, col)
            AddHandler c.SelectedChanged, Sub() RaiseEvent ItemSelectedChanged(_rowIndex, col)
            AddHandler c.TextChanged, Sub() RaiseEvent ItemTextChanged(_rowIndex, col)
            AddHandler c.TextClick, Sub() RaiseEvent ItemClick(_rowIndex, col)
            AddHandler c.ValueChanged, Sub() RaiseEvent ItemValueChanged(_rowIndex, col)

            AddHandler c.KeyDown, Sub(s As Object, e As KeyEventArgs) RaiseEvent ItemKeyDown(_rowIndex, col, CInt(e.KeyCode), ShiftBits(e))
            AddHandler c.KeyUp, Sub(s As Object, e As KeyEventArgs) RaiseEvent ItemKeyUp(_rowIndex, col, CInt(e.KeyCode), ShiftBits(e))
            AddHandler c.KeyPress, Sub(s As Object, e As KeyPressEventArgs) RaiseEvent ItemKeyPress(_rowIndex, col, Asc(e.KeyChar))
            AddHandler c.MouseEnter, Sub() RaiseEvent ItemMouseEnter(_rowIndex, col)
            AddHandler c.MouseHover, Sub() RaiseEvent ItemMouseHover(_rowIndex, col)
            AddHandler c.MouseLeave, Sub() RaiseEvent ItemMouseLeave(_rowIndex, col)
            AddHandler c.MouseDown, Sub(s As Object, e As MouseEventArgs) RaiseEvent ItemMouseDown(_rowIndex, col, ButtonBits(e), 0, e.X, e.Y)
            AddHandler c.MouseMove, Sub(s As Object, e As MouseEventArgs) RaiseEvent ItemMouseMove(_rowIndex, col, ButtonBits(e), 0, e.X, e.Y)
            AddHandler c.MouseUp, Sub(s As Object, e As MouseEventArgs) RaiseEvent ItemMouseUp(_rowIndex, col, ButtonBits(e), 0, e.X, e.Y)
        End Sub

        Private Shared Function ShiftBits(ByVal e As KeyEventArgs) As Integer
            Dim s As Integer = 0
            If e.Shift Then s = s Or 1
            If e.Control Then s = s Or 2
            If e.Alt Then s = s Or 4
            Return s
        End Function

        Private Shared Function ButtonBits(ByVal e As MouseEventArgs) As Integer
            Select Case e.Button
                Case MouseButtons.Left : Return 1
                Case MouseButtons.Right : Return 2
                Case MouseButtons.Middle : Return 4
                Case Else : Return 0
            End Select
        End Function

    End Class

End Namespace
