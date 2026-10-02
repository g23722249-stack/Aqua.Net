Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Windows.Forms

' A simpler ImageButton: one transparent PNG (Image) instead of five state bitmaps plus a colour-keyed
' mask. The states are derived from that one image while painting:
'   mouse over / focused  -> the image as is
'   neither (ExitFocus)   -> the image at ExitFocusOpacity (60%)
'   pressed               -> the image slightly darkened
'   disabled              -> the image in grey scale, also at ExitFocusOpacity
' Transparency comes from the PNG's alpha channel (smooth edges), so there is no window Region:
' like any transparent WinForms control it shows its parent's background, not overlapping siblings.
' Kept from ImageButton: Text over the image, BorderStyle, and the auto-repeating MousePress.
'
' HoverZoom: while enabled and under the mouse, the image bounces up to (1 + HoverZoom) times its size,
' growing away from the edge HoverZoomDirection keeps fixed (Up: the bottom stays put). The control
' never draws outside itself, so give it room for the grown image in the designer: the image then
' rests against that edge (e.g. a 48x48 icon in a 58x58 button sits at the bottom, centred).
Namespace Global.Aqua

    ''' <summary>How PngButton places its image.</summary>
    Public Enum PngSizeMode
        ''' <summary>1:1, centred in the button.</summary>
        CenterImage = 0
        ''' <summary>Scaled to fit the button, keeping its aspect ratio, centred.</summary>
        Zoom = 1
    End Enum

    ''' <summary>Which way PngButton's hover zoom grows (the opposite edge stays fixed).</summary>
    Public Enum PngZoomDirection
        ''' <summary>Grows upwards; the bottom edge stays put.</summary>
        Up = 0
        ''' <summary>Grows downwards; the top edge stays put.</summary>
        Down = 1
        ''' <summary>Grows to the left; the right edge stays put.</summary>
        Left = 2
        ''' <summary>Grows to the right; the left edge stays put.</summary>
        Right = 3
        ''' <summary>Grows evenly from the centre.</summary>
        Center = 4
    End Enum

    <DefaultEvent("Click"), DefaultProperty("Image")>
    Public Class PngButton
        Inherits Control

        Private Const RepeatMs As Integer = 120
        Private Const PressedBrightness As Single = 0.85F

        Private _image As Image
        Private _sizeMode As PngSizeMode = PngSizeMode.CenterImage
        Private _exitFocusOpacity As Single = 0.6F
        Private _borderStyle As BorderStyle = BorderStyle.None
        Private _hoverZoom As Single = 0.2F
        Private _zoomDirection As PngZoomDirection = PngZoomDirection.Up

        ' hover zoom animation: _zoom runs 0 (rest) .. 1 (fully grown)
        Private Const ZoomInMs As Integer = 450
        Private Const ZoomOutMs As Integer = 150
        Private ReadOnly _zoomTimer As New Timer()
        Private _zoom As Single = 0
        Private _zoomFrom As Single = 0
        Private _zoomTo As Single = 0
        Private _zoomStart As Integer
        Private _zoomDuration As Integer

        Private _mouseHover As Boolean = False
        Private _pressed As Boolean = False
        Private _focus As Boolean = False
        Private ReadOnly _repeat As New Timer()
        Private _pressButton As Integer = 0

        ''' <summary>Fired once on press and then every 120 ms while the button is held.</summary>
        Public Event MousePress(button As Integer, shift As Integer)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
            _repeat.Interval = RepeatMs
            AddHandler _repeat.Tick, AddressOf OnRepeatTick
            _zoomTimer.Interval = 15
            AddHandler _zoomTimer.Tick, AddressOf OnZoomTick
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("外觀"), Description("按鈕圖片(建議用透明背景的 PNG)。滑鼠移入或取得焦點時原圖顯示,平時淡化,停用時灰階並淡化。")>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                _image = value
                Invalidate()
            End Set
        End Property

        Private Function ShouldSerializeImage() As Boolean
            Return _image IsNot Nothing
        End Function

        Private Sub ResetImage()
            Image = Nothing
        End Sub

        <Category("外觀"), Description("圖片的擺放方式:原尺寸置中,或等比例縮放到按鈕大小。"), DefaultValue(GetType(PngSizeMode), "CenterImage")>
        Public Property SizeMode As PngSizeMode
            Get
                Return _sizeMode
            End Get
            Set(value As PngSizeMode)
                If _sizeMode = value Then Return
                _sizeMode = value
                Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("沒有焦點、滑鼠也不在上面時(以及停用時)圖片的不透明度(0~1)。"), DefaultValue(0.6F)>
        Public Property ExitFocusOpacity As Single
            Get
                Return _exitFocusOpacity
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, Math.Min(1.0F, value))
                If _exitFocusOpacity = value Then Return
                _exitFocusOpacity = value
                Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("滑鼠經過時彈跳放大的比例(0.2 = 20%);0 = 不放大。只在 Enabled 時有作用。控制項要留出放大後的空間。"), DefaultValue(0.2F)>
        Public Property HoverZoom As Single
            Get
                Return _hoverZoom
            End Get
            Set(value As Single)
                value = Math.Max(0.0F, value)
                If _hoverZoom = value Then Return
                _hoverZoom = value
                Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("彈跳放大的方向;反方向的邊固定不動(Up = 底部不動、往上放大)。"), DefaultValue(GetType(PngZoomDirection), "Up")>
        Public Property HoverZoomDirection As PngZoomDirection
            Get
                Return _zoomDirection
            End Get
            Set(value As PngZoomDirection)
                If _zoomDirection = value Then Return
                _zoomDirection = value
                Invalidate()
            End Set
        End Property

        ''' <summary>Frame drawn around the button (iPhoto flashes FixedSingle on click).</summary>
        <Category("外觀"), DefaultValue(GetType(BorderStyle), "None")>
        Public Property BorderStyle As BorderStyle
            Get
                Return _borderStyle
            End Get
            Set(value As BorderStyle)
                If _borderStyle = value Then Return
                _borderStyle = value
                Invalidate()
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

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            If _image IsNot Nothing Then
                Dim dest As Rectangle = ImageBounds()
                If dest.Width <> _image.Width OrElse dest.Height <> _image.Height Then
                    g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                End If
                Using attr As ImageAttributes = StateAttributes()
                    If attr Is Nothing Then
                        g.DrawImage(_image, dest)
                    Else
                        g.DrawImage(_image, dest, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel, attr)
                    End If
                End Using
            End If

            If Not String.IsNullOrEmpty(Text) Then
                Dim fc As Color = If(Enabled, ForeColor, ColorUtil.OleToColor(12435133))   ' gc_lngDisableForeColor
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fc,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
            End If

            Select Case _borderStyle
                Case BorderStyle.FixedSingle
                    ControlPaint.DrawBorder(g, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid)
                Case BorderStyle.Fixed3D
                    ControlPaint.DrawBorder3D(g, ClientRectangle, Border3DStyle.Sunken)
            End Select
        End Sub

        ''' <summary>Where the image goes: its rest size (1:1, or fitted so the fully grown image still
        ''' fits), grown by the current zoom, against the edge HoverZoomDirection keeps fixed.</summary>
        Private Function ImageBounds() As Rectangle
            Dim w As Double = _image.Width, h As Double = _image.Height
            If _sizeMode = PngSizeMode.Zoom AndAlso w > 0 AndAlso h > 0 Then
                Dim scale As Double = Math.Min(Width / w, Height / h) / (1 + _hoverZoom)
                w *= scale
                h *= scale
            End If
            Dim grow As Double = 1 + _hoverZoom * _zoom
            Dim iw As Integer = CInt(Math.Round(w * grow)), ih As Integer = CInt(Math.Round(h * grow))
            Dim x As Integer = (Width - iw) \ 2, y As Integer = (Height - ih) \ 2
            If _hoverZoom > 0 Then
                Select Case _zoomDirection
                    Case PngZoomDirection.Up : y = Height - ih
                    Case PngZoomDirection.Down : y = 0
                    Case PngZoomDirection.Left : x = Width - iw
                    Case PngZoomDirection.Right : x = 0
                End Select
            End If
            Return New Rectangle(x, y, iw, ih)
        End Function

        '=====================================================================
        ' Hover zoom animation
        '=====================================================================
        Private Sub ZoomTo(ByVal target As Single)
            If _hoverZoom <= 0 OrElse Not Enabled Then target = 0
            If target = _zoomTo AndAlso (_zoomTimer.Enabled OrElse _zoom = target) Then Return
            _zoomFrom = _zoom
            _zoomTo = target
            _zoomStart = Environment.TickCount
            _zoomDuration = If(target > _zoom, ZoomInMs, ZoomOutMs)
            If _zoom = target Then Return
            _zoomTimer.Start()
        End Sub

        ''' <summary>Stops the animation and puts the image back to its rest size at once.</summary>
        Private Sub ResetZoom()
            _zoomTimer.Stop()
            _zoom = 0 : _zoomFrom = 0 : _zoomTo = 0
            Invalidate()
        End Sub

        Private Sub OnZoomTick(sender As Object, e As EventArgs)
            Dim t As Double = Math.Min(1.0, (Environment.TickCount - _zoomStart) / CDbl(_zoomDuration))
            ' growing bounces in (never past the full size, which the designer made room for);
            ' shrinking just eases back
            Dim eased As Double = If(_zoomTo > _zoomFrom, EaseOutBounce(t), 1 - (1 - t) ^ 3)
            _zoom = CSng(_zoomFrom + (_zoomTo - _zoomFrom) * eased)
            If t >= 1 Then
                _zoom = _zoomTo
                _zoomTimer.Stop()
            End If
            Invalidate()
        End Sub

        ''' <summary>Robert Penner's easeOutBounce: reaches 1 at about a third of the time, then drops
        ''' back and lands twice more, each bounce smaller.</summary>
        Private Shared Function EaseOutBounce(ByVal t As Double) As Double
            Const n1 As Double = 7.5625, d1 As Double = 2.75
            If t < 1 / d1 Then
                Return n1 * t * t
            ElseIf t < 2 / d1 Then
                t -= 1.5 / d1 : Return n1 * t * t + 0.75
            ElseIf t < 2.5 / d1 Then
                t -= 2.25 / d1 : Return n1 * t * t + 0.9375
            Else
                t -= 2.625 / d1 : Return n1 * t * t + 0.984375
            End If
        End Function

        ''' <summary>The colour matrix for the current state; Nothing = draw the image as is.</summary>
        Private Function StateAttributes() As ImageAttributes
            Dim m As ColorMatrix
            If Not Enabled Then
                ' grey scale (luminance weights), faded like ExitFocus
                m = New ColorMatrix(New Single()() {
                    New Single() {0.299F, 0.299F, 0.299F, 0, 0},
                    New Single() {0.587F, 0.587F, 0.587F, 0, 0},
                    New Single() {0.114F, 0.114F, 0.114F, 0, 0},
                    New Single() {0, 0, 0, _exitFocusOpacity, 0},
                    New Single() {0, 0, 0, 0, 1}})
            ElseIf _pressed Then
                m = New ColorMatrix()
                m.Matrix00 = PressedBrightness : m.Matrix11 = PressedBrightness : m.Matrix22 = PressedBrightness
            ElseIf _mouseHover OrElse _focus OrElse _exitFocusOpacity >= 1.0F Then
                Return Nothing
            Else
                m = New ColorMatrix()
                m.Matrix33 = _exitFocusOpacity
            End If
            Dim attr As New ImageAttributes()
            attr.SetColorMatrix(m)
            Return attr
        End Function

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
        End Sub

        '=====================================================================
        ' Interaction (state tracking + auto-repeat)
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            _pressed = True
            Invalidate()
            _pressButton = ButtonBits(e.Button)
            RaiseEvent MousePress(_pressButton, 0)
            _repeat.Stop()
            _repeat.Start()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
            _repeat.Stop()
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _mouseHover = True
            ZoomTo(1)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseHover = False
            _pressed = False
            _repeat.Stop()
            ZoomTo(0)
            Invalidate()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            _focus = True
            Invalidate()
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _focus = False
            Invalidate()
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            If Not Enabled Then
                _repeat.Stop()
                _pressed = False
                _mouseHover = False
                ResetZoom()
            End If
            Invalidate()
        End Sub

        Private Sub OnRepeatTick(sender As Object, e As EventArgs)
            If Not _pressed OrElse Not Enabled Then
                _repeat.Stop()
                Return
            End If
            RaiseEvent MousePress(_pressButton, 0)
        End Sub

        Private Shared Function ButtonBits(ByVal b As MouseButtons) As Integer
            Select Case b
                Case MouseButtons.Left : Return 1
                Case MouseButtons.Right : Return 2
                Case MouseButtons.Middle : Return 4
                Case Else : Return 0
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _repeat.Stop()
                _repeat.Dispose()
                _zoomTimer.Stop()
                _zoomTimer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
