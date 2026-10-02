Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.EditBox UserControl (Control\EditBox.ctl). Unlike TextBox.ctl/
' MultiLineTextBox.ctl (which just wrapped a plain VB.TextBox), EditBox.ctl raw-created its own
' native "Edit" window via CreateWindowEx -- that was only necessary in VB6 because VB6's own
' TextBox OCX was too limited; .NET's TextBox is already a full-featured managed wrapper over the
' exact same native Win32 Edit control, so this port is built the same composed way as TextBox.vb.
'
' Distinguishing features vs TextBox.vb: selectable BorderStyle chrome (only "Aqua" draws the
' themed border; the others map to native BorderStyle so plain flat/sunken edits are possible),
' character-class filtering (ValidChar/FormatText), a CustomFormat mask applied on blur, CaseType
' enforcement while typing, and Arrow/Enter-as-Tab navigation. Multiline+ScrollBars here are plain
' native scrollbars (EditBox.ctl never had MultiLineTextBox.ctl's themed Aqua.ScrollBar overlay).
Namespace Global.Aqua

    <DefaultEvent("TextChanged")>
    Public Class EditBox
        Inherits UserControl

        Private Const BorderInset As Integer = 4   ' gc_intKeepBorderSize
        Private Const ShadowInset As Integer = 1   ' gc_intTextBoxShadowHeight

        Private ReadOnly _edit As New System.Windows.Forms.TextBox()

        Private _borderStyle As EditBoxBorderStyle = EditBoxBorderStyle.Aqua
        Private _obtuseness As ObtusenessMode = ObtusenessMode.All
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _caseType As CaseType = CaseType.NoCase
        Private _formatText As EditBoxTextFormat = EditBoxTextFormat.NoFormat
        Private _validChar As String = ""
        Private _customFormat As String = ""
        Private _autoSelected As Boolean = False
        Private _arrowAsKeyTab As Boolean = False
        Private _enterAsKeyTab As Boolean = False
        Private _autoHScroll As Boolean = True
        Private _autoVScroll As Boolean = False
        Private _multiline As Boolean = False
        Private _scrollBars As ScrollBars = ScrollBars.None
        Private _focused As Boolean = False

        Public Event LockedChanged(sender As Object, e As EventArgs)
        Public Event AlignmentChanged(sender As Object, e As EventArgs)
        Public Event BorderStyleChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event CaseTypeChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            ' The native Edit control VB6's EditBox raw-created defaulted to the standard white
            ' text-entry background, independent of the outer UserControl's own ambient BackColor
            ' (system window-face grey, not white) -- without this, _edit inherits that grey by
            ' default and looks like a mismatched block instead of a normal white text field.
            MyBase.BackColor = SystemColors.Window

            _edit.BorderStyle = System.Windows.Forms.BorderStyle.None
            _edit.TabStop = True
            Controls.Add(_edit)

            AddHandler _edit.TextChanged, Sub(sender, e) MyBase.OnTextChanged(e)
            AddHandler _edit.Enter, AddressOf OnEditEnter
            AddHandler _edit.Leave, AddressOf OnEditLeave
            AddHandler _edit.Validating, Sub(sender, e) MyBase.OnValidating(e)
            AddHandler _edit.Validated, Sub(sender, e) MyBase.OnValidated(e)
            AddHandler _edit.KeyDown, AddressOf OnEditKeyDown
            AddHandler _edit.KeyPress, AddressOf OnEditKeyPress
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
            UpdateRegion()
            ApplyBorderStyle()
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
                _edit.Text = ApplyFormat(value)
            End Set
        End Property

        ''' <summary>See TextBox.vb's identical note: Font/BackColor/ForeColor must NOT be overridden
        ''' to read from _edit, since _edit's Parent is Me and that creates a Get/Get cycle the moment
        ''' either value is ambient -- pushed down via these change notifications instead.</summary>
        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            _edit.Font = Font
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnBackColorChanged(e As EventArgs)
            MyBase.OnBackColorChanged(e)
            _edit.BackColor = BackColor
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
            End Set
        End Property

        ''' <summary>Shadows UserControl.BorderStyle deliberately: this is the VB6 EditBoxBorderStyle
        ''' enum (which includes the Aqua themed chrome), not the native WinForms BorderStyle.</summary>
        <Category("外觀")>
        <DefaultValue(EditBoxBorderStyle.Aqua)>
        Public Shadows Property BorderStyle As EditBoxBorderStyle
            Get
                Return _borderStyle
            End Get
            Set(value As EditBoxBorderStyle)
                If _borderStyle = value Then Return
                _borderStyle = value
                ApplyBorderStyle()
                RaiseEvent BorderStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ObtusenessMode.All)>
        Public Property Obtuseness As ObtusenessMode
            Get
                Return _obtuseness
            End Get
            Set(value As ObtusenessMode)
                If _obtuseness = value Then Return
                _obtuseness = value
                UpdateRegion()
                Invalidate()
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

        ''' <summary>Scrolls to the last line (a log box): SelStart alone doesn't scroll a box without
        ''' the focus.</summary>
        Public Sub ScrollToEnd()
            Const WM_VSCROLL As Integer = &H115, SB_BOTTOM As Integer = 7
            If Not _edit.IsHandleCreated Then Return
            Dim m As Message = Message.Create(_edit.Handle, WM_VSCROLL, New IntPtr(SB_BOTTOM), IntPtr.Zero)
            Dim target As IWindowTarget = _edit.WindowTarget
            target.OnMessage(m)
        End Sub

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

        ''' <summary>Enforces character case while typing (VB6: CaseType -- read/written but never
        ''' actually applied in the original EditBox.ctl; this port makes it functional).</summary>
        <Category("行為")>
        <DefaultValue(CaseType.NoCase)>
        Public Property CaseType As CaseType
            Get
                Return _caseType
            End Get
            Set(value As CaseType)
                If _caseType = value Then Return
                _caseType = value
                _edit.CharacterCasing = ToCharacterCasing(value)
                RaiseEvent CaseTypeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Shared Function ToCharacterCasing(ByVal ct As CaseType) As CharacterCasing
            Select Case ct
                Case CaseType.UpperCase : Return CharacterCasing.Upper
                Case CaseType.LowerCase : Return CharacterCasing.Lower
                Case Else : Return CharacterCasing.Normal   ' NoCase and ProperCase (Proper is done in KeyPress)
            End Select
        End Function

        ''' <summary>Restricts typed characters to this set (VB6: ValidChar). Blank = no restriction.
        ''' NumericOnly/DateFormat FormatText override this with their own fixed set while active.</summary>
        <Category("行為")>
        <DefaultValue("")>
        Public Property ValidChar As String
            Get
                Return _validChar
            End Get
            Set(value As String)
                _validChar = If(value, "")
            End Set
        End Property

        ''' <summary>Selects which characters are accepted while typing (VB6: FormatText).</summary>
        <Category("行為")>
        <DefaultValue(EditBoxTextFormat.NoFormat)>
        Public Property FormatText As EditBoxTextFormat
            Get
                Return _formatText
            End Get
            Set(value As EditBoxTextFormat)
                _formatText = value
            End Set
        End Property

        ''' <summary>.NET numeric/date ToString format mask, applied on blur and whenever Text is set
        ''' in code (VB6: CustomFormat, applied via Format$ -- VB6's format tokens differ from .NET's,
        ''' so an existing VB6 CustomFormat string may need adjusting when porting a form over).</summary>
        <Category("行為")>
        <DefaultValue("")>
        Public Property CustomFormat As String
            Get
                Return _customFormat
            End Get
            Set(value As String)
                _customFormat = If(value, "")
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property AutoSelected As Boolean
            Get
                Return _autoSelected
            End Get
            Set(value As Boolean)
                If _multiline Then Return   ' VB6: no-op for multiline
                _autoSelected = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property ArrowAsKeyTab As Boolean
            Get
                Return _arrowAsKeyTab
            End Get
            Set(value As Boolean)
                _arrowAsKeyTab = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property EnterAsKeyTab As Boolean
            Get
                Return _enterAsKeyTab
            End Get
            Set(value As Boolean)
                _enterAsKeyTab = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(True)>
        Public Property AutoHScroll As Boolean
            Get
                Return _autoHScroll
            End Get
            Set(value As Boolean)
                If _autoHScroll = value Then Return
                _autoHScroll = value
                ApplyEditMode()
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property AutoVScroll As Boolean
            Get
                Return _autoVScroll
            End Get
            Set(value As Boolean)
                If Not _multiline Then Return   ' VB6: no-op unless multiline
                _autoVScroll = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property Multiline As Boolean
            Get
                Return _multiline
            End Get
            Set(value As Boolean)
                If _multiline = value Then Return
                _multiline = value
                ApplyEditMode()
            End Set
        End Property

        ''' <summary>Plain native scrollbars (VB6 EditBox never themed these, unlike MultiLineTextBox.ctl).</summary>
        <Category("外觀")>
        <DefaultValue(GetType(ScrollBars), "None")>
        Public Property ScrollBars As ScrollBars
            Get
                Return _scrollBars
            End Get
            Set(value As ScrollBars)
                If _scrollBars = value Then Return
                _scrollBars = value
                ApplyEditMode()
            End Set
        End Property

        Public Shadows Function Focus() As Boolean
            Return _edit.Focus()
        End Function

        '=====================================================================
        ' Layout / region / native style (ports of ResizeUserControl, RegionUserControl, InitializeControl)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            LayoutChildren()
        End Sub

        Private Sub ApplyBorderStyle()
            Select Case _borderStyle
                Case EditBoxBorderStyle.NoBorder
                    _edit.BorderStyle = System.Windows.Forms.BorderStyle.None
                Case EditBoxBorderStyle.FixedSingle
                    _edit.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
                Case EditBoxBorderStyle.Normal, EditBoxBorderStyle.Raised, EditBoxBorderStyle.Bumped
                    _edit.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D   ' Raised/Bumped approximated: no direct .NET equivalent
                Case Else ' Aqua
                    _edit.BorderStyle = System.Windows.Forms.BorderStyle.None
            End Select
            UpdateRegion()
            LayoutChildren()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(_borderStyle <> EditBoxBorderStyle.Aqua OrElse _obtuseness = ObtusenessMode.None OrElse Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(_obtuseness, Width, Height))
            If old IsNot Nothing Then old.Dispose()
            ' SetWindowRgn on a child doesn't itself make the parent repaint the corner pixels the
            ' new (smaller) region just exposed -- without this they can be left showing whatever was
            ' there before (stale/garbage), instead of the parent's real current background.
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Private Sub LayoutChildren()
            If _borderStyle = EditBoxBorderStyle.Aqua Then
                Dim top As Integer = BorderInset + ShadowInset
                Dim left As Integer = BorderInset
                Dim w As Integer = Math.Max(0, Width - BorderInset * 2)
                Dim h As Integer = Math.Max(0, Height - BorderInset * 2 - ShadowInset)
                _edit.SetBounds(left, top, w, h)
            Else
                _edit.SetBounds(0, 0, Width, Height)
            End If
            Invalidate()
        End Sub

        ''' <summary>Port of the m_bolMultiline/m_enumScrollBars -&gt; native style flags translation in InitializeControl.</summary>
        Private Sub ApplyEditMode()
            _edit.Multiline = _multiline
            _edit.ScrollBars = If(_multiline, _scrollBars, ScrollBars.None)
            Dim wantH As Boolean = _multiline AndAlso (_scrollBars = ScrollBars.Horizontal OrElse _scrollBars = ScrollBars.Both)
            _edit.WordWrap = If(_multiline, Not wantH, Not _autoHScroll)
        End Sub

        '=====================================================================
        ' Painting (ports of DrawControlBorder / DrawControlParhelia -- Aqua style only)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If _borderStyle = EditBoxBorderStyle.Aqua Then
                BorderPainter.DrawThemedBorder(e.Graphics, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
            End If
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Private Sub OnEditEnter(sender As Object, e As EventArgs)
            _focused = True
            If _autoSelected AndAlso Not _multiline Then _edit.SelectAll()
            If _borderStyle = EditBoxBorderStyle.Aqua Then Invalidate()
            MyBase.OnEnter(e)
        End Sub

        Private Sub OnEditLeave(sender As Object, e As EventArgs)
            _focused = False
            ApplyCustomFormatOnBlur()
            If _borderStyle = EditBoxBorderStyle.Aqua Then Invalidate()
            MyBase.OnLeave(e)
        End Sub

        ''' <summary>Port of the WM_CHAR ValidChar/FormatText filter and ProperCase enforcement.</summary>
        Private Sub OnEditKeyPress(sender As Object, e As KeyPressEventArgs)
            If Not Char.IsControl(e.KeyChar) Then
                Dim allowed As String = EffectiveValidChar()
                If allowed.Length > 0 AndAlso allowed.IndexOf(e.KeyChar) < 0 Then
                    e.Handled = True
                    Return
                End If
                If _caseType = CaseType.ProperCase Then
                    Dim atWordStart As Boolean = _edit.SelectionStart = 0
                    If Not atWordStart Then
                        Dim prev As Char = _edit.Text(Math.Max(0, _edit.SelectionStart - 1))
                        atWordStart = Char.IsWhiteSpace(prev)
                    End If
                    e.KeyChar = If(atWordStart, Char.ToUpperInvariant(e.KeyChar), Char.ToLowerInvariant(e.KeyChar))
                End If
            End If
            MyBase.OnKeyPress(e)
        End Sub

        Private Function EffectiveValidChar() As String
            Select Case _formatText
                Case EditBoxTextFormat.NumericOnly : Return "0123456789"
                Case EditBoxTextFormat.DateFormat : Return "/0123456789"
                Case Else : Return _validChar   ' NoFormat / CustomFormat: whatever ValidChar was set to
            End Select
        End Function

        ''' <summary>Port of Text1_KeyDown: optional Arrow/Enter-as-Tab field navigation.</summary>
        Private Sub OnEditKeyDown(sender As Object, e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Dim p As Control = Parent
            If p Is Nothing Then Return
            If _enterAsKeyTab AndAlso e.KeyCode = Keys.Enter Then
                p.SelectNextControl(Me, True, True, True, True)
            ElseIf _arrowAsKeyTab AndAlso Not _multiline Then
                Select Case e.KeyCode
                    Case Keys.Right, Keys.Down : p.SelectNextControl(Me, True, True, True, True)
                    Case Keys.Left, Keys.Up : p.SelectNextControl(Me, False, True, True, True)
                End Select
            End If
        End Sub

        ''' <summary>Port of the WM_KILLFOCUS reformat: applies CustomFormat as a date or numeric ToString mask.</summary>
        Private Sub ApplyCustomFormatOnBlur()
            _edit.Text = ApplyFormat(_edit.Text)
        End Sub

        Private Function ApplyFormat(ByVal value As String) As String
            If String.IsNullOrEmpty(_customFormat) Then Return value
            Try
                If LooksLikeDateFormat(_customFormat) Then
                    Dim dt As DateTime
                    If DateTime.TryParse(value, dt) Then Return dt.ToString(_customFormat)
                Else
                    Dim d As Double
                    If Double.TryParse(value, d) Then Return d.ToString(_customFormat)
                End If
            Catch
                ' invalid format string / unparsable text: leave the text untouched, matching the
                ' VB6 On Error Resume Next that wrapped its equivalent Format$ calls
            End Try
            Return value
        End Function

        Private Shared Function LooksLikeDateFormat(ByVal fmt As String) As Boolean
            For Each ch As Char In "yMdHms"
                If fmt.IndexOf(ch) >= 0 Then Return True
            Next
            Return False
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
