Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.Cell UserControl (Control\Cell.ctl).
' A Cell is one column-cell inside a CellRow: optional check mark, optional icon,
' a text label, and an optional trailing "ext" icon, laid out per Alignment.
' The layout maths and the selected-row text-colour rule are reproduced faithfully;
' the check images are the original 16x16 icons (see CheckMarkResources).
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class Cell
        Inherits Control

        ' 4px gap between elements — VB6 used (4 * Screen.TwipsPerPixelX) i.e. 4 pixels.
        Private Const Interval As Integer = 4

        ' --- backing fields (mirror the VB6 m_ variables) ---
        Private _selColor As Color = ColorUtil.OleToColor(GridConst.gc_lngGridSelColor)
        Private _colorOfCheck As ColorConstants = ColorConstants.Blue
        Private _checkStyle As ItemCheckStyle = ItemCheckStyle.None
        Private _alignment As AlignmentConstants = AlignmentConstants.LeftJustify
        Private _value As String = ""
        Private _checked As Boolean = False
        Private _selected As Boolean = False
        Private _icon As Image = Nothing
        Private _extIcon As Image = Nothing

        ' virtual-scroll bookkeeping used by the Grid
        Private _itemTop As Integer = 0
        Private _itemLeft As Integer = 0
        Private _itemOnView As Boolean = True

        ' suppress relayout/height changes during bulk updates (sort / Switch)
        Private _fixed As Boolean = False
        Private _inLayout As Boolean = False

        ' last computed layout, for hit-testing
        Private _lyCheck As Rectangle = Rectangle.Empty
        Private _lyIcon As Rectangle = Rectangle.Empty
        Private _lyText As Rectangle = Rectangle.Empty
        Private _lyExt As Rectangle = Rectangle.Empty

        ' --- custom events (base Control already provides Click/DblClick/Key*/Mouse*/*Changed) ---
        Public Event CheckedClick(sender As Object, e As EventArgs)
        Public Event TextClick(sender As Object, e As EventArgs)
        Public Event IconClick(sender As Object, e As EventArgs)
        Public Event ExtIconClick(sender As Object, e As EventArgs)

        Public Event SelColorChanged(sender As Object, e As EventArgs)
        Public Event ColorOfCheckChanged(sender As Object, e As EventArgs)
        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event IconChanged(sender As Object, e As EventArgs)
        Public Event ExtIconChanged(sender As Object, e As EventArgs)
        Public Event CheckStyleChanged(sender As Object, e As EventArgs)
        Public Event CheckedChanged(sender As Object, e As EventArgs)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event AlignmentChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            MyBase.BackColor = ColorUtil.OleToColor(GridConst.gc_lngGridOddBackColor)
            MyBase.ForeColor = Color.Black
            TabStop = False
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================

        <DefaultValue(GetType(AlignmentConstants), "LeftJustify")>
        Public Property Alignment As AlignmentConstants
            Get
                Return _alignment
            End Get
            Set(value As AlignmentConstants)
                If _alignment = value Then Return
                _alignment = value
                Relayout()
                RaiseEvent AlignmentChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)>
        Public Property SelColor As Color
            Get
                Return _selColor
            End Get
            Set(value As Color)
                If _selColor.ToArgb() = value.ToArgb() Then Return
                _selColor = value
                If Not _fixed Then Invalidate()
                RaiseEvent SelColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property ColorOfCheck As ColorConstants
            Get
                Return _colorOfCheck
            End Get
            Set(value As ColorConstants)
                If _colorOfCheck = value Then Return
                _colorOfCheck = value
                If Not _fixed Then Invalidate()
                RaiseEvent ColorOfCheckChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                _icon = value
                Relayout()
                RaiseEvent IconChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)>
        Public Property ExtIcon As Image
            Get
                Return _extIcon
            End Get
            Set(value As Image)
                _extIcon = value
                Relayout()
                RaiseEvent ExtIconChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <DefaultValue("")>
        Public Property Value As String
            Get
                Return _value
            End Get
            Set(v As String)
                If _value = v Then Return
                _value = If(v, "")
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <DefaultValue(GetType(ItemCheckStyle), "None")>
        Public Property CheckStyle As ItemCheckStyle
            Get
                Return _checkStyle
            End Get
            Set(value As ItemCheckStyle)
                If _checkStyle = value Then Return
                _checkStyle = value
                Relayout()
                RaiseEvent CheckStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <DefaultValue(False)>
        Public Property Checked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If _checked = value Then Return
                _checked = value
                Relayout()
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <DefaultValue(False)>
        Public Property Selected As Boolean
            Get
                Return _selected
            End Get
            Set(value As Boolean)
                If _selected = value Then Return
                _selected = value
                If Not _fixed Then Invalidate()
                RaiseEvent SelectedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Logical top of the row in grid coordinates (virtual scroll bookkeeping).</summary>
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

        ''' <summary>Whether this cell's row is within the visible viewport.</summary>
        <Browsable(False)>
        Public Property ItemOnView As Boolean
            Get
                Return _itemOnView
            End Get
            Set(value As Boolean)
                _itemOnView = value
            End Set
        End Property

        ''' <summary>When True, relayout/height changes are suppressed (used during bulk updates).</summary>
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
        ' Base-property change hooks -> relayout / repaint
        '=====================================================================
        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Relayout()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Relayout()
        End Sub

        Protected Overrides Sub OnForeColorChanged(e As EventArgs)
            MyBase.OnForeColorChanged(e)
            If Not _fixed Then Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            If Not _fixed Then Invalidate()
        End Sub

        '=====================================================================
        ' Layout (port of SetUserControlPosition)
        '=====================================================================
        Private Sub Relayout()
            If _fixed Then Return
            UpdateHeight()
            Invalidate()
        End Sub

        Private Function TextIsEmpty() As Boolean
            Return String.IsNullOrEmpty(Text) OrElse Text.Trim().Length = 0
        End Function

        Private Function MeasureText() As Size
            If TextIsEmpty() Then Return Size.Empty
            Return TextRenderer.MeasureText(Text, Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                            TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End Function

        ''' <summary>Row cells are text-height + 4px tall, exactly as the VB6 code set them.</summary>
        Private Sub UpdateHeight()
            If _inLayout Then Return
            Dim onlyChecked As Boolean = (_checkStyle <> ItemCheckStyle.None) AndAlso
                                         TextIsEmpty() AndAlso _icon Is Nothing AndAlso _extIcon Is Nothing
            If onlyChecked Then Return

            Dim th As Integer = TextRenderer.MeasureText("Ag", Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                                         TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine).Height
            Dim desired As Integer = th + 4
            If Height <> desired Then
                _inLayout = True
                Try
                    Height = desired
                Finally
                    _inLayout = False
                End Try
            End If
        End Sub

        ''' <summary>Compute element rectangles for the current size (no side effects). Port of SetUserControlPosition.</summary>
        Private Sub ComputeLayout()
            _lyCheck = Rectangle.Empty
            _lyIcon = Rectangle.Empty
            _lyText = Rectangle.Empty
            _lyExt = Rectangle.Empty

            Dim w As Integer = ClientSize.Width
            Dim h As Integer = ClientSize.Height

            Dim checkImg As Image = CheckImage()
            Dim checkW As Integer = If(checkImg IsNot Nothing, checkImg.Width, 0)
            Dim checkH As Integer = If(checkImg IsNot Nothing, checkImg.Height, 0)
            Dim iconW As Integer = If(_icon IsNot Nothing, _icon.Width, 0)
            Dim iconH As Integer = If(_icon IsNot Nothing, _icon.Height, 0)
            Dim extW As Integer = If(_extIcon IsNot Nothing, _extIcon.Width, 0)
            Dim extH As Integer = If(_extIcon IsNot Nothing, _extIcon.Height, 0)
            Dim ts As Size = MeasureText()
            Dim textVisible As Boolean = Not TextIsEmpty()

            Dim onlyChecked As Boolean = (_checkStyle <> ItemCheckStyle.None) AndAlso
                                         TextIsEmpty() AndAlso _icon Is Nothing AndAlso _extIcon Is Nothing

            If onlyChecked Then
                Dim cx As Integer
                If _alignment = AlignmentConstants.Center Then
                    cx = (w - checkW) \ 2
                Else
                    cx = Interval
                End If
                _lyCheck = New Rectangle(cx, (h - checkH) \ 2, checkW, checkH)
                Return
            End If

            Dim lngLeft As Integer = Interval

            ' check mark first (when a style is set)
            If _checkStyle <> ItemCheckStyle.None AndAlso checkImg IsNot Nothing Then
                _lyCheck = New Rectangle(lngLeft, (h - checkH) \ 2, checkW, checkH)
                lngLeft = _lyCheck.Right + Interval
            End If

            ' alignment adjusts the starting X for the icon+text run
            Select Case _alignment
                Case AlignmentConstants.LeftJustify
                    ' lngLeft already correct
                Case AlignmentConstants.Center
                    If _icon Is Nothing Then
                        lngLeft = (w - ts.Width) \ 2
                    Else
                        lngLeft = (w - (iconW + Interval + ts.Width)) \ 2
                    End If
                Case AlignmentConstants.RightJustify
                    If _extIcon Is Nothing Then
                        If _icon Is Nothing Then
                            lngLeft = w - Interval - ts.Width
                        Else
                            lngLeft = w - Interval - ts.Width - Interval - iconW
                        End If
                    Else
                        If _icon Is Nothing Then
                            lngLeft = w - Interval - extW - Interval - ts.Width
                        Else
                            lngLeft = w - Interval - extW - Interval - ts.Width - Interval - iconW
                        End If
                    End If
            End Select

            ' icon
            If _icon IsNot Nothing Then
                _lyIcon = New Rectangle(lngLeft, (h - iconH) \ 2, iconW, iconH)
                lngLeft = _lyIcon.Right + Interval
            End If

            ' text
            If textVisible Then
                _lyText = New Rectangle(lngLeft, (h - ts.Height) \ 2, ts.Width, ts.Height)
            End If

            ' ext icon pinned near the right edge (VB6: Width - extW*1.5)
            If _extIcon IsNot Nothing Then
                Dim ex As Integer = w - CInt(extW * 1.5)
                _lyExt = New Rectangle(ex, (h - extH) \ 2, extW, extH)
            End If
        End Sub

        Private Function CheckImage() As Image
            Return CheckMarkResources.GetCheckSurface(_checkStyle, _colorOfCheck, _checked, Enabled)
        End Function

        '=====================================================================
        ' Painting (port of DrawListItemBackground + DrawUserControlText + SetTextColor)
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            ComputeLayout()
            Dim g As Graphics = e.Graphics

            ' background: selected -> SelColor fill, otherwise BackColor
            If _selected Then
                Using b As New SolidBrush(_selColor)
                    g.FillRectangle(b, ClientRectangle)
                End Using
            Else
                Using b As New SolidBrush(BackColor)
                    g.FillRectangle(b, ClientRectangle)
                End Using
            End If

            ' check image
            Dim checkImg As Image = CheckImage()
            If checkImg IsNot Nothing AndAlso Not _lyCheck.IsEmpty Then
                g.DrawImage(checkImg, _lyCheck)
            End If

            ' icon
            If _icon IsNot Nothing AndAlso Not _lyIcon.IsEmpty Then
                g.DrawImage(_icon, _lyIcon)
            End If

            ' text
            If Not _lyText.IsEmpty Then
                TextRenderer.DrawText(g, Text, Font, _lyText.Location, ResolveTextColor(),
                                      TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            End If

            ' ext icon
            If _extIcon IsNot Nothing AndAlso Not _lyExt.IsEmpty Then
                g.DrawImage(_extIcon, _lyExt)
            End If
        End Sub

        ''' <summary>
        ''' Selected + saturated dark background -> white text, else ForeColor; disabled -> grey.
        ''' Mirrors SetTextColor's HSL test (Luminosity &lt;= 90 And Saturation &gt; 50).
        ''' </summary>
        Private Function ResolveTextColor() As Color
            If Not Enabled Then
                Return GridConst.DisableForeColor
            End If
            If _selected Then
                Dim hsl As Hsl = ColorUtil.ColorToHsl(ColorUtil.ColorToOle(_selColor))
                If hsl.Luminosity <= 90 AndAlso hsl.Saturation > 50 Then
                    Return Color.White
                End If
            End If
            Return ForeColor
        End Function

        '=====================================================================
        ' Mouse / keyboard routing (port of UserControl_MouseDown hit-tests)
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            If e.Button = MouseButtons.Left Then
                ComputeLayout()
                If Not _lyCheck.IsEmpty AndAlso _lyCheck.Contains(e.Location) Then
                    ToggleChecked()
                    RaiseEvent CheckedClick(Me, EventArgs.Empty)
                ElseIf Not _lyIcon.IsEmpty AndAlso _lyIcon.Contains(e.Location) Then
                    RaiseEvent IconClick(Me, EventArgs.Empty)
                ElseIf Not _lyExt.IsEmpty AndAlso _lyExt.Contains(e.Location) Then
                    RaiseEvent ExtIconClick(Me, EventArgs.Empty)
                ElseIf Not _lyText.IsEmpty AndAlso _lyText.Contains(e.Location) Then
                    RaiseEvent TextClick(Me, EventArgs.Empty)
                End If
            End If
            MyBase.OnMouseDown(e)
        End Sub

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            If e.KeyCode = Keys.Space AndAlso _checkStyle <> ItemCheckStyle.None Then
                ToggleChecked()
            End If
            MyBase.OnKeyDown(e)
        End Sub

        Private Sub ToggleChecked()
            Checked = Not _checked
        End Sub

        '=====================================================================
        ' Public methods
        '=====================================================================

        ''' <summary>Reset the cell to empty (port of Cell.Clear).</summary>
        Public Sub Clear()
            Text = ""
            _value = ""
            _icon = Nothing
            _extIcon = Nothing
            If _selected Then Selected = False
            Relayout()
        End Sub

        ''' <summary>Repaint the cell (port of Cell.Refresh -> PaintUserControl).</summary>
        Public Shadows Sub Refresh()
            If Not _fixed Then
                UpdateHeight()
                Invalidate()
            End If
            MyBase.Refresh()
        End Sub

        ''' <summary>
        ''' Swap all visual state with another cell (port of Cell.Switch, used by Grid sort).
        ''' </summary>
        Public Sub Switch(other As Cell)
            If other Is Nothing Then Return

            ' snapshot self
            Dim sFont = Me.Font : Dim sFore = Me.ForeColor : Dim sSel = _selColor
            Dim sColorOfCheck = _colorOfCheck : Dim sStyle = _checkStyle : Dim sAlign = _alignment
            Dim sChecked = _checked : Dim sSelected = _selected : Dim sEnabled = Me.Enabled
            Dim sIcon = _icon : Dim sExt = _extIcon
            Dim sText = Me.Text : Dim sValue = _value

            Me.Fixed = True
            other.Fixed = True

            ' copy other -> self
            Me.Font = other.Font : Me.ForeColor = other.ForeColor : _selColor = other.SelColor
            _colorOfCheck = other.ColorOfCheck : _checkStyle = other.CheckStyle : _alignment = other.Alignment
            _checked = other.Checked : _selected = other.Selected : Me.Enabled = other.Enabled
            _icon = other.Icon : _extIcon = other.ExtIcon
            Me.Text = other.Text : _value = other.Value

            ' copy snapshot -> other
            other.Font = sFont : other.ForeColor = sFore : other.SelColor = sSel
            other.ColorOfCheck = sColorOfCheck : other.CheckStyle = sStyle : other.Alignment = sAlign
            other.Checked = sChecked : other.Selected = sSelected : other.Enabled = sEnabled
            other.Icon = sIcon : other.ExtIcon = sExt
            other.Text = sText : other.Value = sValue

            Me.Fixed = False
            other.Fixed = False
            other.Refresh()
            Me.Refresh()
        End Sub

    End Class

End Namespace
