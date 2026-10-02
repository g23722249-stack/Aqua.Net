Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Unified port of the VB6 Aqua.TextBox (Control\TextBox.ctl) AND Aqua.MultiLineTextBox
' (Control\MultiLineTextBox.ctl), which were two separate UserControls in VB6 because a
' native VB6 TextBox couldn't be reskinned once MultiLine + native scrollbars were turned on.
' Here a single control exposes Multiline/ScrollBars (matching System.Windows.Forms.TextBox's
' own API) and switches behaviour at runtime:
'   - hosts a real child System.Windows.Forms.TextBox for actual text editing/caret/IME/etc.
'   - paints the Aqua 3-line themed border + rounded (Obtuseness) region + focus "Parhelia" glow
'     around it (BorderPainter/RegionUtil, both already used by other ported controls)
'   - when Multiline and ScrollBars requests Vertical/Horizontal/Both, the inner TextBox keeps
'     its real native scrollbars (so real scrolling/mouse-wheel/keyboard nav all keep working)
'     but an Aqua.ScrollBar is laid exactly on top of each one to theme it, synced through
'     EM_LINESCROLL / GetScrollInfo (Internal\NativeEdit.vb) exactly like the VB6 control did.
'   - Embed hides the inner TextBox and paints flat text instead (VB6: used to show a TextBox's
'     content inline in e.g. a grid cell without the editable chrome).
Namespace Global.Aqua

    <DefaultEvent("TextChanged")>
    Public Class TextBox
        ' UserControl (not plain Control): the WinForms designer's ControlDesigner does not expect a
        ' plain Control to host real child controls added in its own constructor (Controls.Add on _edit/
        ' _vScroll/_hScroll below) -- dropping such a control onto a form hangs/crashes the designer.
        ' UserControl/ContainerControl is the composition base WinForms actually expects for this.
        Inherits UserControl

        Private Const BorderInset As Integer = 4   ' gc_intKeepBorderSize
        Private Const ShadowInset As Integer = 1   ' gc_intTextBoxShadowHeight
        Private Const ScrollThickness As Integer = 18   ' matches Aqua.ScrollBar's own thumb thickness

        Private ReadOnly _edit As New System.Windows.Forms.TextBox()
        Private ReadOnly _vScroll As New Aqua.ScrollBar()
        Private ReadOnly _hScroll As New Aqua.ScrollBar()

        Private _multiline As Boolean = False
        Private _scrollBars As ScrollBars = ScrollBars.None
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _embed As Boolean = False
        Private _autoSelect As Boolean = False
        Private _rowActive As Boolean = True
        Private _themeColor As ColorConstants = ColorConstants.Blue
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _soundEnterFocus As String = ""
        Private _soundExitFocus As String = ""
        Private _focused As Boolean = False
        Private _normalBackColor As Color = SystemColors.Window
        Private _syncingScroll As Boolean = False
        Private _valid As Boolean = True   ' VB6 m_bolValid: False once the user edits, until Validation passes
        Private _dropEffect As DragDropEffects = DragDropEffects.None

        ''' <summary>Raised when focus moves into / out of the text box (VB6: EnterFocus/ExitFocus).</summary>
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)
        ''' <summary>Raised as focus leaves, only if the text was edited since it was last set in code or
        ''' last validated (VB6: Validation(Cancel)). e.Cancel = True keeps focus in the text box.</summary>
        Public Event Validation(sender As Object, e As CancelEventArgs)
        Public Event LockedChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event EmbedChanged(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event AlignmentChanged(sender As Object, e As EventArgs)
        Public Event Active(sender As Object, e As EventArgs)
        Public Event Deactivate(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            ' VB6's TextBox.ctl hardcoded Text1.BackColor to pure white, independent of the outer
            ' UserControl's own ambient BackColor (system window-face grey, not white) -- without
            ' this, _edit inherits that grey by default and looks like a mismatched block instead
            ' of a normal white text field.
            MyBase.BackColor = SystemColors.Window

            _edit.BorderStyle = BorderStyle.None
            _edit.TabStop = True
            _edit.ScrollBars = ScrollBars.None
            Controls.Add(_edit)

            ' Aqua.ScrollBar's own constructor starts Enabled=False (it expects a host to activate
            ' it, matching the VB6 original). Since Me.Enabled defaults to True and OnEnabledChanged
            ' only fires on an actual change, that sync never ran without this explicit initial push
            ' -- leaving the overlay permanently disabled (inert to all mouse input) whenever the
            ' consumer never happens to toggle TextBox.Enabled themselves.
            _vScroll.Orientation = OrientationMode.Vertical
            _vScroll.Visible = False
            _vScroll.Enabled = True
            Controls.Add(_vScroll)

            _hScroll.Orientation = OrientationMode.Horizontal
            _hScroll.Visible = False
            _hScroll.Enabled = True
            Controls.Add(_hScroll)

            AddHandler _edit.TextChanged, AddressOf OnEditTextChanged
            AddHandler _edit.Enter, AddressOf OnEditEnter
            AddHandler _edit.Leave, AddressOf OnEditLeave
            AddHandler _edit.Validating, AddressOf OnEditValidating
            AddHandler _edit.Validated, Sub(sender, e) MyBase.OnValidated(e)
            AddHandler _edit.KeyDown, Sub(sender, e) MyBase.OnKeyDown(e)
            AddHandler _edit.KeyPress, Sub(sender, e) MyBase.OnKeyPress(e)
            AddHandler _edit.KeyUp, AddressOf OnEditKeyUp
            AddHandler _edit.Click, Sub(sender, e) MyBase.OnClick(e)
            AddHandler _edit.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler _edit.MouseDown, AddressOf OnEditMouseDown
            AddHandler _edit.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler _edit.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))
            AddHandler _edit.MouseEnter, Sub(sender, e) MyBase.OnMouseEnter(e)
            AddHandler _edit.MouseLeave, Sub(sender, e) MyBase.OnMouseLeave(e)
            AddHandler _edit.MouseWheel, AddressOf OnEditMouseWheel
            ' VB6 OLEDropMode=Manual + OLEDragDrop -> AllowDrop + the standard Drag* events, raised
            ' on this control whether the drop lands on the inner edit or on the border.
            AddHandler _edit.DragEnter, Sub(sender, e) OnDragEnter(e)
            AddHandler _edit.DragOver, Sub(sender, e) OnDragOver(e)
            AddHandler _edit.DragLeave, Sub(sender, e) OnDragLeave(e)
            AddHandler _edit.DragDrop, Sub(sender, e) OnDragDrop(e)
            ' SyncScrollBars() no-ops while _edit.IsHandleCreated is False (the common case: Multiline/
            ' ScrollBars/Text are usually all set before the control is ever shown). Nothing else
            ' re-triggers a sync once the handle finally exists, so the overlay would otherwise stay
            ' stuck at its construction-time Min=Max=1 ("nothing to scroll") forever.
            AddHandler _edit.HandleCreated, Sub(sender, e) SyncScrollBars()

            AddHandler _vScroll.Scroll, AddressOf OnVScrollScroll
            AddHandler _hScroll.Scroll, AddressOf OnHScrollScroll

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
        ' Passthrough properties (proxy the inner native TextBox)
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
                _valid = True   ' text set in code never needs Validation (VB6 Text Let)
            End Set
        End Property

        ''' <summary>False once the user has edited the text and it hasn't passed Validation yet
        ''' (VB6: Valid). Set it False to force Validation on the next focus exit.</summary>
        <Browsable(False)>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Valid As Boolean
            Get
                Return _valid
            End Get
            Set(value As Boolean)
                _valid = value
            End Set
        End Property

        ''' <summary>Accept drops (VB6: OLEDropMode = Manual); handle DragDrop to take the data.
        ''' Passed through to the inner edit, where most drops actually land.</summary>
        Public Overrides Property AllowDrop As Boolean
            Get
                Return MyBase.AllowDrop
            End Get
            Set(value As Boolean)
                MyBase.AllowDrop = value
                _edit.AllowDrop = value
            End Set
        End Property

        ''' <summary>
        ''' Font/BackColor/ForeColor are deliberately NOT overridden as properties here. _edit's
        ''' Parent is Me, so a Get that reads straight from _edit (as this used to) creates a cycle
        ''' the moment _edit's own value is ambient/unset: resolving it walks up to Parent.Font,
        ''' i.e. Me.Font, whose Get pointed straight back at _edit.Font -- infinite recursion ->
        ''' stack overflow, which is what was crashing/hanging the VS designer the instant TextBox
        ''' was dropped onto a form. Leaving Font/BackColor/ForeColor as Me's own normal (inherited)
        ''' Control properties lets ambient resolution walk up to the real parent as usual; these
        ''' On*Changed overrides just push the resolved value down to _edit whenever it changes.
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
        <DefaultValue(False)>
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

        '=====================================================================
        ' Structural properties: Multiline / ScrollBars / Obtuseness / Embed
        '=====================================================================
        <Category("行為")>
        <DefaultValue(False)>
        Public Property Multiline As Boolean
            Get
                Return _multiline
            End Get
            Set(value As Boolean)
                If _multiline = value Then Return
                _multiline = value
                LayoutChildren()
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(GetType(ScrollBars), "None")>
        Public Property ScrollBars As ScrollBars
            Get
                Return _scrollBars
            End Get
            Set(value As ScrollBars)
                If _scrollBars = value Then Return
                _scrollBars = value
                LayoutChildren()
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

        ''' <summary>Aqua.ScrollBar accent colour used for the multiline scrollbar overlay (VB6: Color).</summary>
        <Category("外觀")>
        <DefaultValue(ColorConstants.Blue)>
        Public Property Color As ColorConstants
            Get
                Return _themeColor
            End Get
            Set(value As ColorConstants)
                If _themeColor = value Then Return
                _themeColor = value
                _vScroll.Color = value
                _hScroll.Color = value
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
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

        ''' <summary>
        ''' Grid-row style active/inactive tint (VB6: ActiveControl). Named RowActive, not ActiveControl,
        ''' because UserControl/ContainerControl already has a differently-typed ActiveControl property
        ''' (the child control holding keyboard focus) -- an unrelated concept this would otherwise shadow.
        ''' </summary>
        <Browsable(False)>
        Public Property RowActive As Boolean
            Get
                Return _rowActive
            End Get
            Set(value As Boolean)
                _rowActive = value
                _edit.BackColor = If(value, _normalBackColor, ColorUtil.OleToColor(14737632))  ' gc_lngDeactiveBackColor
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
            If Enabled Then
                SyncScrollBars()   ' re-enabling: only actually enable each bar if it still has something to scroll
            Else
                _vScroll.Enabled = False
                _hScroll.Enabled = False
            End If
            Invalidate()
        End Sub

        Protected Overrides Sub OnCausesValidationChanged(e As EventArgs)
            MyBase.OnCausesValidationChanged(e)
            _edit.CausesValidation = CausesValidation
        End Sub

        Public Shadows Function Focus() As Boolean
            Return _edit.Focus()
        End Function

        '=====================================================================
        ' Layout (ports of SetUserControlPosition, both variants)
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
            ' SetWindowRgn on a child doesn't itself make the parent repaint the corner pixels the
            ' new (smaller) region just exposed -- without this they can be left showing whatever was
            ' there before (stale/garbage), instead of the parent's real current background.
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Private Sub LayoutChildren()
            If _embed Then
                _edit.Visible = False
                _vScroll.Visible = False
                _hScroll.Visible = False
                Invalidate()
                Return
            End If

            ApplyEditMode()

            Dim showV As Boolean = _multiline AndAlso (_scrollBars = ScrollBars.Vertical OrElse _scrollBars = ScrollBars.Both)
            Dim showH As Boolean = _multiline AndAlso (_scrollBars = ScrollBars.Horizontal OrElse _scrollBars = ScrollBars.Both)

            Dim top As Integer = BorderInset + ShadowInset
            Dim left As Integer = BorderInset
            Dim w As Integer = Math.Max(0, Width - BorderInset * 2)
            Dim h As Integer = Math.Max(0, Height - BorderInset * 2 - ShadowInset)

            _edit.SetBounds(left, top, w, h)
            _edit.Visible = True

            _vScroll.Visible = showV
            If showV Then
                _vScroll.SetBounds(left + w - ScrollThickness, top, ScrollThickness, h - If(showH, ScrollThickness, 0))
            End If

            _hScroll.Visible = showH
            If showH Then
                _hScroll.SetBounds(left, top + h - ScrollThickness, w - If(showV, ScrollThickness, 0), ScrollThickness)
            End If

            _edit.BringToFront()
            If showV Then _vScroll.BringToFront()
            If showH Then _hScroll.BringToFront()

            SyncScrollBars()
            Invalidate()
        End Sub

        ''' <summary>Ports the m_bolMultiline/m_enumScrollBars -&gt; native style flags translation in InitializeControl.</summary>
        Private Sub ApplyEditMode()
            _edit.Multiline = _multiline
            _edit.WordWrap = Not (_multiline AndAlso (_scrollBars = ScrollBars.Horizontal OrElse _scrollBars = ScrollBars.Both))
            _edit.ScrollBars = If(_multiline, _scrollBars, ScrollBars.None)
        End Sub

        '=====================================================================
        ' Painting (ports of DrawControlBorder / DrawControlParhelia / EmbedUserControl)
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
            _valid = False   ' the Text setter sets it back to True right after this for code changes
            SyncScrollBars()
            MyBase.OnTextChanged(EventArgs.Empty)
            If _embed Then Invalidate()
        End Sub

        Private Sub OnEditEnter(sender As Object, e As EventArgs)
            _focused = True
            If _autoSelect Then _edit.SelectAll()
            PlaySoundFile(_soundEnterFocus)
            Invalidate()
            MyBase.OnEnter(e)
            MyBase.OnGotFocus(e)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Private Sub OnEditLeave(sender As Object, e As EventArgs)
            _focused = False
            PlaySoundFile(_soundExitFocus)
            Invalidate()
            MyBase.OnLeave(e)
            MyBase.OnLostFocus(e)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        ''' <summary>Port of Text1_Validate: Validation fires only for unvalidated user edits;
        ''' cancelling it cancels the inner edit's Validating, which keeps focus there.</summary>
        Private Sub OnEditValidating(sender As Object, e As CancelEventArgs)
            MyBase.OnValidating(e)
            If e.Cancel OrElse _valid Then Return
            RaiseEvent Validation(Me, e)
            If Not e.Cancel Then _valid = True
        End Sub

        ''' <summary>VB6 manual OLE drop accepted the drag unless OLEDragOver said otherwise; .NET
        ''' refuses it unless DragEnter sets an Effect. Default to Copy (or whatever the source
        ''' allows) before the DragEnter handler runs, which can still change or clear it.</summary>
        Protected Overrides Sub OnDragEnter(e As DragEventArgs)
            If e.Effect = DragDropEffects.None Then
                e.Effect = If((e.AllowedEffect And DragDropEffects.Copy) <> 0, DragDropEffects.Copy, e.AllowedEffect)
            End If
            MyBase.OnDragEnter(e)
            _dropEffect = e.Effect
        End Sub

        Protected Overrides Sub OnDragOver(e As DragEventArgs)
            If e.Effect = DragDropEffects.None Then e.Effect = _dropEffect
            MyBase.OnDragOver(e)
        End Sub

        Private Sub OnEditKeyUp(sender As Object, e As KeyEventArgs)
            SyncScrollBars()
            MyBase.OnKeyUp(e)
        End Sub

        Private Sub OnEditMouseDown(sender As Object, e As MouseEventArgs)
            SyncScrollBars()
            MyBase.OnMouseDown(e)
        End Sub

        Private Sub OnEditMouseWheel(sender As Object, e As MouseEventArgs)
            SyncScrollBars()
            MyBase.OnMouseWheel(e)
        End Sub

        Private Sub PlaySoundFile(ByVal path As String)
            SoundUtil.PlaySound(path)
        End Sub

        '=====================================================================
        ' Aqua.ScrollBar overlay <-> native Edit control scroll-position sync
        '=====================================================================
        Private Sub SyncScrollBars()
            If _syncingScroll OrElse Not _multiline OrElse Not _edit.IsHandleCreated Then Return
            _syncingScroll = True
            Try
                If _vScroll.Visible Then ApplyScrollState(_vScroll, NativeEdit.GetScrollState(_edit.Handle, NativeEdit.SB_VERT))
                If _hScroll.Visible Then ApplyScrollState(_hScroll, NativeEdit.GetScrollState(_edit.Handle, NativeEdit.SB_HORZ))
            Finally
                _syncingScroll = False
            End Try
        End Sub

        ''' <summary>Port of SetScrollBarRange: also disables the bar when there's nothing to scroll
        ''' on that axis (content fits within the view), not just when the whole TextBox is disabled.</summary>
        Private Sub ApplyScrollState(ByVal bar As Aqua.ScrollBar, ByVal si As NativeEdit.SCROLLINFO)
            Dim pageSpan As Integer = Math.Max(si.nPage - 1, 0)
            Dim maxV As Integer = Math.Max(si.nMin, si.nMax - pageSpan)
            bar.Minimum = si.nMin
            bar.Maximum = maxV
            bar.LargeChange = Math.Max(1, si.nPage)
            Dim v As Integer = si.nPos
            If v < bar.Minimum Then v = bar.Minimum
            If v > bar.Maximum Then v = bar.Maximum
            bar.Value = v
            bar.Enabled = Enabled AndAlso maxV > si.nMin
        End Sub

        Private Sub OnVScrollScroll(sender As Object, e As EventArgs)
            If _syncingScroll Then Return
            Dim si = NativeEdit.GetScrollState(_edit.Handle, NativeEdit.SB_VERT)
            Dim delta As Integer = _vScroll.Value - si.nPos
            If delta <> 0 Then NativeEdit.LineScroll(_edit.Handle, 0, delta)
            _edit.Focus()
        End Sub

        Private Sub OnHScrollScroll(sender As Object, e As EventArgs)
            If _syncingScroll Then Return
            Dim si = NativeEdit.GetScrollState(_edit.Handle, NativeEdit.SB_HORZ)
            Dim delta As Integer = _hScroll.Value - si.nPos
            If delta <> 0 Then NativeEdit.LineScroll(_edit.Handle, delta, 0)
            _edit.Focus()
        End Sub

        '=====================================================================
        ' File I/O (VB6 LoadFile/SaveFile round-tripped through a RichTextBox purely for plain-text
        ' encoding; File.ReadAllText/WriteAllText already handle that directly in .NET)
        '=====================================================================
        Public Function LoadFile(ByVal fileName As String) As Boolean
            Try
                Text = IO.File.ReadAllText(fileName)
                Return True
            Catch
                Return False
            End Try
        End Function

        Public Function SaveFile(ByVal fileName As String) As Boolean
            Try
                IO.File.WriteAllText(fileName, Text)
                Return True
            Catch
                Return False
            End Try
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
