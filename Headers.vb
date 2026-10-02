Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
' Alias the enum so the public accessor Function SortOrder(index) does not shadow the type name.
Imports SortOrderEnum = Aqua.SortOrder

' Owner-drawn port of the VB6 Aqua.Headers UserControl (Control\Headers.ctl):
' the column-header bar. Reproduces the themed button surfaces (stretched templates),
' per-alignment text/icon/sort-arrow layout, click sort-cycling, draggable column
' splitters and the bottom border line.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class Headers
        Inherits Control

        Private Const Interval As Integer = 4          ' 4px gap inside a button
        Private Const OutDrop As Integer = 1           ' gc_intGridOutDropWidth
        Private Const SplitWidth As Integer = 1        ' gc_intGridSplitWidth
        Private Const SplitHitTol As Integer = 3       ' px either side of a boundary = resize zone
        Private Const MinColWidth As Integer = 20      ' intKeepPixels in picSplit_MouseMove
        Private Const ClickWaitMs As Integer = 80      ' gc_lngButtonClickWaitInterval

        Private Structure ColumnInfo
            Public Text As String
            Public Icon As Image
            Public Width As Integer
            Public RunWidth As Integer
            Public Alignment As AlignmentConstants
            Public Sort As Boolean
            Public Order As SortOrderEnum
        End Structure

        Private _cols As New List(Of ColumnInfo)()
        Private _selIndex As Integer = -1
        Private _color As ColorConstants = ColorConstants.Blue
        Private _lastWidth As Integer = 0              ' stretched width of the final column
        Private _rects As New List(Of Rectangle)()     ' per-column button rectangles
        Private _resizing As Boolean = False

        Private _clickFlashIndex As Integer = -1
        Private ReadOnly _flashTimer As New Timer()

        ' split-drag state
        Private _splitDragIndex As Integer = -1
        Private _splitDragging As Boolean = False
        Private _splitStartX As Integer

        ' --- events consumed by the Grid ---
        Public Event HeaderClick(sender As Object, index As Integer)
        Public Event BeforeSort(index As Integer, ByRef cancel As Boolean)
        Public Event SortRequested(index As Integer, order As SortOrderEnum)
        Public Event AfterSort(index As Integer)
        Public Event ItemSortOrderChanged(index As Integer, order As SortOrderEnum)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event ItemSplitStart(index As Integer, left1 As Integer, width1 As Integer, left2 As Integer, width2 As Integer)
        Public Event ItemSplit(index As Integer, left1 As Integer, width1 As Integer, left2 As Integer, width2 As Integer)
        Public Event ItemSplitComplete(index As Integer, left1 As Integer, width1 As Integer, left2 As Integer, width2 As Integer)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = GridConst.GridLineColor
            MyBase.ForeColor = System.Drawing.Color.Black
            TabStop = False
            _flashTimer.Interval = ClickWaitMs
            AddHandler _flashTimer.Tick, AddressOf OnFlashTick
        End Sub

        '=====================================================================
        ' Data model / public API (used by Grid)
        '=====================================================================
        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _cols.Count
            End Get
        End Property

        Public Shadows Function Text(ByVal index As Integer) As String
            CheckIndex(index)
            Return _cols(index).Text
        End Function

        Public Function Icon(ByVal index As Integer) As Image
            CheckIndex(index)
            Return _cols(index).Icon
        End Function

        Public Function Alignment(ByVal index As Integer) As AlignmentConstants
            CheckIndex(index)
            Return _cols(index).Alignment
        End Function

        Public Function Sort(ByVal index As Integer) As Boolean
            CheckIndex(index)
            Return _cols(index).Sort
        End Function

        Public Function SortOrder(ByVal index As Integer) As SortOrderEnum
            CheckIndex(index)
            Return _cols(index).Order
        End Function

        Public Function ItemWidth(ByVal index As Integer) As Integer
            CheckIndex(index)
            Return _cols(index).Width
        End Function

        ''' <summary>Pixel-aligned running width used to size the row cells (VB6 ItemRunWidth).</summary>
        Friend Function ItemRunWidth(ByVal index As Integer) As Integer
            CheckIndex(index)
            ' the last column is stretched to fill; report the stretched width
            If index = _cols.Count - 1 AndAlso _lastWidth > 0 Then Return _lastWidth
            Return _cols(index).RunWidth
        End Function

        Public Function ItemLeft(ByVal index As Integer) As Integer
            CheckIndex(index)
            EnsureLayout()
            Return _rects(index).Left
        End Function

        <Browsable(False)>
        Public Property SelectedIndex As Integer
            Get
                Return _selIndex
            End Get
            Set(value As Integer)
                _selIndex = value
                Invalidate()
            End Set
        End Property

        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                Invalidate()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Sub Clear()
            _cols.Clear()
            _selIndex = -1
            Recalc()
            Invalidate()
        End Sub

        ''' <summary>Append a column (port of AdditionHeader).</summary>
        Public Sub AdditionHeader(ByVal icon As Image, ByVal text As String,
                                  ByVal alignment As AlignmentConstants, ByVal width As Integer,
                                  ByVal sort As Boolean, ByVal order As SortOrderEnum)
            Dim c As New ColumnInfo()
            c.Icon = icon
            c.Text = If(String.IsNullOrEmpty(text), "  ", text.TrimEnd())
            c.Width = If(width <= 0, DefaultHeaderWidth(), width)
            c.RunWidth = c.Width      ' already in pixels; VB6 rounded twips->px here
            c.Alignment = alignment
            c.Sort = sort
            c.Order = order
            _cols.Add(c)
        End Sub

        ''' <summary>Re-layout after headers were (re)built (port of SetHeaderProperty).</summary>
        Public Sub SetHeaderProperty()
            If _selIndex > _cols.Count - 1 Then _selIndex = -1
            Recalc()
            Invalidate()
        End Sub

        Private Shared Function DefaultHeaderWidth() As Integer
            Return 60 ' GetDefaultHeaderWidthTwips = 60px
        End Function

        Private Sub CheckIndex(ByVal index As Integer)
            If index < 0 OrElse index >= _cols.Count Then
                Throw New ArgumentOutOfRangeException(NameOf(index))
            End If
        End Sub

        '=====================================================================
        ' VB6 compatibility shims used by the Grid
        '=====================================================================
        Public Shadows Sub Move(ByVal x As Integer, ByVal y As Integer, ByVal w As Integer, ByVal h As Integer)
            SetBounds(x, y, w, h)
        End Sub

        Public Sub ZOrder(ByVal zpos As Integer)
            If zpos = 0 Then BringToFront() Else SendToBack()
        End Sub

        Public Shadows Sub Refresh()
            Recalc()
            Invalidate()
            MyBase.Refresh()
        End Sub

        '=====================================================================
        ' Layout (ports of ResizeUserControl + SetUserControlPosition)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Recalc()
            Invalidate()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Recalc()
            Invalidate()
        End Sub

        Private Sub EnsureLayout()
            If _rects.Count <> _cols.Count Then Recalc()
        End Sub

        Private Sub Recalc()
            _rects.Clear()
            Dim n As Integer = _cols.Count
            If n = 0 Then Return

            ' natural width required by all columns
            Dim natural As Integer = OutDrop * 2 + (n - 1) * SplitWidth
            For i = 0 To n - 1
                natural += _cols(i).RunWidth
            Next

            Dim assigned As Integer = Width
            Dim lastW As Integer = _cols(n - 1).RunWidth
            If assigned >= natural Then
                lastW = _cols(n - 1).RunWidth + (assigned - natural)   ' stretch final column
            Else
                ' header is wider than the space it was given -> keep natural width
                If Width <> natural Then
                    Width = natural   ' triggers OnResize/Recalc again with assigned = natural
                    Return
                End If
            End If
            _lastWidth = lastW

            Dim h As Integer = Height
            Dim x As Integer = OutDrop
            For i = 0 To n - 1
                Dim w As Integer = If(i = n - 1, lastW, _cols(i).RunWidth)
                _rects.Add(New Rectangle(x, 0, w, h))
                x += w + SplitWidth
            Next
        End Sub

        '=====================================================================
        ' Painting (ports of DrawUserControl + DrawHeaders)
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            EnsureLayout()
            Dim g As Graphics = e.Graphics

            ' background (grid-line colour shows through the 1px splitter gaps)
            Using b As New SolidBrush(BackColor)
                g.FillRectangle(b, ClientRectangle)
            End Using

            For i = 0 To _cols.Count - 1
                DrawColumn(g, i, _rects(i))
            Next

            ' bottom border line
            Using p As New Pen(GridConst.GridBorderLineColor, 2)
                g.DrawLine(p, 0, Height - 1, Width, Height - 1)
            End Using
        End Sub

        Private Sub DrawColumn(g As Graphics, ByVal index As Integer, ByVal rect As Rectangle)
            Dim col As ColumnInfo = _cols(index)

            ' surface
            Dim state As HeaderState = HeaderState.Normal
            If index = _clickFlashIndex Then
                state = HeaderState.Click
            ElseIf index = _selIndex Then
                state = HeaderState.Selected
            End If
            Dim surf As Image = HeaderResources.GetSurface(state, _color)
            If surf IsNot Nothing Then
                g.DrawImage(surf, rect)
            End If

            ' sort arrow (only on the selected, sortable column with a direction)
            Dim arrow As Image = Nothing
            Dim arrowRect As Rectangle = Rectangle.Empty
            If col.Sort AndAlso index = _selIndex AndAlso col.Order <> SortOrderEnum.None Then
                arrow = HeaderResources.GetSortArrow(col.Order)
                If arrow IsNot Nothing Then
                    Dim ax As Integer = rect.Right - CInt(arrow.Width * 1.5)
                    Dim ay As Integer = rect.Top + (rect.Height - arrow.Height) \ 2
                    arrowRect = New Rectangle(ax, ay, arrow.Width, arrow.Height)
                End If
            End If

            ' text + icon layout per alignment (port of DrawHeaders positioning)
            Dim hasText As Boolean = Not String.IsNullOrEmpty(col.Text) AndAlso col.Text.Trim().Length > 0
            Dim ts As Size = If(hasText,
                TextRenderer.MeasureText(col.Text, Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                         TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine),
                Size.Empty)
            Dim iconW As Integer = If(col.Icon IsNot Nothing, col.Icon.Width, 0)
            Dim iconH As Integer = If(col.Icon IsNot Nothing, col.Icon.Height, 0)
            Dim w As Integer = rect.Width

            Dim rel As Integer   ' left offset relative to the button rect
            If hasText AndAlso col.Icon IsNot Nothing Then
                Select Case col.Alignment
                    Case AlignmentConstants.LeftJustify : rel = Interval
                    Case AlignmentConstants.Center : rel = (w - (iconW + Interval + ts.Width)) \ 2
                    Case Else ' Right
                        If Not arrowRect.IsEmpty Then
                            rel = (arrowRect.Left - rect.Left) - ts.Width - Interval - iconW - Interval
                        Else
                            rel = w - Interval - ts.Width - Interval - iconW
                        End If
                End Select
                Dim iconRect As New Rectangle(rect.Left + rel, rect.Top + (rect.Height - iconH) \ 2, iconW, iconH)
                g.DrawImage(col.Icon, iconRect)
                DrawText(g, col.Text, New Point(iconRect.Right + Interval, rect.Top + (rect.Height - ts.Height) \ 2))
            ElseIf hasText Then
                Select Case col.Alignment
                    Case AlignmentConstants.LeftJustify : rel = Interval
                    Case AlignmentConstants.Center : rel = (w - ts.Width) \ 2
                    Case Else ' Right
                        If Not arrowRect.IsEmpty Then
                            rel = (arrowRect.Left - rect.Left) - ts.Width - Interval
                        Else
                            rel = w - ts.Width - Interval
                        End If
                End Select
                DrawText(g, col.Text, New Point(rect.Left + rel, rect.Top + (rect.Height - ts.Height) \ 2))
            ElseIf col.Icon IsNot Nothing Then
                Select Case col.Alignment
                    Case AlignmentConstants.LeftJustify : rel = Interval
                    Case AlignmentConstants.Center : rel = (w - iconW) \ 2
                    Case Else : rel = w - iconW - Interval
                End Select
                g.DrawImage(col.Icon, New Rectangle(rect.Left + rel, rect.Top + (rect.Height - iconH) \ 2, iconW, iconH))
            End If

            If arrow IsNot Nothing AndAlso Not arrowRect.IsEmpty Then
                g.DrawImage(arrow, arrowRect)
            End If
        End Sub

        Private Sub DrawText(g As Graphics, ByVal text As String, ByVal location As Point)
            TextRenderer.DrawText(g, text, Font, location, ForeColor,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End Sub

        '=====================================================================
        ' Interaction: splitter drag + column click
        '=====================================================================
        Private Function SplitBoundaryAt(ByVal x As Integer) As Integer
            ' returns the column index i whose right splitter is under x, else -1 (last col excluded)
            For i = 0 To _rects.Count - 2
                Dim edge As Integer = _rects(i).Right
                If Math.Abs(x - edge) <= SplitHitTol Then Return i
            Next
            Return -1
        End Function

        Private Function ColumnAt(ByVal x As Integer) As Integer
            For i = 0 To _rects.Count - 1
                If x >= _rects(i).Left AndAlso x < _rects(i).Right Then Return i
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _splitDragging Then
                DoSplitDrag(e.X)
                Return
            End If
            Cursor = If(SplitBoundaryAt(e.X) >= 0, Cursors.VSplit, Cursors.Default)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            Dim b As Integer = SplitBoundaryAt(e.X)
            If b >= 0 Then
                _splitDragIndex = b
                _splitDragging = True
                _resizing = False
                _splitStartX = e.X
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If e.Button <> MouseButtons.Left Then Return
            If _splitDragging Then
                Dim i As Integer = _splitDragIndex
                _splitDragging = False
                If _resizing Then
                    _resizing = False
                    RaiseEvent ItemSplitComplete(i, _rects(i).Left, _cols(i).RunWidth,
                                                 _rects(i + 1).Left, _cols(i + 1).RunWidth)
                End If
                _splitDragIndex = -1
                Return
            End If
            Dim col As Integer = ColumnAt(e.X)
            If col >= 0 Then HandleColumnClick(col)
        End Sub

        Private Sub DoSplitDrag(ByVal x As Integer)
            Dim i As Integer = _splitDragIndex
            If i < 0 OrElse i >= _cols.Count - 1 Then Return
            Dim dx As Integer = x - _splitStartX
            If dx = 0 Then Return

            ' constrain: neither neighbour narrower than MinColWidth
            Dim newLeftW As Integer = _cols(i).RunWidth + dx
            Dim newRightW As Integer = ItemRunWidth(i + 1) - dx
            If newLeftW < MinColWidth OrElse newRightW < MinColWidth Then Return

            Dim ci As ColumnInfo = _cols(i) : ci.RunWidth = newLeftW : ci.Width = newLeftW : _cols(i) = ci
            Dim cj As ColumnInfo = _cols(i + 1) : cj.RunWidth = newRightW : cj.Width = newRightW : _cols(i + 1) = cj
            _splitStartX = x

            Recalc()
            Invalidate()

            If Not _resizing Then
                _resizing = True
                RaiseEvent ItemSplitStart(i, _rects(i).Left, _cols(i).RunWidth,
                                          _rects(i + 1).Left, _cols(i + 1).RunWidth)
            End If
            RaiseEvent ItemSplit(i, _rects(i).Left, _cols(i).RunWidth,
                                 _rects(i + 1).Left, _cols(i + 1).RunWidth)
        End Sub

        ''' <summary>Port of picButton_Click: sort-cycle on the selected sortable column, then select+flash.</summary>
        Private Sub HandleColumnClick(ByVal index As Integer)
            If _cols.Count <= 0 Then Return

            If index = _selIndex AndAlso _cols(index).Sort Then
                Dim cancel As Boolean = False
                RaiseEvent BeforeSort(index, cancel)
                If Not cancel Then
                    Dim c As ColumnInfo = _cols(index)
                    Select Case c.Order
                        Case SortOrderEnum.None : c.Order = SortOrderEnum.Ascending
                        Case SortOrderEnum.Ascending : c.Order = SortOrderEnum.Descending
                        Case SortOrderEnum.Descending : c.Order = SortOrderEnum.Ascending
                    End Select
                    _cols(index) = c
                    RaiseEvent SortRequested(index, c.Order)
                    RaiseEvent AfterSort(index)
                    RaiseEvent ItemSortOrderChanged(index, c.Order)
                End If
            End If

            Dim prevSel As Integer = _selIndex
            _selIndex = index

            ' brief click flash
            _clickFlashIndex = index
            Invalidate()
            _flashTimer.Stop()
            _flashTimer.Start()

            RaiseEvent HeaderClick(Me, index)
            If prevSel <> _selIndex Then RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        Private Sub OnFlashTick(sender As Object, e As EventArgs)
            _flashTimer.Stop()
            _clickFlashIndex = -1
            Invalidate()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _flashTimer.Stop()
                _flashTimer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
