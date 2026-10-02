Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Windows.Forms

' A slimmed-down Aqua.Button (ExamControls): one Image instead of four state images, defaulting to
' the ExamControls button-hover artwork. The image is drawn three-slice like Aqua.Button -- the two
' square end caps (image height wide) keep their aspect ratio, the middle stretches -- and the states
' are derived from it the way PngButton does:
'   mouse over / focused  -> the image as is
'   neither (ExitFocus)   -> the image faded to ExitFocusOpacity (60%)
'   pressed               -> the image slightly darkened
'   disabled              -> the image in grey scale, also faded
' The background is transparent the WinForms way (BackColor Transparent): every repaint first paints
' the parent behind the button, so the faded image is never drawn over its previous self. (Not
' WS_EX_TRANSPARENT like Aqua.Button: beside a WS_EX_COMPOSITED Aqua.TabControl that kept the window
' repainting without end.)
' Events match FlashButton so the two can replace each other: Click fires 80 ms after the pressed
' face shows (also for Enter/Space), MousePress first fires after 1 s held then every 100 ms with
' the modifier bits, MouseHover waits HoverInterval. ColorChanged is declared only for that swap --
' ThinButton has no Color, so it never fires; AutoSizeChanged follows Control.AutoSize.
Namespace Global.Aqua

    <DefaultEvent("Click"), DefaultProperty("Image")>
    Public Class ThinButton
        Inherits Control

        Private Const PressedBrightness As Single = 0.85F
        Private Const ExitFocusOpacity As Single = 0.6F
        ' Not FlashButton's gc_lngDisableForeColor (189,190,189): on the grey-scaled, faded image that
        ' light grey all but vanished.
        Private Shared ReadOnly DisabledForeColor As Color = Color.FromArgb(128, 128, 128)

        Private _image As Image
        Private _hoverInterval As Integer = 100

        Private _mouseHover As Boolean = False
        Private _pressed As Boolean = False
        Private _clickFlash As Boolean = False
        Private _focus As Boolean = False
        Private ReadOnly _pressTimer As New Timer()
        Private ReadOnly _hoverTimer As New Timer()
        Private ReadOnly _clickRevertTimer As New Timer()
        Private _pendingClickArgs As EventArgs
        Private _pressButton As Integer
        Private _pressShift As Integer

        ''' <summary>Fired after the button is held 1 s, then every 100 ms until released (as FlashButton).</summary>
        Public Event MousePress(button As Integer, shift As Integer)
        ''' <summary>Only here so a FlashButton's handlers still bind; ThinButton has no Color, so it never fires.</summary>
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Shadows Event AutoSizeChanged(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)
        ''' <summary>Shadows Control's built-in MouseHover: fires HoverInterval ms after the mouse
        ''' enters, like FlashButton, instead of after the OS's fixed hover delay.</summary>
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)

        Public Sub New()
            ' no OptimizedDoubleBuffer: the back buffer would paint over the transparency
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor Or
                     ControlStyles.Selectable, True)
            SetStyle(ControlStyles.Opaque, False)
            BackColor = Color.Transparent
            ForeColor = Color.White
            _pressTimer.Interval = 1000
            AddHandler _pressTimer.Tick, AddressOf OnPressTick
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick
            AddHandler _clickRevertTimer.Tick, AddressOf OnClickRevertTick

            ExamDefaultImages.EnsureLoaded()
            _image = ExamDefaultImages.ButtonHover
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(120, 36)
            End Get
        End Property

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("外觀"), Description("按鈕圖片,以高度為邊長的左右兩端維持比例、中間拉伸。滑鼠移入或取得焦點時原圖顯示,平時淡化,停用時灰階並淡化。")>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                _image = value
                RefreshTransparent()
            End Set
        End Property

        Private Function ShouldSerializeImage() As Boolean
            Return _image IsNot ExamDefaultImages.ButtonHover
        End Function

        Private Sub ResetImage()
            Image = ExamDefaultImages.ButtonHover
        End Sub

        <Category("行為"), Description("滑鼠移入後多久(毫秒)觸發 MouseHover;0 = 不觸發。"), DefaultValue(100)>
        Public Property HoverInterval As Integer
            Get
                Return _hoverInterval
            End Get
            Set(value As Integer)
                _hoverInterval = value
            End Set
        End Property

        <DefaultValue(GetType(Color), "Transparent")>
        Public Overrides Property BackColor As Color
            Get
                Return MyBase.BackColor
            End Get
            Set(value As Color)
                MyBase.BackColor = value
            End Set
        End Property

        <DefaultValue(GetType(Color), "White")>
        Public Overrides Property ForeColor As Color
            Get
                Return MyBase.ForeColor
            End Get
            Set(value As Color)
                MyBase.ForeColor = value
            End Set
        End Property

        '=====================================================================
        ' Painting
        '=====================================================================
        ' BackColor Transparent: WinForms paints the parent (its background and its own painting)
        ' behind the button on every repaint, so the faded image is never drawn over the previous frame.
        ' Not WS_EX_TRANSPARENT: next to a WS_EX_COMPOSITED control (Aqua.TabControl) that made the
        ' window repaint the TabControl without end (frmSetup stayed blank, one CPU core busy).

        ''' <summary>Repaints the button (the parent behind it comes with it, see above).</summary>
        Private Sub RefreshTransparent()
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            If _image IsNot Nothing AndAlso Width > 0 AndAlso Height > 0 Then
                g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                Using attr As ImageAttributes = StateAttributes()
                    DrawThreeSlice(g, _image, attr)
                End Using
            End If

            If Not String.IsNullOrEmpty(Text) Then
                Dim fc As Color = If(Enabled, ForeColor, DisabledForeColor)
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fc,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
            End If
        End Sub

        ''' <summary>Aqua.Button's layout, generalised from its fixed 516x172 artwork: the end caps are
        ''' image-height squares scaled to the button height, the middle strip fills the rest. An image
        ''' too narrow to slice is just stretched over the button.</summary>
        Private Sub DrawThreeSlice(ByVal g As Graphics, ByVal img As Image, ByVal attr As ImageAttributes)
            Dim cap As Integer = img.Height
            If img.Width < cap * 2 + 1 Then
                DrawPart(g, img, ClientRectangle, New Rectangle(0, 0, img.Width, img.Height), attr)
                Return
            End If

            Dim capDest As Integer = CInt(Math.Round(cap * (Height / CDbl(img.Height))))
            capDest = Math.Min(capDest, Width \ 2)
            Dim centerDest As Integer = Math.Max(0, Width - capDest * 2)

            DrawPart(g, img, New Rectangle(0, 0, capDest, Height),
                     New Rectangle(0, 0, cap, img.Height), attr)
            If centerDest > 0 Then
                DrawPart(g, img, New Rectangle(capDest, 0, centerDest, Height),
                         New Rectangle(cap, 0, img.Width - cap * 2, img.Height), attr)
            End If
            DrawPart(g, img, New Rectangle(Width - capDest, 0, capDest, Height),
                     New Rectangle(img.Width - cap, 0, cap, img.Height), attr)
        End Sub

        Private Shared Sub DrawPart(ByVal g As Graphics, ByVal img As Image, ByVal dest As Rectangle,
                                    ByVal src As Rectangle, ByVal attr As ImageAttributes)
            If dest.Width <= 0 OrElse dest.Height <= 0 Then Return
            If attr Is Nothing Then
                g.DrawImage(img, dest, src, GraphicsUnit.Pixel)
            Else
                g.DrawImage(img, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attr)
            End If
        End Sub

        ''' <summary>The colour matrix for the current state; Nothing = draw the image as is.</summary>
        Private Function StateAttributes() As ImageAttributes
            Dim m As ColorMatrix
            If Not Enabled Then
                ' grey scale (luminance weights), faded like ExitFocus
                m = New ColorMatrix(New Single()() {
                    New Single() {0.299F, 0.299F, 0.299F, 0, 0},
                    New Single() {0.587F, 0.587F, 0.587F, 0, 0},
                    New Single() {0.114F, 0.114F, 0.114F, 0, 0},
                    New Single() {0, 0, 0, ExitFocusOpacity, 0},
                    New Single() {0, 0, 0, 0, 1}})
            ElseIf _pressed OrElse _clickFlash Then
                m = New ColorMatrix()
                m.Matrix00 = PressedBrightness : m.Matrix11 = PressedBrightness : m.Matrix22 = PressedBrightness
            ElseIf _mouseHover OrElse _focus Then
                Return Nothing
            Else
                m = New ColorMatrix()
                m.Matrix33 = ExitFocusOpacity
            End If
            Dim attr As New ImageAttributes()
            attr.SetColorMatrix(m)
            Return attr
        End Function

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            RefreshTransparent()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            RefreshTransparent()
        End Sub

        '=====================================================================
        ' Interaction (events and timing as FlashButton)
        '=====================================================================
        Protected Overrides Sub OnClick(e As EventArgs)
            ' FlashButton: show the pressed face, and raise Click only after 80 ms
            _clickFlash = True
            RefreshTransparent()
            _pendingClickArgs = e
            _clickRevertTimer.Interval = 80
            _clickRevertTimer.Stop()
            _clickRevertTimer.Start()
        End Sub

        Private Sub OnClickRevertTick(sender As Object, e As EventArgs)
            _clickRevertTimer.Stop()
            _clickFlash = False
            RefreshTransparent()
            Dim args As EventArgs = If(_pendingClickArgs, EventArgs.Empty)
            _pendingClickArgs = Nothing
            MyBase.OnClick(args)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            _pressed = True
            RefreshTransparent()
            If Not _pressTimer.Enabled Then
                _pressButton = ButtonBits(e.Button)
                _pressShift = ModifierBits()
                _pressTimer.Interval = 1000
                _pressTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
            _pressTimer.Stop()
            RefreshTransparent()
        End Sub

        Private Sub OnPressTick(sender As Object, e As EventArgs)
            _pressTimer.Interval = 100   ' first fire waits 1000ms, then repeats every 100ms
            RaiseEvent MousePress(_pressButton, _pressShift)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _mouseHover = True
            If _hoverInterval > 0 Then
                _hoverTimer.Interval = _hoverInterval
                _hoverTimer.Start()
            End If
            RefreshTransparent()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseHover = False
            _pressed = False
            _hoverTimer.Stop()
            RefreshTransparent()
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If Not _mouseHover Then Return
            RaiseEvent MouseHover(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            _focus = True
            RefreshTransparent()
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _focus = False
            RefreshTransparent()
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        ''' <summary>Enter/Space activates the button, same as a real WinForms Button.</summary>
        Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
            MyBase.OnKeyPress(e)
            If e.KeyChar = ControlChars.Cr OrElse e.KeyChar = " "c Then
                OnClick(EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnAutoSizeChanged(e As EventArgs)
            MyBase.OnAutoSizeChanged(e)
            RaiseEvent AutoSizeChanged(Me, e)
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            If Not Enabled Then
                _pressTimer.Stop()
                _hoverTimer.Stop()
                _pressed = False
                _mouseHover = False
            End If
            RefreshTransparent()
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
                _pressTimer.Stop() : _pressTimer.Dispose()
                _hoverTimer.Stop() : _hoverTimer.Dispose()
                _clickRevertTimer.Stop() : _clickRevertTimer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
