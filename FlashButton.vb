Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Port of the VB6 Aqua.Button UserControl (Control\Button.ctl) -- renamed FlashButton here to
' avoid colliding with the pre-existing, unrelated ExamControls.Button in this project. A single
' pill-shaped button with Normal/Hover/Click/Disabled skin art plus a distinctive idle animation:
' while focused or hovered (and Flash=True), it plays a looping 12-frame "light sweep" animation
' forward, pauses, drops back to plain Hover art, then sweeps backward -- and fades out through the
' same frames when focus/hover ends. Owner-drawn Control (no children), same pattern as
' ImageButton.vb/Slider.vb/ProgressBar.vb.
'
' Not ported: VB6's "Parhelia" glow-on-focus (DrawControlParhelia/ShowParhelia) drew an outline on
' the *parent* AquaForm's surface via a container-specific interface this WinForms port has no
' equivalent host for; there is no AquaForm here for it to hook into.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class FlashButton
        Inherits Control

        Private Enum FlashButtonVisualState
            Normal
            Disable
            Hover
            Click
        End Enum

        Private _color As ColorConstants = ColorConstants.Blue
        Private _hue As Integer = 0
        Private _saturation As Integer = 0
        Private _luminosity As Integer = 0
        Private _hoverInterval As Integer = 100
        Private _flash As Boolean = True
        Private _autoSize As Boolean = True

        Private _mouseEnter As Boolean = False
        Private _hover As Boolean = False
        Private _focused As Boolean = False
        Private _flashIndex As Integer = 0
        Private _flashForward As Boolean = True

        Private _currentSurface As Image
        Private _ownedTint As Bitmap
        Private _pendingClickArgs As EventArgs

        Private ReadOnly _flashTimer As New Timer()
        Private ReadOnly _hoverTimer As New Timer()
        Private ReadOnly _pressTimer As New Timer()
        Private ReadOnly _clickRevertTimer As New Timer()
        Private _pressButton As Integer
        Private _pressShift As Integer

        Private _soundClick, _soundMouseEnter, _soundMouseHover, _soundMouseLeave, _soundEnterFocus, _soundExitFocus As String

        Public Event MousePress(button As Integer, shift As Integer)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Shadows Event AutoSizeChanged(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)
        ''' <summary>Shadows Control's built-in MouseHover: VB6 gated this on a configurable
        ''' HoverInterval timer (default 100ms here) rather than the OS's fixed hover delay.</summary>
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.Selectable, True)
            Text = "Button"
            _pressTimer.Interval = 1000
            AddHandler _pressTimer.Tick, AddressOf OnPressTick
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick
            AddHandler _flashTimer.Tick, AddressOf OnFlashTick
            AddHandler _clickRevertTimer.Tick, AddressOf OnClickRevertTick
            UpdateRegion()
            RefreshBaseState()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Dim m As Image = FlashButtonResources.GetMask()
                Return New Size(If(m IsNot Nothing, m.Width, 81), NaturalHeight())
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("外觀")>
        <DefaultValue(ColorConstants.Blue)>
        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                RefreshBaseState()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>VB6 quirk preserved: these three HSL-tint offsets take effect on the next
        ''' redraw the control was going to do anyway (hover/click/flash), not immediately --
        ''' VB6's own Let accessors never called DrawUserControl/DrawBackground here.</summary>
        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfHue As Integer
            Get
                Return _hue
            End Get
            Set(value As Integer)
                _hue = value
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfSaturation As Integer
            Get
                Return _saturation
            End Get
            Set(value As Integer)
                _saturation = value
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfLuminosity As Integer
            Get
                Return _luminosity
            End Get
            Set(value As Integer)
                _luminosity = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(100)>
        Public Property HoverInterval As Integer
            Get
                Return _hoverInterval
            End Get
            Set(value As Integer)
                _hoverInterval = value
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(True)>
        Public Property Flash As Boolean
            Get
                Return _flash
            End Get
            Set(value As Boolean)
                _flash = value
            End Set
        End Property

        ''' <summary>When True (default), Height is locked to the button art's natural height --
        ''' same fixed-height pattern as Loading.vb. Only Width stays adjustable.</summary>
        <Category("外觀")>
        <DefaultValue(True)>
        Public Shadows Property AutoSize As Boolean
            Get
                Return _autoSize
            End Get
            Set(value As Boolean)
                If _autoSize = value Then Return
                _autoSize = value
                If _autoSize Then Height = NaturalHeight()
                RaiseEvent AutoSizeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        Public Property SoundFileOfClick As String
            Get
                Return _soundClick
            End Get
            Set(value As String)
                _soundClick = value
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
        Public Property SoundFileOfMouseHover As String
            Get
                Return _soundMouseHover
            End Get
            Set(value As String)
                _soundMouseHover = value
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

        Private Function NaturalHeight() As Integer
            Dim m As Image = FlashButtonResources.GetMask()
            Return If(m IsNot Nothing, m.Height, 27)
        End Function

        '=====================================================================
        ' Fixed height (AutoSize) + capsule-shaped region
        '=====================================================================
        Protected Overrides Sub SetBoundsCore(x As Integer, y As Integer, width As Integer, height As Integer, specified As BoundsSpecified)
            Dim h As Integer = If(_autoSize, NaturalHeight(), height)
            MyBase.SetBoundsCore(x, y, width, h, specified)
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            Invalidate()
        End Sub

        ''' <summary>Port of RegionUserControl. Two circular-arc radius guesses (Height\2, then a
        ''' pixel-measured ~10px) both missed the actual shape in the real running app -- rather
        ''' than guess a third radius, this now does what VB6 itself did: 3-slice horizontal-stretch
        ''' the real fb_imgButtonMask bitmap to the control's exact size (same Skin.DrawStretch used
        ''' for the button surface art) and build the Region directly from ITS black/white pixels,
        ''' one rectangle per row's contiguous non-black run. No approximation left to get wrong.</summary>
        Private Sub UpdateRegion()
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim mask As Image = FlashButtonResources.GetMask()
            If mask Is Nothing Then Return

            Dim old As Region = Me.Region
            Using stretched As New Bitmap(Width, Height)
                Using g As Graphics = Graphics.FromImage(stretched)
                    g.Clear(System.Drawing.Color.Black)
                    Skin.DrawStretch(g, mask, New Rectangle(0, 0, Width, Height), horizontal:=True)
                End Using
                Me.Region = RegionFromMask(stretched)
            End Using
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        ''' <summary>Builds a Region from a black/white mask bitmap: one rectangle per row's
        ''' contiguous run of non-black pixels (a pill/stadium mask has exactly one run per row, so
        ''' this stays cheap). Mirrors VB6's Quartz.Region.CreateFromPicture(mask, TransparencyColor
        ''' :=vbBlack) -- black pixels excluded, everything else kept.</summary>
        Private Shared Function RegionFromMask(ByVal mask As Bitmap) As Region
            Dim path As New GraphicsPath()
            Dim w As Integer = mask.Width, h As Integer = mask.Height
            For y = 0 To h - 1
                Dim left As Integer = -1, right As Integer = -1
                For x = 0 To w - 1
                    Dim c As Color = mask.GetPixel(x, y)
                    Dim isBlack As Boolean = c.R < 128 AndAlso c.G < 128 AndAlso c.B < 128
                    If Not isBlack Then
                        If left = -1 Then left = x
                        right = x
                    End If
                Next
                If left >= 0 Then path.AddRectangle(New Rectangle(left, y, right - left + 1, 1))
            Next
            Dim result As New Region(path)
            path.Dispose()
            Return result
        End Function

        '=====================================================================
        ' Painting -- state art is resolved eagerly by DrawBackground/DrawFlashBackground (ports of
        ' the VB6 methods of the same name) into _currentSurface; OnPaint just draws whatever that
        ' currently is, mirroring VB6's immediate-mode UserControl.Picture assignment.
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            If _currentSurface IsNot Nothing Then Skin.DrawStretch(g, _currentSurface, ClientRectangle, horizontal:=True)
            If Text.Length > 0 Then
                Dim fc As Color = If(Enabled, ForeColor, ColorUtil.OleToColor(12435133))   ' gc_lngDisableForeColor RGB(189,190,189)
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fc, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            End If
        End Sub

        ''' <summary>Port of DrawUserControl/DrawBackground(ecsNormal/ecsDisable/ecsHover/ecsClick).
        ''' Hover/Click art gets the HSL tint; Normal/Disable never does (matches VB6 exactly).</summary>
        Private Sub DrawBackground(ByVal state As FlashButtonVisualState)
            Dim raw As Image
            Dim tint As Boolean = False
            Select Case state
                Case FlashButtonVisualState.Disable
                    raw = FlashButtonResources.GetDisabled()
                Case FlashButtonVisualState.Hover
                    raw = FlashButtonResources.GetHover(_color)
                    tint = True
                Case FlashButtonVisualState.Click
                    raw = FlashButtonResources.GetClick(_color)
                    tint = True
                Case Else
                    raw = FlashButtonResources.GetNormal()
            End Select
            SetSurface(If(tint, TintIfNeeded(raw), raw))
        End Sub

        ''' <summary>Port of DrawFlashBackground: flash-sweep frames (and the plain-Hover frame
        ''' shown mid-cycle) always get the HSL tint, unconditionally.</summary>
        Private Sub DrawFlashBackground(ByVal raw As Image)
            SetSurface(TintIfNeeded(raw))
        End Sub

        Private Sub SetSurface(ByVal img As Image)
            _currentSurface = img
            Invalidate()
        End Sub

        ''' <summary>Applies ColorOfHue/Saturation/Luminosity via ColorUtil.ApplyHslShift (skipped
        ''' entirely when all three are 0, the common case) and owns/disposes exactly one tinted
        ''' bitmap at a time so repeated calls during the 50ms flash animation don't leak.</summary>
        Private Function TintIfNeeded(ByVal raw As Image) As Image
            If raw Is Nothing Then Return Nothing
            If _hue = 0 AndAlso _saturation = 0 AndAlso _luminosity = 0 Then Return raw
            Dim newTint As Bitmap = ColorUtil.ApplyHslShift(raw, _hue, _saturation, _luminosity)
            Dim old As Bitmap = _ownedTint
            _ownedTint = newTint
            If old IsNot Nothing Then old.Dispose()
            Return newTint
        End Function

        ''' <summary>Port of DrawUserControl: redraws to Normal or Disable depending on Enabled.
        ''' VB6 called this from the Text/Color/ForeColor/Font/Enabled setters -- so changing any
        ''' of those while the button happens to be mid-hover/click/flash visually resets it back
        ''' to the plain base art. A real quirk, preserved rather than "fixed" into a smarter
        ''' refresh-whatever-is-current call.</summary>
        Private Sub RefreshBaseState()
            DrawBackground(If(Enabled, FlashButtonVisualState.Normal, FlashButtonVisualState.Disable))
        End Sub

        '=====================================================================
        ' Flash animation (port of tmrFlash_timer / CreateFlashTimer)
        '=====================================================================
        Private Sub StartFlashAnimation()
            If Not _flash Then Return
            If _flashTimer.Enabled Then Return
            _flashTimer.Interval = 50   ' gc_lngButtonFlashInterval
            _flashIndex = 0
            _flashForward = True
            _flashTimer.Start()
        End Sub

        Private Sub OnFlashTick(sender As Object, e As EventArgs)
            If Not Enabled Then Return

            If _mouseEnter AndAlso Not ClientRectangle.Contains(PointToClient(Cursor.Position)) Then
                LeaveHoverState()
                MyBase.OnMouseLeave(EventArgs.Empty)
            End If

            Dim picture As Image = Nothing
            If Not _focused AndAlso Not _mouseEnter Then
                ' fading out: count the frame index back down to 0, then stop and settle on Normal
                _flashIndex -= 1
                picture = FlashButtonResources.GetFlashFrame(_color, _flashIndex)
                If picture IsNot Nothing Then DrawFlashBackground(picture)
                If _flashIndex = 0 Then
                    _flashTimer.Stop()
                    DrawBackground(FlashButtonVisualState.Normal)
                End If
            Else
                Const flashCount As Integer = FlashButtonResources.FlashFrameCount
                Const waitFrames As Integer = 12   ' gc_lngButtonFlashHoverWaitFrames
                If _flashForward Then
                    If _flashIndex > flashCount + waitFrames Then
                        picture = FlashButtonResources.GetHover(_color)
                        _flashIndex = flashCount - 1
                        _flashForward = False
                    ElseIf _flashIndex >= flashCount AndAlso _flashIndex <= flashCount + waitFrames Then
                        _flashIndex += 1   ' pause at the end, holding the last-drawn frame
                    Else
                        picture = FlashButtonResources.GetFlashFrame(_color, _flashIndex)
                        _flashIndex += 1
                    End If
                Else
                    If _flashIndex = 0 Then
                        picture = FlashButtonResources.GetFlashFrame(_color, 0)
                        _flashForward = True
                        _flashIndex = 1
                    Else
                        picture = FlashButtonResources.GetFlashFrame(_color, _flashIndex)
                        _flashIndex -= 1
                    End If
                End If
                If picture IsNot Nothing Then DrawFlashBackground(picture)
            End If
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnClick(e As EventArgs)
            ' VB6's UserControl_Click drew the Click art, played the sound, did a BLOCKING 80ms
            ' Wait(), redrew Hover art, then finally raised Click -- so subscribers only saw Click
            ' after that visible pressed-flash. A one-shot timer gets the same visible sequence and
            ' the same "Click fires after the flash" ordering without freezing the UI thread.
            SoundUtil.PlaySound(_soundClick)
            DrawBackground(FlashButtonVisualState.Click)
            _pendingClickArgs = e
            _clickRevertTimer.Interval = 80   ' gc_lngButtonClickWaitInterval
            _clickRevertTimer.Stop()
            _clickRevertTimer.Start()
        End Sub

        Private Sub OnClickRevertTick(sender As Object, e As EventArgs)
            _clickRevertTimer.Stop()
            ' the darker face stays only while the mouse is over the button (Flash=True: the animation takes over)
            If _flash OrElse ClientRectangle.Contains(PointToClient(Cursor.Position)) Then DrawBackground(FlashButtonVisualState.Hover) Else RefreshBaseState()
            Dim args As EventArgs = If(_pendingClickArgs, EventArgs.Empty)
            _pendingClickArgs = Nothing
            MyBase.OnClick(args)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If Not _pressTimer.Enabled Then
                _pressButton = ButtonBits(e.Button)
                _pressShift = ModifierBits()
                _pressTimer.Interval = 1000
                _pressTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressTimer.Stop()
        End Sub

        Private Sub OnPressTick(sender As Object, e As EventArgs)
            _pressTimer.Interval = 100   ' VB6: first fire waits 1000ms, then repeats every 100ms
            RaiseEvent MousePress(_pressButton, _pressShift)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _mouseEnter = True
            _hover = False
            If _hoverInterval > 0 Then
                _hoverTimer.Interval = _hoverInterval
                _hoverTimer.Start()
            End If
            StartFlashAnimation()
            SoundUtil.PlaySound(_soundMouseEnter)
        End Sub

        Private Sub LeaveHoverState()
            _mouseEnter = False
            _hover = False
            _hoverTimer.Stop()
            ' Flash=False has no animation to fade it out: back to the plain face right away
            If Not _flash Then RefreshBaseState()
            SoundUtil.PlaySound(_soundMouseLeave)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            LeaveHoverState()
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If _hover Then Return
            _hover = True
            If Not _flash Then DrawBackground(FlashButtonVisualState.Hover)
            SoundUtil.PlaySound(_soundMouseHover)
            RaiseEvent MouseHover(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            _focused = True
            StartFlashAnimation()
            ' Flash=False: focus alone doesn't darken the button (it stayed dark after a click)
            SoundUtil.PlaySound(_soundEnterFocus)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _focused = False
            SoundUtil.PlaySound(_soundExitFocus)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        ''' <summary>Enter/Space activates the button, same as a real WinForms Button.</summary>
        Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
            MyBase.OnKeyPress(e)
            If e.KeyChar = ControlChars.Cr OrElse e.KeyChar = " "c Then
                OnClick(EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            RefreshBaseState()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            RefreshBaseState()
        End Sub

        Protected Overrides Sub OnForeColorChanged(e As EventArgs)
            MyBase.OnForeColorChanged(e)
            RefreshBaseState()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            RefreshBaseState()
        End Sub

        Private Shared Function ButtonBits(ByVal b As MouseButtons) As Integer
            Select Case b
                Case MouseButtons.Left : Return 1
                Case MouseButtons.Right : Return 2
                Case MouseButtons.Middle : Return 4
                Case Else : Return 0
            End Select
        End Function

        Private Shared Function ModifierBits() As Integer
            Dim m As Integer = 0
            If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then m = m Or 1
            If (Control.ModifierKeys And Keys.Control) = Keys.Control Then m = m Or 2
            If (Control.ModifierKeys And Keys.Alt) = Keys.Alt Then m = m Or 4
            Return m
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _flashTimer.Stop() : _flashTimer.Dispose()
                _hoverTimer.Stop() : _hoverTimer.Dispose()
                _pressTimer.Stop() : _pressTimer.Dispose()
                _clickRevertTimer.Stop() : _clickRevertTimer.Dispose()
                If _ownedTint IsNot Nothing Then _ownedTint.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
