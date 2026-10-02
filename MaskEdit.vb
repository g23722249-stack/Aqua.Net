Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.MaskEdit UserControl (Control\MaskEdit.ctl), which wrapped the third-party
' MSMASK32.OCX (MSMask.MaskEdBox) control. .NET already ships a full-featured native replacement,
' System.Windows.Forms.MaskedTextBox (same Mask/PromptChar/AllowPromptAsInput vocabulary, plus its
' own MaskFull/MaskCompleted/MaskInputRejected), so this is composed the same way as TextBox.vb/
' EditBox.vb: a real child MaskedTextBox does the actual masked editing, Aqua chrome (themed
' border/region/focus glow, Embed flat-text mode) drawn around it.
'
' Simplifications vs the VB6 original: the separate "Format" display-formatting string (VB
' Format$-syntax, applied on top of the mask) has no equivalent on MaskedTextBox and is not
' applied -- Mask itself already does the formatting/punctuation job for the vast majority of real
' masks. ClipMode and the whole OLEDrag/OLEDrop family are not ported, matching TextBox.vb/
' EditBox.vb (neither of which carried those over either).
Namespace Global.Aqua

    <DefaultEvent("TextChanged")>
    Public Class MaskEdit
        Inherits UserControl

        Private Const BorderInset As Integer = 4   ' gc_intKeepBorderSize
        Private Const ShadowInset As Integer = 1   ' gc_intTextBoxShadowHeight

        Private ReadOnly _edit As New MaskedTextBox()

        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _embed As Boolean = False
        Private _autoSelect As Boolean = True
        Private _autoTab As Boolean = False
        Private _allowedKeys As EditAllowedKeys = EditAllowedKeys.AllowAll
        Private _rowActive As Boolean = True
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _soundEnterFocus As String = ""
        Private _soundExitFocus As String = ""
        Private _focused As Boolean = False
        Private _normalBackColor As Color = SystemColors.Window

        Public Event LockedChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event EmbedChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event AlignmentChanged(sender As Object, e As EventArgs)
        Public Event AllowedKeysChanged(sender As Object, e As EventArgs)
        Public Event AllowPromptChanged(sender As Object, e As EventArgs)
        Public Event Active(sender As Object, e As EventArgs)
        Public Event Deactivate(sender As Object, e As EventArgs)
        Public Event ValidationError(sender As Object, e As MaskInputRejectedEventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            MyBase.BackColor = SystemColors.Window

            _edit.BorderStyle = BorderStyle.None
            _edit.TabStop = True
            Controls.Add(_edit)

            AddHandler _edit.TextChanged, AddressOf OnEditTextChanged
            AddHandler _edit.Enter, AddressOf OnEditEnter
            AddHandler _edit.Leave, AddressOf OnEditLeave
            AddHandler _edit.Validating, Sub(sender, e) MyBase.OnValidating(e)
            AddHandler _edit.Validated, Sub(sender, e) MyBase.OnValidated(e)
            AddHandler _edit.KeyDown, AddressOf OnEditKeyDown
            AddHandler _edit.KeyPress, AddressOf OnEditKeyPress
            AddHandler _edit.KeyUp, Sub(sender, e) MyBase.OnKeyUp(CType(e, KeyEventArgs))
            AddHandler _edit.Click, Sub(sender, e) MyBase.OnClick(e)
            AddHandler _edit.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler _edit.MouseDown, Sub(sender, e) MyBase.OnMouseDown(CType(e, MouseEventArgs))
            AddHandler _edit.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler _edit.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))
            AddHandler _edit.MaskInputRejected, Sub(sender, e) RaiseEvent ValidationError(Me, e)

            _edit.Font = Font
            _edit.BackColor = BackColor
            _edit.ForeColor = ForeColor
            _normalBackColor = BackColor
            UpdateRegion()
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(120, 24)
            End Get
        End Property

        '=====================================================================
        ' Passthrough properties (proxy the inner MaskedTextBox)
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

        ''' <summary>Unformatted, prompt-and-literal-free entered characters only (VB6: Value getter
        ''' returned MaskEdBox1.ClipText). The setter is a plain string assignment -- VB6's
        ''' VBA.Format(Value, Format) is not applied, see the file header.</summary>
        <Browsable(False)>
        Public Property Value As Object
            Get
                Return _edit.MaskedTextProvider?.ToString(False, False)
            End Get
            Set(value As Object)
                Text = Convert.ToString(value)
            End Set
        End Property

        ''' <summary>Same as Text here -- .NET's MaskedTextBox.Text already includes prompt chars and
        ''' literals by default (TextMaskFormat.IncludePromptAndLiterals), which is what VB6's
        ''' FormattedText/Text(PromptInclude:=True) both amounted to.</summary>
        <Browsable(False)>
        Public ReadOnly Property FormattedText As String
            Get
                Return _edit.Text
            End Get
        End Property

        ' See Font/BackColor/ForeColor NOT overridden as properties in TextBox.vb for why: the same
        ' Parent-ambient-resolution recursion trap applies here, so they're left as Me's own normal
        ' inherited Control properties and just pushed down to _edit in the On*Changed overrides below.
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

        Protected Overrides Sub OnCausesValidationChanged(e As EventArgs)
            MyBase.OnCausesValidationChanged(e)
            _edit.CausesValidation = CausesValidation
        End Sub

        <Category("行為")>
        Public Property Mask As String
            Get
                Return _edit.Mask
            End Get
            Set(value As String)
                _edit.Mask = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(" "c)>
        Public Property PromptChar As Char
            Get
                Return _edit.PromptChar
            End Get
            Set(value As Char)
                _edit.PromptChar = If(value = ControlChars.NullChar, " "c, value)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property AllowPrompt As Boolean
            Get
                Return _edit.AllowPromptAsInput
            End Get
            Set(value As Boolean)
                If _edit.AllowPromptAsInput = value Then Return
                _edit.AllowPromptAsInput = value
                RaiseEvent AllowPromptChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(EditAllowedKeys.AllowAll)>
        Public Property AllowedKeys As EditAllowedKeys
            Get
                Return _allowedKeys
            End Get
            Set(value As EditAllowedKeys)
                If _allowedKeys = value Then Return
                _allowedKeys = value
                RaiseEvent AllowedKeysChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Whether Enter is pressed inside a fully-completed mask advances focus to the
        ''' next control, on top of the unconditional Enter-becomes-Tab behaviour below (VB6:
        ''' MaskEdBox1.AutoTab, a native MSMask feature with no .NET MaskedTextBox equivalent --
        ''' reimplemented here via MaskFull + SendKeys).</summary>
        <Category("行為")>
        <DefaultValue(False)>
        Public Property AutoTab As Boolean
            Get
                Return _autoTab
            End Get
            Set(value As Boolean)
                _autoTab = value
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
        <DefaultValue(True)>
        Public Property AutoSelect As Boolean
            Get
                Return _autoSelect
            End Get
            Set(value As Boolean)
                _autoSelect = value
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(GetType(AlignmentConstants), "LeftJustify")>
        Public Property Alignment As AlignmentConstants
            Get
                Select Case _edit.TextAlign
                    Case HorizontalAlignment.Right : Return AlignmentConstants.RightJustify
                    Case HorizontalAlignment.Center : Return AlignmentConstants.Center
                    Case Else : Return AlignmentConstants.LeftJustify
                End Select
            End Get
            Set(value As AlignmentConstants)
                Dim ha As HorizontalAlignment
                Select Case value
                    Case AlignmentConstants.RightJustify : ha = HorizontalAlignment.Right
                    Case AlignmentConstants.Center : ha = HorizontalAlignment.Center
                    Case Else : ha = HorizontalAlignment.Left
                End Select
                If _edit.TextAlign = ha Then Return
                _edit.TextAlign = ha
                RaiseEvent AlignmentChanged(Me, EventArgs.Empty)
                Invalidate()
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
        <DefaultValue(False)>
        Public Property Embed As Boolean
            Get
                Return _embed
            End Get
            Set(value As Boolean)
                If _embed = value Then Return
                _embed = value
                LayoutChildren()
                RaiseEvent EmbedChanged(Me, EventArgs.Empty)
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

        ''' <summary>Grid-row style active/inactive tint (VB6: ActiveControl) -- named RowActive, not
        ''' ActiveControl, for the same reason as TextBox.vb's RowActive: UserControl/ContainerControl
        ''' already has a differently-typed ActiveControl property this would otherwise shadow.</summary>
        <Browsable(False)>
        Public Property RowActive As Boolean
            Get
                Return _rowActive
            End Get
            Set(value As Boolean)
                _rowActive = value
                _edit.BackColor = If(value, _normalBackColor, ColorUtil.OleToColor(14737632))   ' gc_lngDeactiveBackColor
                If value Then
                    RaiseEvent Active(Me, EventArgs.Empty)
                Else
                    RaiseEvent Deactivate(Me, EventArgs.Empty)
                End If
            End Set
        End Property

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _edit.Enabled = Enabled
            Invalidate()
        End Sub

        Public Shadows Function Focus() As Boolean
            Return _edit.Focus()
        End Function

        '=====================================================================
        ' Layout (port of SetUserControlPosition)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            LayoutChildren()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(_obtuseness = ObtusenessMode.None OrElse Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(_obtuseness, Width, Height))
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Private Sub LayoutChildren()
            If _embed Then
                _edit.Visible = False
                Invalidate()
                Return
            End If

            Dim top As Integer = BorderInset + ShadowInset
            Dim left As Integer = BorderInset
            Dim w As Integer = Math.Max(0, Width - BorderInset * 2)
            Dim h As Integer = Math.Max(0, Height - BorderInset * 2 - ShadowInset)

            _edit.SetBounds(left, top, w, h)
            _edit.Visible = True
            Invalidate()
        End Sub

        '=====================================================================
        ' Painting (ports of DrawControlBorder / EmbedUserControl)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            If _embed Then
                Using b As New SolidBrush(BackColor)
                    g.FillRectangle(b, ClientRectangle)
                End Using
                Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis
                Select Case Alignment
                    Case AlignmentConstants.RightJustify : flags = flags Or TextFormatFlags.Right
                    Case AlignmentConstants.Center : flags = flags Or TextFormatFlags.HorizontalCenter
                    Case Else : flags = flags Or TextFormatFlags.Left
                End Select
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, flags)
            Else
                BorderPainter.DrawThemedBorder(g, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
            End If
        End Sub

        '=====================================================================
        ' Inner-control event relaying
        '=====================================================================
        Private Sub OnEditTextChanged(sender As Object, e As EventArgs)
            MyBase.OnTextChanged(EventArgs.Empty)
            If _embed Then Invalidate()
            If _autoTab AndAlso _edit.MaskFull AndAlso _edit.Focused Then SendKeys.Send("{TAB}")
        End Sub

        Private Sub OnEditEnter(sender As Object, e As EventArgs)
            _focused = True
            If _autoSelect Then
                _edit.SelectionStart = 0
                _edit.SelectionLength = _edit.Text.Length
            End If
            SoundUtil.PlaySound(_soundEnterFocus)
            Invalidate()
            MyBase.OnEnter(e)
            MyBase.OnGotFocus(e)
        End Sub

        Private Sub OnEditLeave(sender As Object, e As EventArgs)
            _focused = False
            SoundUtil.PlaySound(_soundExitFocus)
            Invalidate()
            MyBase.OnLeave(e)
            MyBase.OnLostFocus(e)
        End Sub

        ''' <summary>Port of MaskEdBox1_KeyDown: Enter always becomes Tab (not gated by AutoTab --
        ''' that property only controls the MaskFull-triggered advance in OnEditTextChanged above)
        ''' and, unlike every other key, is never relayed onward as a KeyDown event.</summary>
        Private Sub OnEditKeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode = Keys.Return Then
                e.Handled = True
                e.SuppressKeyPress = True
                SendKeys.Send("{TAB}")
            Else
                MyBase.OnKeyDown(e)
            End If
        End Sub

        ''' <summary>Port of MaskEdit.ctl's KeyAllowed/KeyNotAllowed/OnKeyPress trio. Control
        ''' characters (Backspace etc.) always pass through untouched. AllowDecimal/AllowDollarSigns
        ''' are also checked inside KeyNotAllowedChar, but since KeyAllowedChar (checked first, same
        ''' as the VB6 original) already matches and allows those two characters whenever those
        ''' flags are set, that inner check can never actually fire in practice -- kept anyway since
        ''' the goal here is a faithful behavioural port, not a rewrite.</summary>
        Private Sub OnEditKeyPress(sender As Object, e As KeyPressEventArgs)
            Dim ch As Char = e.KeyChar
            If AscW(ch) >= 32 Then
                Dim blocked As Boolean = False
                If KeyAllowedChar(ch, _allowedKeys) Then
                    ' explicitly allowed special character -- skip the block checks below
                ElseIf KeyNotAllowedChar(ch, _allowedKeys) Then
                    blocked = True
                ElseIf (_allowedKeys And EditAllowedKeys.AllowNumbers) <> 0 AndAlso Not Char.IsDigit(ch) Then
                    blocked = True
                End If

                If blocked Then
                    e.Handled = True
                    Return
                End If

                If (_allowedKeys And EditAllowedKeys.AllowLowercase) <> 0 Then
                    e.KeyChar = Char.ToLower(ch)
                ElseIf (_allowedKeys And EditAllowedKeys.AllowUppercase) <> 0 Then
                    e.KeyChar = Char.ToUpper(ch)
                End If
            End If
            MyBase.OnKeyPress(e)
        End Sub

        Private Shared Function KeyAllowedChar(ByVal ch As Char, ByVal allowed As EditAllowedKeys) As Boolean
            Select Case True
                Case (allowed And EditAllowedKeys.AllowAMPM) <> 0 AndAlso "AMP".IndexOf(Char.ToUpper(ch)) >= 0 : Return True
                Case (allowed And EditAllowedKeys.AllowColon) <> 0 AndAlso ch = ":"c : Return True
                Case (allowed And EditAllowedKeys.AllowDecimal) <> 0 AndAlso ch = "."c : Return True
                Case (allowed And EditAllowedKeys.AllowDollarSigns) <> 0 AndAlso ch = "$"c : Return True
                Case (allowed And EditAllowedKeys.AllowForwardSlash) <> 0 AndAlso ch = "/"c : Return True
                Case (allowed And EditAllowedKeys.AllowNegative) <> 0 AndAlso ch = "-"c : Return True
                Case (allowed And EditAllowedKeys.AllowParenthesis) <> 0 AndAlso "()".IndexOf(ch) >= 0 : Return True
                Case (allowed And EditAllowedKeys.AllowPounds) <> 0 AndAlso ch = "#"c : Return True
                Case (allowed And EditAllowedKeys.AllowSpaces) <> 0 AndAlso ch = " "c : Return True
                Case (allowed And EditAllowedKeys.AllowStars) <> 0 AndAlso ch = "*"c : Return True
                Case Else : Return False
            End Select
        End Function

        Private Shared Function KeyNotAllowedChar(ByVal ch As Char, ByVal allowed As EditAllowedKeys) As Boolean
            Select Case True
                Case (allowed And EditAllowedKeys.AllowNoDoubleQuotes) <> 0 AndAlso ch = """"c : Return True
                Case (allowed And EditAllowedKeys.AllowNoSingleQuotes) <> 0 AndAlso ch = "'"c : Return True
                Case (allowed And EditAllowedKeys.AllowNoSpaces) <> 0 AndAlso ch = " "c : Return True
                Case (allowed And EditAllowedKeys.AllowDecimal) <> 0 AndAlso ch = "."c : Return True
                Case (allowed And EditAllowedKeys.AllowDollarSigns) <> 0 AndAlso ch = "$"c : Return True
                Case Else : Return False
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
