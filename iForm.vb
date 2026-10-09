Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.iForm UserControl (Control\iForm.ctl): a lighter "floating panel" sibling
' of AquaForm.vb -- same title bar / control-box / drag / TopMost / Opacity / BorderStyle pattern,
' but with NO main menu bar, all four corners rounded (VB6: DrawObtusenessControlBorderRegion,
' radius 16, vs AquaForm's top-two-corners-only) and four purely decorative corner glyphs
' (imgUL/UR/DL/DR -- VB6 wires no mouse handlers to them at all, confirmed by grepping iForm.ctl;
' only the single bottom-right imgSize is an actual interactive resize handle, exactly like
' AquaForm). See AquaForm.vb's header comment for what's intentionally not ported (shadow window,
' Parhelia, OLE drag-drop) and which events are covered by native Form members instead.
Namespace Global.Aqua

    Public Class iForm
        Inherits Form

        Private Const TitleBarHeight As Integer = 23
        Private Const ControlBoxStartX As Integer = 8
        Private Const ControlBoxInterval As Integer = 6
        Private Const ControlBoxIconSize As Integer = 12
        Private Const CornerRadius As Integer = 16
        Private Const ResizeGripSize As Integer = 16

        Private _titleIcon As Image
        Private _bgImage As Image
        Private _sizeMode As ImageSizeMode = ImageSizeMode.Appose
        Private _borderStyle As Aqua.FormBorderStyle = Aqua.FormBorderStyle.Fixed
        Private _closeButton As Boolean = True
        Private _minButton As Boolean = True
        Private _maxButton As Boolean = True
        Private _hoverInterval As Integer = 0
        Private _titleFont As Font
        Private _shadow As Boolean = True

        Private _hoverBox As FormControlBox? = Nothing
        Private _lastWindowState As FormWindowState = FormWindowState.Normal

        ' last title-bar press, for double-click detection (see IsTitleDoubleClick)
        Private _titleClickTick As Integer
        Private _titleClickScreen As Point
        Private _titleClickValid As Boolean = False

        Private _mouseEnter As Boolean = False
        Private _hover As Boolean = False
        Private ReadOnly _hoverTimer As New Timer()
        Private ReadOnly _pressTimer As New Timer()
        Private _pressButton As Integer
        Private _pressShift As Integer

        Private _resizing As Boolean = False
        Private _resizeStartScreen As Point
        Private _resizeStartSize As Size
        Private ReadOnly _resizeThrottle As New Timer() With {.Interval = 15}
        Private _pendingResizeSize As Size?

        Private _soundClick, _soundMouseEnter, _soundMouseHover, _soundMouseLeave As String

        Public Event TitleChanged(sender As Object, e As EventArgs)
        Public Event IconChanged(sender As Object, e As EventArgs)
        Public Event ImageChanged(sender As Object, e As EventArgs)
        Public Event SizeModeChanged(sender As Object, e As EventArgs)
        Public Event BorderStyleChanged(sender As Object, e As EventArgs)
        Public Event CloseButtonChanged(sender As Object, e As EventArgs)
        Public Event MinButtonChanged(sender As Object, e As EventArgs)
        Public Event MaxButtonChanged(sender As Object, e As EventArgs)
        Public Event TopMostChanged(sender As Object, e As EventArgs)
        Public Event OpacityChanged(sender As Object, e As EventArgs)
        Public Event TitleFontChanged(sender As Object, e As EventArgs)
        Public Event Minimize(sender As Object, e As EventArgs)
        Public Event Maximize(sender As Object, e As EventArgs)
        Public Event Restore(sender As Object, e As EventArgs)
        Public Event MousePress(button As Integer, shift As Integer)
        ''' <summary>Shadows Control's built-in MouseHover: gated on a configurable HoverInterval
        ''' timer (default 0 = disabled, matching VB6) rather than the OS's fixed hover delay.</summary>
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)

        Public Sub New()
            ' See AquaForm.vb's constructor: neither DoubleBuffered=True nor
            ' ControlStyles.OptimizedDoubleBuffer here -- confirmed by frame-by-frame inspection of
            ' the reported flicker to be what makes child controls flash solid black during a live
            ' resize drag.
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.ResizeRedraw, True)
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
            Size = New Size(544, 405)
            Text = "Title"
            BackColor = SystemColors.Control
            _titleFont = Font
            _pressTimer.Interval = 1000
            AddHandler _pressTimer.Tick, AddressOf OnPressTick
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick
            AddHandler _resizeThrottle.Tick, AddressOf OnResizeThrottleTick
            UpdateRegion()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            NativeWindowDrag.DisableDwmCornerRounding(Handle)
            UpdateRegion()
        End Sub

        ''' <summary>See AquaForm.vb's WndProc override for why WM_ERASEBKGND is swallowed here --
        ''' same live-resize flicker fix.</summary>
        Protected Overrides Sub WndProc(ByRef m As Message)
            Const WM_ERASEBKGND As Integer = &H14
            If m.Msg = WM_ERASEBKGND Then
                m.Result = CType(1, IntPtr)
                Return
            End If
            MyBase.WndProc(m)
        End Sub

        '=====================================================================
        ' Properties (same shape as AquaForm.vb, minus the menu bar)
        '=====================================================================
        <Category("外觀")>
        Public Property TitleIcon As Image
            Get
                Return _titleIcon
            End Get
            Set(value As Image)
                _titleIcon = value
                Invalidate()
                RaiseEvent IconChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property Image As Image
            Get
                Return _bgImage
            End Get
            Set(value As Image)
                _bgImage = value
                Invalidate()
                RaiseEvent ImageChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ImageSizeMode.Appose)>
        Public Property SizeMode As ImageSizeMode
            Get
                Return _sizeMode
            End Get
            Set(value As ImageSizeMode)
                If _sizeMode = value Then Return
                _sizeMode = value
                Invalidate()
                RaiseEvent SizeModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(Aqua.FormBorderStyle.Fixed)>
        Public Property WindowBorderStyle As Aqua.FormBorderStyle
            Get
                Return _borderStyle
            End Get
            Set(value As Aqua.FormBorderStyle)
                If _borderStyle = value Then Return
                _borderStyle = value
                Invalidate()
                RaiseEvent BorderStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property CloseButton As Boolean
            Get
                Return _closeButton
            End Get
            Set(value As Boolean)
                If _closeButton = value Then Return
                _closeButton = value
                Invalidate()
                RaiseEvent CloseButtonChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property MaxButton As Boolean
            Get
                Return _maxButton
            End Get
            Set(value As Boolean)
                If _maxButton = value Then Return
                _maxButton = value
                Invalidate()
                RaiseEvent MaxButtonChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property MinButton As Boolean
            Get
                Return _minButton
            End Get
            Set(value As Boolean)
                If _minButton = value Then Return
                _minButton = value
                Invalidate()
                RaiseEvent MinButtonChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(0)>
        Public Property HoverInterval As Integer
            Get
                Return _hoverInterval
            End Get
            Set(value As Integer)
                _hoverInterval = value
            End Set
        End Property

        <Category("外觀")>
        Public Property TitleFont As Font
            Get
                Return _titleFont
            End Get
            Set(value As Font)
                _titleFont = value
                Invalidate()
                RaiseEvent TitleFontChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Kept for API compatibility with VB6's Shadow property; the Quartz.ShadowWindow
        ''' drop-shadow effect it drove has no port here, so this is currently a no-op.</summary>
        <Category("外觀")>
        <DefaultValue(True)>
        Public Property Shadow As Boolean
            Get
                Return _shadow
            End Get
            Set(value As Boolean)
                _shadow = value
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

        Public Shadows Property TopMost As Boolean
            Get
                Return MyBase.TopMost
            End Get
            Set(value As Boolean)
                If MyBase.TopMost = value Then Return
                MyBase.TopMost = value
                RaiseEvent TopMostChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Shadows Property Opacity As Double
            Get
                Return MyBase.Opacity
            End Get
            Set(value As Double)
                If MyBase.Opacity = value Then Return
                MyBase.Opacity = value
                RaiseEvent OpacityChanged(Me, EventArgs.Empty)
            End Set
        End Property

        '=====================================================================
        ' Region: all four corners rounded (VB6: DrawObtusenessControlBorderRegion, radius 16) --
        ' the one real geometric difference from AquaForm's top-only rounding.
        '=====================================================================
        Private Sub UpdateRegion()
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim old As Region = Me.Region
            Me.Region = RegionUtil.CreateObtusenessRegion(ObtusenessMode.All, Width, Height, CornerRadius)
            If old IsNot Nothing Then old.Dispose()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            ' (See AquaForm.vb's OnResize -- reverted skipping this mid-drag, it broke live resize.)
            UpdateRegion()
            Invalidate()
            If WindowState <> _lastWindowState Then
                Dim prev As FormWindowState = _lastWindowState
                _lastWindowState = WindowState
                Select Case WindowState
                    Case FormWindowState.Minimized : RaiseEvent Minimize(Me, EventArgs.Empty)
                    Case FormWindowState.Maximized : RaiseEvent Maximize(Me, EventArgs.Empty)
                    Case FormWindowState.Normal
                        If prev <> FormWindowState.Normal Then RaiseEvent Restore(Me, EventArgs.Empty)
                End Select
            End If
        End Sub

        '=====================================================================
        ' Layout helpers
        '=====================================================================
        Private Function TitleBarRect() As Rectangle
            Return New Rectangle(0, 0, Width, TitleBarHeight)
        End Function

        Private Function ContentRect() As Rectangle
            Return New Rectangle(1, TitleBarHeight, Math.Max(0, Width - 2), Math.Max(0, Height - TitleBarHeight - 1))
        End Function

        Private Function ControlBoxRect(ByVal box As FormControlBox) As Rectangle
            Dim x As Integer = ControlBoxStartX
            Dim y As Integer = ControlBoxStartX
            If box = FormControlBox.Close Then Return New Rectangle(x, y, ControlBoxIconSize, ControlBoxIconSize)
            If _closeButton Then x += ControlBoxIconSize + ControlBoxInterval
            If box = FormControlBox.Minimize Then Return New Rectangle(x, y, ControlBoxIconSize, ControlBoxIconSize)
            If _minButton Then x += ControlBoxIconSize + ControlBoxInterval
            Return New Rectangle(x, y, ControlBoxIconSize, ControlBoxIconSize)   ' Maximize
        End Function

        Private Function ResizeGripRect() As Rectangle
            Return New Rectangle(Width - ResizeGripSize, Height - ResizeGripSize, ResizeGripSize, ResizeGripSize)
        End Function

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        End Sub

        Private _backBuffer As Bitmap

        ''' <summary>See AquaForm.vb's OnPaint for why this is manually double-buffered here
        ''' instead of via ControlStyles.OptimizedDoubleBuffer / DoubleBuffered.</summary>
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Width <= 0 OrElse Height <= 0 Then Return
            If _backBuffer Is Nothing OrElse _backBuffer.Width <> Width OrElse _backBuffer.Height <> Height Then
                If _backBuffer IsNot Nothing Then _backBuffer.Dispose()
                _backBuffer = New Bitmap(Width, Height)
            End If

            Using g As Graphics = Graphics.FromImage(_backBuffer)
                Dim active As Boolean = ContainsFocus OrElse Focused

                DrawTitleBar(g, active)
                DrawContent(g)
                DrawBorder(g)
                If _borderStyle = Aqua.FormBorderStyle.Sizable Then
                    Dim grip As Image = FormChromeResources.GetSizeGrip()
                    If grip IsNot Nothing Then g.DrawImage(grip, ResizeGripRect())
                End If
            End Using

            e.Graphics.DrawImageUnscaled(_backBuffer, Point.Empty)
        End Sub

        Private Sub DrawTitleBar(ByVal g As Graphics, ByVal active As Boolean)
            Dim bar As Image = FormChromeResources.GetTitleBar(active)
            Dim tr As Rectangle = TitleBarRect()
            If bar IsNot Nothing Then
                Skin.DrawStretch(g, bar, tr, horizontal:=True)
            Else
                Using b As New SolidBrush(SystemColors.Control)
                    g.FillRectangle(b, tr)
                End Using
            End If

            If _closeButton Then DrawControlBox(g, FormControlBox.Close, active)
            If _minButton Then DrawControlBox(g, FormControlBox.Minimize, active)
            If _maxButton Then DrawControlBox(g, FormControlBox.Maximize, active)

            Dim titleSize As Size = ChromeText.Measure(g, Text, _titleFont)
            Dim titleX As Integer = (Width - titleSize.Width) \ 2
            If _titleIcon IsNot Nothing Then
                g.DrawImage(_titleIcon, New Rectangle(titleX - 6 - _titleIcon.Width, (TitleBarHeight - _titleIcon.Height) \ 2, _titleIcon.Width, _titleIcon.Height))
            End If
            Dim titleColor As Color = If(active, ForeColor, ColorUtil.OleToColor(&H808080))
            ChromeText.DrawCentered(g, Text, _titleFont, New Rectangle(0, 0, Width, TitleBarHeight), titleColor)   ' smoothed (see ChromeText)
        End Sub

        Private Sub DrawControlBox(ByVal g As Graphics, ByVal box As FormControlBox, ByVal active As Boolean)
            Dim hover As Boolean = active AndAlso _hoverBox.HasValue AndAlso _hoverBox.Value = box
            Dim img As Image = FormChromeResources.GetControlButton(active, box, hover)
            If img IsNot Nothing Then g.DrawImage(img, ControlBoxRect(box))
        End Sub

        Private Sub DrawContent(ByVal g As Graphics)
            Dim cr As Rectangle = ContentRect()
            ' Always fill first (see AquaForm.DrawContent): pixels the picture doesn't cover must not be
            ' left transparent, or the screen and transparent Labels show stale garbage there.
            Using b As New SolidBrush(BackColor)
                g.FillRectangle(b, cr)
            End Using
            If _bgImage Is Nothing Then Return
            ' VB6 Quartz.Draw SizeMode: Appose = tile, Fill = nine-slice, ... (Skin.DrawSized)
            Skin.DrawSized(g, _bgImage, cr, _sizeMode)
        End Sub

        ''' <summary>Three independent straight Pen lines (VB6's own technique, plus corner overlay
        ''' images trying to visually patch the seam where they met the Region's actual curve) kept
        ''' producing a visible mismatch right at the rounded corners no matter how the overlay was
        ''' calibrated -- straight lines fundamentally can't line up with a curve. Stroking the exact
        ''' same GraphicsPath the Region itself was built from guarantees the border always follows
        ''' the true clipped outline, corners included, with nothing separate to misalign.</summary>
        Private Sub DrawBorder(ByVal g As Graphics)
            ' CreateObtusenessPath adds its own "+1" to width/height internally (the same padding
            ' CreateObtusenessRegion uses, calibrated for native rounded-region inclusive/exclusive
            ' semantics) -- for a Region that's harmless headroom, but for a visible Pen stroke it
            ' pushes the right/bottom edge one pixel past the real canvas and off it entirely.
            ' Passing Width-1/Height-1 here cancels that padding back out so the path's outer edge
            ' lands exactly on the last real pixel column/row.
            Using path As Drawing2D.GraphicsPath = RegionUtil.CreateObtusenessPath(ObtusenessMode.All, Width - 1, Height - 1, CornerRadius)
                Using p As New Pen(Color.FromArgb(148, 150, 148), 1)
                    g.DrawPath(p, path)
                End Using
            End Using
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return

            If _borderStyle = Aqua.FormBorderStyle.Sizable AndAlso ResizeGripRect().Contains(e.Location) Then
                ' See AquaForm.vb's OnMouseDown for why this tracks the drag manually instead of
                ' using the WM_SYSCOMMAND/SC_SIZE trick.
                _resizing = True
                _resizeStartScreen = PointToScreen(e.Location)
                _resizeStartSize = Size
                Capture = True
                _resizeThrottle.Start()
                Return
            End If

            If _closeButton AndAlso ControlBoxRect(FormControlBox.Close).Contains(e.Location) Then
                Close()
                Return
            End If
            If _minButton AndAlso ControlBoxRect(FormControlBox.Minimize).Contains(e.Location) Then
                WindowState = FormWindowState.Minimized
                Return
            End If
            If _maxButton AndAlso ControlBoxRect(FormControlBox.Maximize).Contains(e.Location) Then
                WindowState = If(WindowState = FormWindowState.Maximized, FormWindowState.Normal, FormWindowState.Maximized)
                Return
            End If

            If TitleBarRect().Contains(e.Location) Then
                ' double-click on the title maximizes / restores, like a native caption (only
                ' when the window has a maximize button, same as Windows)
                If _maxButton AndAlso IsTitleDoubleClick(e) Then
                    WindowState = If(WindowState = FormWindowState.Maximized, FormWindowState.Normal, FormWindowState.Maximized)
                    Return
                End If
                SoundUtil.PlaySound(_soundClick)
                NativeWindowDrag.BeginDragMove(Handle)
                Return
            End If

            If Not _pressTimer.Enabled Then
                _pressButton = ButtonBits(e.Button)
                _pressShift = ModifierBits()
                _pressTimer.Interval = 1000
                _pressTimer.Start()
            End If
        End Sub

        ''' <summary>Whether this title-bar press completes a double-click. Tracked by hand because the
        ''' first press goes into the OS move loop (BeginDragMove), which swallows the button-up, so
        ''' WinForms doesn't reliably report the second press as a double-click (e.Clicks = 2).
        ''' Uses the system double-click time and distance; a recognised double-click resets the
        ''' tracking so a third press starts over instead of toggling again.</summary>
        Private Function IsTitleDoubleClick(ByVal e As MouseEventArgs) As Boolean
            Dim now As Integer = Environment.TickCount
            Dim pt As Point = PointToScreen(e.Location)
            Dim area As Size = SystemInformation.DoubleClickSize
            Dim isDouble As Boolean = e.Clicks >= 2 OrElse
                (_titleClickValid AndAlso
                 now - _titleClickTick <= SystemInformation.DoubleClickTime AndAlso
                 Math.Abs(pt.X - _titleClickScreen.X) <= area.Width \ 2 AndAlso
                 Math.Abs(pt.Y - _titleClickScreen.Y) <= area.Height \ 2)
            If isDouble Then
                _titleClickValid = False
            Else
                _titleClickValid = True
                _titleClickTick = now
                _titleClickScreen = pt
            End If
            Return isDouble
        End Function

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressTimer.Stop()
            If _resizing Then
                _resizing = False
                Capture = False
                _resizeThrottle.Stop()
                If _pendingResizeSize.HasValue Then
                    Dim target As Size = _pendingResizeSize.Value
                    _pendingResizeSize = Nothing
                    NativeWindowDrag.SuspendDrawing(Handle)
                    Size = target
                    NativeWindowDrag.ResumeDrawing(Handle)
                End If
            End If
        End Sub

        Private Sub OnPressTick(sender As Object, e As EventArgs)
            _pressTimer.Interval = 100
            RaiseEvent MousePress(_pressButton, _pressShift)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)

            If _resizing Then
                ' See AquaForm.vb's OnMouseMove: throttled to the timer below instead of applying
                ' Size on every MouseMove, which is what was causing continuous resize flicker.
                Dim cur As Point = PointToScreen(e.Location)
                Dim newWidth As Integer = Math.Max(MinimumSize.Width, _resizeStartSize.Width + (cur.X - _resizeStartScreen.X))
                Dim newHeight As Integer = Math.Max(MinimumSize.Height, _resizeStartSize.Height + (cur.Y - _resizeStartScreen.Y))
                _pendingResizeSize = New Size(newWidth, newHeight)
                Return
            End If

            Dim newHoverBox As FormControlBox? = Nothing
            If _closeButton AndAlso ControlBoxRect(FormControlBox.Close).Contains(e.Location) Then newHoverBox = FormControlBox.Close
            If _minButton AndAlso ControlBoxRect(FormControlBox.Minimize).Contains(e.Location) Then newHoverBox = FormControlBox.Minimize
            If _maxButton AndAlso ControlBoxRect(FormControlBox.Maximize).Contains(e.Location) Then newHoverBox = FormControlBox.Maximize
            If newHoverBox <> _hoverBox Then
                _hoverBox = newHoverBox
                Invalidate()
            End If

            If Not _mouseEnter Then
                _mouseEnter = True
                _hover = False
                SoundUtil.PlaySound(_soundMouseEnter)
                Dim interval As Integer = If(_hoverInterval = 0, 100, _hoverInterval)
                If interval > 0 Then
                    _hoverTimer.Interval = interval
                    _hoverTimer.Start()
                End If
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseEnter = False
            _hover = False
            _hoverTimer.Stop()
            If _hoverBox.HasValue Then
                _hoverBox = Nothing
                Invalidate()
            End If
            SoundUtil.PlaySound(_soundMouseLeave)
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If _hover Then Return
            _hover = True
            SoundUtil.PlaySound(_soundMouseHover)
            RaiseEvent MouseHover(Me, EventArgs.Empty)
        End Sub

        Private Sub OnResizeThrottleTick(sender As Object, e As EventArgs)
            If Not _pendingResizeSize.HasValue Then Return
            Dim target As Size = _pendingResizeSize.Value
            _pendingResizeSize = Nothing
            If target <> Size Then
                NativeWindowDrag.SuspendDrawing(Handle)
                Size = target
                NativeWindowDrag.ResumeDrawing(Handle)
            End If
        End Sub

        Protected Overrides Sub OnActivated(e As EventArgs)
            MyBase.OnActivated(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnDeactivate(e As EventArgs)
            MyBase.OnDeactivate(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
            RaiseEvent TitleChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
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
                _hoverTimer.Stop() : _hoverTimer.Dispose()
                _pressTimer.Stop() : _pressTimer.Dispose()
                _resizeThrottle.Stop() : _resizeThrottle.Dispose()
                If _backBuffer IsNot Nothing Then _backBuffer.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
