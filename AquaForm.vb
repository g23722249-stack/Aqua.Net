Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.AquaForm UserControl (Control\AquaForm.ctl): a Mac OS X ("Aqua")-styled
' custom window chrome -- title bar with left-aligned traffic-light close/min/max buttons, an
' optional Apple-menu-style main menu bar, a background texture/colour, a single bottom-right
' resize grip, and top-corner rounding.
'
' VB6 built this as a UserControl that took over its host VB.Form's Extender (SetParent,
' BorderStyle=0, syncing Width/Height) so the *real* window was the plain host Form underneath.
' This port is simpler: AquaForm inherits Form directly and IS the window (FormBorderStyle=None),
' with everything owner-drawn onto its own client area. Dragging and resizing use the classic
' ReleaseCapture+WM_SYSCOMMAND handoff to the OS's native move/size loop (Internal\NativeWindowDrag)
' instead of VB6's manual WM_NCLBUTTONDOWN passthrough and pixel-delta resize loop.
'
' Not ported (see also FlashButton.vb/Buttons.vb for the same calls elsewhere in this codebase):
'   - Quartz.ShadowWindow drop shadow (Shadow property kept for API compatibility; a no-op here)
'   - "Parhelia" focus glow projected onto child controls
'   - OLE drag-drop passthrough events (WinForms has its own DragEventArgs-based model -- use
'     Form's native DragEnter/DragDrop instead)
' Superseded by native Form members, so not redeclared: Resize, Click, DblClick, KeyDown/Press/Up,
' MouseDown/Move/Up (use Form's own), Active/Deactivate (use Activated/Deactivate), Create/Show/Hide
' (use Load/Shown/VisibleChanged).
Namespace Global.Aqua

    <DefaultEvent("MenuSelected")>
    Public Class AquaForm
        Inherits Form

        Private Const TitleBarHeight As Integer = 23
        Private Const ControlBoxStartX As Integer = 8
        Private Const ControlBoxInterval As Integer = 6
        Private Const ControlBoxIconSize As Integer = 12
        Private Const CornerRadius As Integer = 11
        Private Const ResizeGripSize As Integer = 16

        Private _titleIcon As Image
        Private _bgImage As Image
        Private _sizeMode As ImageSizeMode = ImageSizeMode.Appose
        Private _borderStyle As Aqua.FormBorderStyle = Aqua.FormBorderStyle.Fixed
        Private _closeButton As Boolean = True
        Private _minButton As Boolean = True
        Private _maxButton As Boolean = True
        Private _hoverInterval As Integer = 0
        Private _mainMenu As MenuItem
        Private _selectedMenu As MenuItem
        Private _titleFont As Font
        Private _menuFont As Font
        Private _shadow As Boolean = True

        Private _hoverBox As FormControlBox? = Nothing
        Private _hoverMenuIndex As Integer = -1
        Private _openMenuIndex As Integer = -1
        Private _openPopup As MenuPopupForm
        Private ReadOnly _menuItemRects As New List(Of Rectangle)()
        Private _lastWindowState As FormWindowState = FormWindowState.Normal

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

        Public Event MenuOpen(sender As Object, e As EventArgs)
        Public Event MenuClose(sender As Object, e As EventArgs)
        Public Event MenuSelected(sender As Object, item As MenuItem)
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
        Public Event MenuFontChanged(sender As Object, e As EventArgs)
        Public Event Minimize(sender As Object, e As EventArgs)
        Public Event Maximize(sender As Object, e As EventArgs)
        Public Event Restore(sender As Object, e As EventArgs)
        Public Event MousePress(button As Integer, shift As Integer)
        ''' <summary>Shadows Control's built-in MouseHover: gated on a configurable HoverInterval
        ''' timer (default 0 = disabled, matching VB6) rather than the OS's fixed hover delay.</summary>
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)

        Public Sub New()
            ' Neither DoubleBuffered=True NOR ControlStyles.OptimizedDoubleBuffer on this Form:
            ' confirmed by extracting individual frames from a recording of the reported flicker --
            ' while a resize drag is active, child controls placed on the content area (Buttons,
            ' Labels) render solid BLACK instead of their real content, flipping back to normal the
            ' instant the drag ends. That's the actual flicker; a background-erase/repaint-timing
            ' issue would look different (a white/blank flash, not the children going solid black).
            ' Both of these styles push the Form toward compositing its own bitmap in a way that
            ' doesn't keep real child HWNDs' own painting in sync during rapid Size/Region changes.
            ' AllPaintingInWmPaint + the WM_ERASEBKGND swallow further down still cut down on
            ' background-erase flicker for our OWN owner-drawn chrome without this conflict.
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.ResizeRedraw, True)
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
            Size = New Size(588, 447)
            Text = "Title"
            BackColor = SystemColors.Control
            _titleFont = New Font("Times New Roman", 12)
            _menuFont = Font
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

        ''' <summary>DoubleBuffered/OptimizedDoubleBuffer alone don't fully stop the flicker seen
        ''' while live-resizing (dragging the grip, or Windows' own edge-drag once Sizable): the OS
        ''' still sends WM_ERASEBKGND on every resize step, which paints a plain background wipe
        ''' UNDER our own double-buffered frame. Since OnPaint already fully repaints the entire
        ''' client area every time (title bar + content + border, no gaps), that OS erase is pure
        ''' overhead -- swallowing it here (returning nonzero = "already erased") removes the wipe
        ''' that was flashing between frames.</summary>
        Protected Overrides Sub WndProc(ByRef m As Message)
            Const WM_ERASEBKGND As Integer = &H14
            If m.Msg = WM_ERASEBKGND Then
                m.Result = CType(1, IntPtr)
                Return
            End If
            MyBase.WndProc(m)
        End Sub

        '=====================================================================
        ' Properties
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

        <Category("外觀")>
        Public Property MenuFont As Font
            Get
                Return _menuFont
            End Get
            Set(value As Font)
                _menuFont = value
                Invalidate()
                RaiseEvent MenuFontChanged(Me, EventArgs.Empty)
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

        <Browsable(False)>
        Public ReadOnly Property SelectedMenu As MenuItem
            Get
                Return _selectedMenu
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property Menu As MenuItem
            Get
                Return _mainMenu
            End Get
        End Property

        Public Function AddMenu(ByVal mainMenu As MenuItem) As MenuItem
            _mainMenu = mainMenu
            Invalidate()
            Return _mainMenu
        End Function

        Public Sub ClearMenu()
            _mainMenu = Nothing
            _selectedMenu = Nothing
            Invalidate()
        End Sub

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

        '=====================================================================
        ' Region: VB6 only rounded the TOP two corners (DrawObtusenessControlRegion
        ' ObtusenessMode.上方=Top, radius 11) -- the bottom stays square.
        '=====================================================================
        Private Sub UpdateRegion()
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim old As Region = Me.Region
            Me.Region = RegionUtil.CreateObtusenessRegion(ObtusenessMode.Top, Width, Height, CornerRadius)
            If old IsNot Nothing Then old.Dispose()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            ' (Previously skipped this during a manual drag-resize to cut down on flicker, but that
            ' left the Region -- and so the window's actual visible bounds -- stuck at the pre-drag
            ' size until mouse-up, killing live resize feedback entirely. Reverted: update every
            ' time, like before.)
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
        Private ReadOnly Property MenuBarVisible As Boolean
            Get
                Return _mainMenu IsNot Nothing AndAlso _mainMenu.Count > 0
            End Get
        End Property

        Private Function MenuBarHeight() As Integer
            Return If(MenuBarVisible, TextRenderer.MeasureText("Ag", _menuFont).Height + 6, 0)
        End Function

        Private Function TitleBarRect() As Rectangle
            Return New Rectangle(0, 0, Width, TitleBarHeight)
        End Function

        Private Function MenuBarRect() As Rectangle
            Return New Rectangle(0, TitleBarHeight, Width, MenuBarHeight())
        End Function

        Private Function ContentRect() As Rectangle
            Dim top As Integer = TitleBarHeight + MenuBarHeight()
            Return New Rectangle(1, top, Math.Max(0, Width - 2), Math.Max(0, Height - top - 1))
        End Function

        Private Function ControlBoxRect(ByVal box As FormControlBox) As Rectangle
            Dim x As Integer = ControlBoxStartX
            Dim y As Integer = (TitleBarHeight - ControlBoxIconSize) \ 2
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
            ' full painting happens in OnPaint; avoid the default flicker-prone fill
        End Sub

        Private _backBuffer As Bitmap

        ''' <summary>Manual double buffering, scoped to just this Form's own owner-drawn chrome:
        ''' draw everything to an off-screen Bitmap first, then blit it to the screen in one
        ''' DrawImage call. ControlStyles.OptimizedDoubleBuffer / Form.DoubleBuffered would do this
        ''' automatically, but for a Form specifically that also swallows real child controls'
        ''' own painting during rapid Size/Region changes (confirmed: Buttons/Labels flashed solid
        ''' black during a live resize) -- managing the buffer ourselves, only for the chrome we
        ''' draw here, leaves child controls' normal WM_PAINT handling completely alone.</summary>
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Width <= 0 OrElse Height <= 0 Then Return
            If _backBuffer Is Nothing OrElse _backBuffer.Width <> Width OrElse _backBuffer.Height <> Height Then
                If _backBuffer IsNot Nothing Then _backBuffer.Dispose()
                _backBuffer = New Bitmap(Width, Height)
            End If

            Using g As Graphics = Graphics.FromImage(_backBuffer)
                ' Also counts as "active" while our own dropdown menu is open: that popup is a
                ' separate top-level window holding the real OS focus, so ContainsFocus/Focused
                ' alone would make the title bar flicker to its inactive tint whenever a menu is open.
                Dim active As Boolean = ContainsFocus OrElse Focused OrElse (_openPopup IsNot Nothing AndAlso Not _openPopup.IsDisposed)

                DrawTitleBar(g, active)
                If MenuBarVisible Then DrawMenuBar(g)
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

        Private Sub DrawMenuBar(ByVal g As Graphics)
            Dim mr As Rectangle = MenuBarRect()
            Dim bg As Image = MenuResources.GetBackground()
            If bg IsNot Nothing Then
                Skin.DrawStretch(g, bg, mr, horizontal:=False)
            Else
                Using b As New SolidBrush(SystemColors.ControlLight)
                    g.FillRectangle(b, mr)
                End Using
            End If
            Using p As New Pen(Color.FromArgb(165, 166, 165), 2)
                g.DrawLine(p, 0, mr.Bottom, Width, mr.Bottom)
            End Using

            _menuItemRects.Clear()
            Dim apple As Image = MenuResources.GetApple()
            Dim x As Integer = MenuResources.MainMenuInterval
            If apple IsNot Nothing Then
                g.DrawImage(apple, New Rectangle(x, mr.Top + (mr.Height - apple.Height) \ 2, apple.Width, apple.Height))
                x += apple.Width + MenuResources.MainMenuInterval
            End If

            For i = 0 To _mainMenu.Count - 1
                Dim item As MenuItem = _mainMenu(i)
                If Not item.Visible Then
                    _menuItemRects.Add(Rectangle.Empty)
                    Continue For
                End If
                Dim sz As Size = TextRenderer.MeasureText(item.Text, _menuFont)
                Dim rect As New Rectangle(x, mr.Top, sz.Width + 8, mr.Height)
                _menuItemRects.Add(rect)

                If i = _openMenuIndex OrElse i = _hoverMenuIndex Then
                    Dim sel As Image = MenuResources.GetSelected()
                    Dim hl As New Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height - 4)
                    If sel IsNot Nothing Then
                        Skin.DrawStretch(g, sel, hl, horizontal:=True)
                    Else
                        Using b As New SolidBrush(Color.FromArgb(51, 153, 255))
                            g.FillRectangle(b, hl)
                        End Using
                    End If
                End If
                Dim fc As Color = If(i = _openMenuIndex OrElse i = _hoverMenuIndex, Color.White, ForeColor)
                ChromeText.DrawCentered(g, item.Text, _menuFont, rect, fc)   ' smoothed (see ChromeText); widths still from TextRenderer
                x += rect.Width
            Next
        End Sub

        Private Sub DrawContent(ByVal g As Graphics)
            Dim cr As Rectangle = ContentRect()
            ' Always fill first: the back buffer is reused between paints, and any pixel the picture
            ' doesn't cover would otherwise stay transparent -- copied to the screen as "unchanged", it
            ' showed whatever was there before, and transparent Labels picked that garbage up too.
            Using b As New SolidBrush(BackColor)
                g.FillRectangle(b, cr)
            End Using
            If _bgImage Is Nothing Then Return
            ' VB6 Quartz.Draw SizeMode (Skin.DrawSized): Appose = tile, Fill = nine-slice, CenterImage =
            ' centred, AutoSize = zoom, Normal = top-left. (Appose used to be drawn once, centred, and Fill
            ' stretched.)
            Skin.DrawSized(g, _bgImage, cr, _sizeMode)
        End Sub

        ''' <summary>Straight Pen lines can't line up with the Region's rounded top corners --
        ''' stroking the exact same GraphicsPath the Region was built from (ObtusenessMode.Top,
        ''' matching UpdateRegion above) guarantees the border always follows the true clipped
        ''' outline. See iForm.vb's DrawBorder for the same fix and the full story.</summary>
        Private Sub DrawBorder(ByVal g As Graphics)
            ' See iForm.vb's DrawBorder for why Width-1/Height-1 (not Width/Height) here.
            Using path As Drawing2D.GraphicsPath = RegionUtil.CreateObtusenessPath(ObtusenessMode.Top, Width - 1, Height - 1, CornerRadius)
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
                ' WM_SYSCOMMAND/SC_SIZE (the usual borderless-window resize trick, still used for
                ' the title-bar drag below) turned out not to reliably drive an actual resize for
                ' this window -- tracking the drag manually, the same way VB6's own
                ' imgSize_MouseDown/MouseMove/MouseUp did, sidesteps whatever that mismatch was.
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

            If MenuBarVisible Then
                Dim idx As Integer = MenuIndexAt(e.Location)
                If idx >= 0 Then
                    ToggleMenu(idx)
                    Return
                End If
            End If

            If TitleBarRect().Contains(e.Location) Then
                CloseOpenMenu()
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

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressTimer.Stop()
            If _resizing Then
                _resizing = False
                Capture = False
                _resizeThrottle.Stop()
                ' Flush whatever the last MouseMove computed but the throttle hadn't applied yet,
                ' so the final size is exactly where the mouse was released, not rounded down to
                ' the last ~15ms tick.
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

        Private Function MenuIndexAt(ByVal p As Point) As Integer
            For i = 0 To _menuItemRects.Count - 1
                If _menuItemRects(i).Contains(p) Then Return i
            Next
            Return -1
        End Function

        Private Sub ToggleMenu(ByVal index As Integer)
            If _openMenuIndex = index Then
                CloseOpenMenu()
                Return
            End If
            CloseOpenMenu()
            _openMenuIndex = index
            Invalidate()
            RaiseEvent MenuOpen(Me, EventArgs.Empty)

            _openPopup = New MenuPopupForm()
            AddHandler _openPopup.ItemClicked, AddressOf OnPopupItemClicked
            AddHandler _openPopup.AutoClosed, AddressOf OnPopupAutoClosed
            Dim anchor As Point = PointToScreen(New Point(_menuItemRects(index).Left, MenuBarRect().Bottom))
            _openPopup.ShowFor(_mainMenu(index), anchor, _menuFont)
        End Sub

        ''' <summary>The popup hid itself because the whole chain lost activation (user clicked
        ''' elsewhere, Alt-Tabbed away, etc.) -- reconcile our own bookkeeping to match.</summary>
        Private Sub OnPopupAutoClosed(sender As Object, e As EventArgs)
            CloseOpenMenu()
        End Sub

        Private Sub CloseOpenMenu()
            If _openPopup IsNot Nothing Then
                If Not _openPopup.IsDisposed Then
                    _openPopup.CloseAll()
                    _openPopup.Dispose()
                End If
                _openPopup = Nothing
            End If
            If _openMenuIndex >= 0 Then
                _openMenuIndex = -1
                Invalidate()
                RaiseEvent MenuClose(Me, EventArgs.Empty)
            End If
        End Sub

        Private Sub OnPopupItemClicked(item As MenuItem)
            CloseOpenMenu()
            _selectedMenu = item
            RaiseEvent MenuSelected(Me, item)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)

            If _resizing Then
                ' MouseMove can fire far more often than the display (or the Region rebuild +
                ' repaint it triggers) can actually keep up with, which is what was causing
                ' continuous flicker -- record the target size here but let the ~15ms throttle
                ' timer below be the only thing that actually applies it, capping the real update
                ' rate to roughly what the screen can show smoothly instead of every mouse tick.
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

            If MenuBarVisible Then
                Dim idx As Integer = MenuIndexAt(e.Location)
                If idx <> _hoverMenuIndex Then
                    _hoverMenuIndex = idx
                    Invalidate()
                    If idx >= 0 AndAlso _openMenuIndex >= 0 AndAlso _openMenuIndex <> idx Then ToggleMenu(idx)
                End If
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
            ' NOT closing the open menu here: showing/activating our own popup is itself one of
            ' the things that deactivates this Form (it's a separate top-level window), so doing
            ' that would close the menu the instant it opens. The popup's own activation-chain
            ' logic (MenuPopupForm.OnDeactivate) already closes it when focus genuinely leaves the
            ' whole menu system, and notifies us via AutoClosed (see ToggleMenu) to reconcile.
            Invalidate()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
            RaiseEvent TitleChanged(Me, EventArgs.Empty)
        End Sub

        ''' <summary>Form.TopMost/Opacity have no OnXChanged override point, so these are
        ''' shadowed instead, purely to raise the VB6-compatible TopMostChanged/OpacityChanged
        ''' events; MyBase still does the real work.</summary>
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
                CloseOpenMenu()
                If _backBuffer IsNot Nothing Then _backBuffer.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
