Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.iTextBox UserControl (Control\iTextBox.ctl): a pill-shaped single-line
' text box with an optional leading icon, always rounded (the VB6 Obtuseness field is read/
' written for persistence but was never actually wired to a public property or to
' RegionUserControl, which always used the fixed pill mask -- so this port always renders a
' true stadium/pill shape via RegionUtil, matching what the control actually looked like).
'
' Composition, not owner-drawing: hosts a real child System.Windows.Forms.TextBox for the
' actual editing, same reasoning as Label/TextBox -- a from-scratch caret/IME/selection
' implementation isn't worth it when the native Edit control already does it correctly.
Namespace Global.Aqua

    <DefaultEvent("TextChanged")>
    Public Class ITextBox
        Inherits UserControl

        Private ReadOnly _edit As New System.Windows.Forms.TextBox()

        Private _icon As Image
        Private _autoSelect As Boolean = False
        Private _rowActive As Boolean = True
        Private _parheliaColor As Color = GridConst.BorderFocusColor
        Private _focused As Boolean = False
        Private _normalBackColor As Color = SystemColors.Window
        Private _iconRect As Rectangle = Rectangle.Empty

        Public Event ImageClick(sender As Object, e As EventArgs)
        Public Event ImageDoubleClick(sender As Object, e As EventArgs)
        Public Event ImageMouseDown(sender As Object, e As MouseEventArgs)
        Public Event ImageMouseMove(sender As Object, e As MouseEventArgs)
        Public Event ImageMouseUp(sender As Object, e As MouseEventArgs)
        Public Event AlignmentChanged(sender As Object, e As EventArgs)
        Public Event LockedChanged(sender As Object, e As EventArgs)
        Public Event ImageChanged(sender As Object, e As EventArgs)
        Public Event Active(sender As Object, e As EventArgs)
        Public Event Deactivate(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            ' VB6 hardcoded Text1.BackColor to pure white, independent of the outer UserControl's own
            ' ambient BackColor (which defaults to the system window-face grey, not white) -- without
            ' this, _edit inherits that grey by default and looks like a mismatched block sitting on
            ' the pill surface art instead of blending with it.
            MyBase.BackColor = SystemColors.Window

            _edit.BorderStyle = BorderStyle.None
            _edit.TabStop = True
            Controls.Add(_edit)

            AddHandler _edit.TextChanged, Sub(sender, e) MyBase.OnTextChanged(e)
            AddHandler _edit.Enter, AddressOf OnEditEnter
            AddHandler _edit.Leave, AddressOf OnEditLeave
            AddHandler _edit.Validating, Sub(sender, e) MyBase.OnValidating(e)
            AddHandler _edit.Validated, Sub(sender, e) MyBase.OnValidated(e)
            AddHandler _edit.KeyDown, AddressOf OnEditKeyDown
            AddHandler _edit.KeyPress, Sub(sender, e) MyBase.OnKeyPress(e)
            AddHandler _edit.KeyUp, Sub(sender, e) MyBase.OnKeyUp(e)
            AddHandler _edit.Click, Sub(sender, e) MyBase.OnClick(e)
            AddHandler _edit.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler _edit.MouseDown, Sub(sender, e) MyBase.OnMouseDown(CType(e, MouseEventArgs))
            AddHandler _edit.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler _edit.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))
            AddHandler _edit.MouseEnter, Sub(sender, e) MyBase.OnMouseEnter(e)
            AddHandler _edit.MouseLeave, Sub(sender, e) MyBase.OnMouseLeave(e)

            _edit.Font = Font
            _edit.BackColor = BackColor
            _edit.ForeColor = ForeColor
            _normalBackColor = BackColor
            UpdateRegion()
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' Parent is reliably non-Nothing by now (unlike in the constructor, or a Resize that may
            ' never fire again if the control keeps its initial/default size) -- re-run so the
            ' corner-repaint request in UpdateRegion actually reaches a real parent at least once.
            UpdateRegion()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(120, 24)
            End Get
        End Property

        '=====================================================================
        ' Passthrough properties
        '=====================================================================
        ' UserControl hides Text from the designer (not browsable, not serialized) and an override inherits
        ' that: a default text typed in the designer was dropped on the next save.
        <Browsable(True), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible), Bindable(True)>
        Public Overrides Property Text As String
            Get
                Return _edit.Text
            End Get
            Set(value As String)
                _edit.Text = value
            End Set
        End Property

        ''' <summary>
        ''' Font/BackColor/ForeColor stay Me's own normal (inherited) Control properties rather than
        ''' being overridden to read from _edit -- _edit's Parent is Me, so a Get reading straight
        ''' from _edit would cycle the moment _edit's own value is ambient/unset (resolving it walks
        ''' up to Parent.Font i.e. Me.Font, right back to _edit.Font). See TextBox.vb for the full
        ''' writeup; that exact mistake stack-overflowed the WinForms designer the instant a
        ''' composed control using this pattern was dropped onto a form.
        ''' </summary>
        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            _edit.Font = Font
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnBackColorChanged(e As EventArgs)
            MyBase.OnBackColorChanged(e)
            _edit.BackColor = BackColor
            _normalBackColor = BackColor
            Invalidate()
        End Sub

        Protected Overrides Sub OnForeColorChanged(e As EventArgs)
            MyBase.OnForeColorChanged(e)
            _edit.ForeColor = ForeColor
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _edit.Enabled = Enabled
            Invalidate()
        End Sub

        Protected Overrides Sub OnCausesValidationChanged(e As EventArgs)
            MyBase.OnCausesValidationChanged(e)
            _edit.CausesValidation = CausesValidation
        End Sub

        <Category("外觀")>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                If _icon Is value Then Return
                _icon = value
                LayoutChildren()
                RaiseEvent ImageChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(GetType(HorizontalAlignment), "Left")>
        Public Property Alignment As HorizontalAlignment
            Get
                Return _edit.TextAlign
            End Get
            Set(value As HorizontalAlignment)
                If _edit.TextAlign = value Then Return
                _edit.TextAlign = value
                RaiseEvent AlignmentChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property AutoSelect As Boolean
            Get
                Return _autoSelect
            End Get
            Set(value As Boolean)
                _autoSelect = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property Locked As Boolean
            Get
                Return _edit.ReadOnly
            End Get
            Set(value As Boolean)
                If _edit.ReadOnly = value Then Return
                _edit.ReadOnly = value
                RaiseEvent LockedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(0)>
        Public Property MaxLength As Integer
            Get
                Return _edit.MaxLength
            End Get
            Set(value As Integer)
                _edit.MaxLength = value
            End Set
        End Property

        <Category("行為")>
        Public Property PasswordChar As Char
            Get
                Return _edit.PasswordChar
            End Get
            Set(value As Char)
                _edit.PasswordChar = value
            End Set
        End Property

        Public Property SelStart As Integer
            Get
                Return _edit.SelectionStart
            End Get
            Set(value As Integer)
                _edit.SelectionStart = value
            End Set
        End Property

        Public Property SelLength As Integer
            Get
                Return _edit.SelectionLength
            End Get
            Set(value As Integer)
                _edit.SelectionLength = value
            End Set
        End Property

        Public Property SelText As String
            Get
                Return _edit.SelectedText
            End Get
            Set(value As String)
                _edit.SelectedText = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(GetType(AutoCompleteMode), "None")>
        Public Property AutoCompleteMode As AutoCompleteMode
            Get
                Return _edit.AutoCompleteMode
            End Get
            Set(value As AutoCompleteMode)
                _edit.AutoCompleteMode = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(GetType(AutoCompleteSource), "None")>
        Public Property AutoCompleteSource As AutoCompleteSource
            Get
                Return _edit.AutoCompleteSource
            End Get
            Set(value As AutoCompleteSource)
                _edit.AutoCompleteSource = value
            End Set
        End Property

        <Category("行為")>
        Public Property AutoCompleteCustomSource As AutoCompleteStringCollection
            Get
                Return _edit.AutoCompleteCustomSource
            End Get
            Set(value As AutoCompleteStringCollection)
                _edit.AutoCompleteCustomSource = value
            End Set
        End Property

        ''' <summary>Focus glow colour (VB6: ParheliaColor). Distinct from BorderFocusColor on TextBox -- iTextBox has no border lines.</summary>
        <Category("外觀")>
        Public Property ParheliaColor As Color
            Get
                Return _parheliaColor
            End Get
            Set(value As Color)
                If _parheliaColor = value Then Return
                _parheliaColor = value
                Invalidate()
            End Set
        End Property

        ''' <summary>Grid-row style active/inactive tint (VB6: ActiveControl); named RowActive for the same reason as TextBox.RowActive.</summary>
        <Browsable(False)>
        Public Property RowActive As Boolean
            Get
                Return _rowActive
            End Get
            Set(value As Boolean)
                If _rowActive = value Then Return
                _rowActive = value
                _edit.BackColor = If(value, _normalBackColor, ColorUtil.OleToColor(14737632))   ' gc_lngDeactiveBackColor
                Invalidate()
                If value Then RaiseEvent Active(Me, EventArgs.Empty) Else RaiseEvent Deactivate(Me, EventArgs.Empty)
            End Set
        End Property

        Public Shadows Function Focus() As Boolean
            Return _edit.Focus()
        End Function

        '=====================================================================
        ' Layout (port of SetUserControlPosition / DrawUserControlImage)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            LayoutChildren()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(ObtusenessMode.All, Width, Height, Height \ 2))   ' true pill: radius = half height
            If old IsNot Nothing Then old.Dispose()
            ' SetWindowRgn on a child doesn't itself make the parent repaint the corner pixels the
            ' new (smaller) region just exposed -- without this they can be left showing whatever was
            ' there before (stale/garbage), instead of the parent's real current background.
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Private Sub LayoutChildren()
            Dim h As Integer = Height
            Dim iconArea As Integer = Math.Max(0, h)   ' square reserved at each end, matching VB6 (Text1.Left = Height with no icon too)

            _iconRect = Rectangle.Empty
            If _icon IsNot Nothing Then
                Dim ix As Integer = (iconArea - _icon.Width) \ 2
                Dim iy As Integer = (iconArea - _icon.Height) \ 2
                _iconRect = New Rectangle(ix, iy, _icon.Width, _icon.Height)
            End If

            Dim textW As Integer = Math.Max(0, Width - iconArea * 2)
            Dim textH As Integer = Math.Min(h, _edit.PreferredHeight)
            Dim textTop As Integer = (h - textH) \ 2
            _edit.SetBounds(iconArea, textTop, textW, textH)

            Invalidate()
        End Sub

        '=====================================================================
        ' Painting (ports of DrawUserControl / DrawControlParhelia)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim surface As Image = ITextBoxResources.GetSurface(_rowActive)
            If surface IsNot Nothing Then Skin.DrawStretch(g, surface, ClientRectangle, horizontal:=True)

            If _icon IsNot Nothing AndAlso Not _iconRect.IsEmpty Then g.DrawImage(_icon, _iconRect)

            If _focused Then BorderPainter.DrawParheliaGlow(g, Width, Height, _parheliaColor)
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If Not _iconRect.IsEmpty AndAlso _iconRect.Contains(e.Location) Then RaiseEvent ImageClick(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
            MyBase.OnMouseDoubleClick(e)
            If Not _iconRect.IsEmpty AndAlso _iconRect.Contains(e.Location) Then RaiseEvent ImageDoubleClick(Me, EventArgs.Empty)
        End Sub

        Private Sub OnEditEnter(sender As Object, e As EventArgs)
            _focused = True
            If _autoSelect Then _edit.SelectAll()
            Invalidate()
            MyBase.OnEnter(e)
        End Sub

        Private Sub OnEditLeave(sender As Object, e As EventArgs)
            _focused = False
            Invalidate()
            MyBase.OnLeave(e)
        End Sub

        ''' <summary>Port of Text1_KeyDown: Enter still raises KeyDown, then moves focus onward like Tab (SendKeys "{TAB}" in VB6).</summary>
        Private Sub OnEditKeyDown(sender As Object, e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Enter Then
                Dim p As Control = Parent
                If p IsNot Nothing Then p.SelectNextControl(Me, True, True, True, True)
            End If
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
