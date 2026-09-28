Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Standalone port of the VB6 Aqua.ImageButton UserControl (Control\ImageButton.ctl):
' a button that shows one of five state images (Disable / ExitFocus / EnterFocus /
' MouseHover / Click) fitted per SizeMode, and raises Click plus an auto-repeating
' MousePress while the button is held (the VB6 tmrMousePress, 120 ms).
'
' MaskImage + TransparencyKey give the button an irregular outline the same way VB6's
' RegionUserControl did: the mask is laid out exactly like the state images (same SizeMode
' placement) and every pixel matching TransparencyKey is cut out of the window region.
' Not ported: VB6's SizeMode=Normal also resized the control to the mask/ExitFocus image. Forms
' ported from VB6 already carry that final size, and UpDown relies on Normal meaning "draw 1:1,
' keep my size", so Normal keeps the size it is given here.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class ImageButton
        Inherits Control

        Private Const RepeatMs As Integer = 120

        Private _disable As Image
        Private _exitFocus As Image
        Private _enterFocus As Image
        Private _hover As Image
        Private _click As Image
        Private _mask As Image
        Private _transparencyKey As Color = Color.Black   ' VB6 default vbBlack
        Private _borderStyle As BorderStyle = BorderStyle.None
        Private _sizeMode As ImageSizeMode = ImageSizeMode.Normal

        Private _mouseHover As Boolean = False
        Private _pressed As Boolean = False
        Private _focus As Boolean = False
        Private ReadOnly _repeat As New Timer()
        Private _pressButton As Integer = 0
        Private _pressShift As Integer = 0

        ''' <summary>Fired once on press and then every 120 ms while the button is held.</summary>
        Public Event MousePress(button As Integer, shift As Integer)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)
        Public Event SizeModeChanged(sender As Object, e As EventArgs)
        Public Event BorderStyleChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            BackColor = System.Drawing.Color.Transparent
            _repeat.Interval = RepeatMs
            AddHandler _repeat.Tick, AddressOf OnRepeatTick
        End Sub

        '=====================================================================
        ' State image properties
        '=====================================================================
        ' Fallbacks when a state has no image: Disable -> ExitFocus, Click -> MouseHover,
        ' MouseHover -> ExitFocus, EnterFocus -> ExitFocus (see CurrentImage).
        <Category("外觀"), Description("停用時的圖;未設定時用 ExitFocusImage。")>
        Public Property DisableImage As Image
            Get
                Return _disable
            End Get
            Set(value As Image)
                _disable = value : Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("一般狀態(沒有焦點)的圖,也是其他狀態缺圖時的預設圖。")>
        Public Property ExitFocusImage As Image
            Get
                Return _exitFocus
            End Get
            Set(value As Image)
                _exitFocus = value : Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("取得焦點時的圖;未設定時用 ExitFocusImage。")>
        Public Property EnterFocusImage As Image
            Get
                Return _enterFocus
            End Get
            Set(value As Image)
                _enterFocus = value : Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("滑鼠停在上面時的圖;未設定時用 ExitFocusImage。")>
        Public Property MouseHoverImage As Image
            Get
                Return _hover
            End Get
            Set(value As Image)
                _hover = value : Invalidate()
            End Set
        End Property

        <Category("外觀"), Description("按下時的圖;未設定時用 MouseHoverImage。")>
        Public Property ClickImage As Image
            Get
                Return _click
            End Get
            Set(value As Image)
                _click = value : Invalidate()
            End Set
        End Property

        ''' <summary>Outline mask: pixels matching TransparencyKey are cut out of the button (VB6: MaskImage).
        ''' Laid out with the same SizeMode placement as the state images. Nothing = rectangular button.</summary>
        <Category("外觀"), Description("外形遮罩:與 TransparencyKey 同色的像素會被挖空;未設定則為矩形。")>
        Public Property MaskImage As Image
            Get
                Return _mask
            End Get
            Set(value As Image)
                _mask = value
                UpdateRegion()
            End Set
        End Property

        ''' <summary>The MaskImage colour treated as "outside the button" (VB6: TransparencyKey, default black).</summary>
        <Category("外觀"), Description("MaskImage 中代表「按鈕外」的顏色。"), DefaultValue(GetType(Color), "Black")>
        Public Property TransparencyKey As Color
            Get
                Return _transparencyKey
            End Get
            Set(value As Color)
                If _transparencyKey = value Then Return
                _transparencyKey = value
                UpdateRegion()
            End Set
        End Property

        ''' <summary>Frame drawn around the button (VB6: BorderStyle; iPhoto flashes FixedSingle on click).</summary>
        <Category("外觀"), DefaultValue(GetType(BorderStyle), "None")>
        Public Property BorderStyle As BorderStyle
            Get
                Return _borderStyle
            End Get
            Set(value As BorderStyle)
                If _borderStyle = value Then Return
                _borderStyle = value
                Invalidate()
                RaiseEvent BorderStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Function ShouldSerializeDisableImage() As Boolean
            Return _disable IsNot Nothing
        End Function
        Private Function ShouldSerializeExitFocusImage() As Boolean
            Return _exitFocus IsNot Nothing
        End Function
        Private Function ShouldSerializeEnterFocusImage() As Boolean
            Return _enterFocus IsNot Nothing
        End Function
        Private Function ShouldSerializeMouseHoverImage() As Boolean
            Return _hover IsNot Nothing
        End Function
        Private Function ShouldSerializeClickImage() As Boolean
            Return _click IsNot Nothing
        End Function
        Private Function ShouldSerializeMaskImage() As Boolean
            Return _mask IsNot Nothing
        End Function

        Public Property SizeMode As ImageSizeMode
            Get
                Return _sizeMode
            End Get
            Set(value As ImageSizeMode)
                If _sizeMode = value Then Return
                _sizeMode = value
                UpdateRegion()
                Invalidate()
                RaiseEvent SizeModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        '=====================================================================
        ' Painting
        '=====================================================================
        Private Function CurrentImage() As Image
            If Not Enabled Then Return If(_disable, _exitFocus)
            If _pressed Then Return If(_click, _hover)
            If _mouseHover Then Return If(_hover, _exitFocus)
            If _focus Then Return If(_enterFocus, _exitFocus)
            Return _exitFocus
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim img As Image = CurrentImage()
            If img IsNot Nothing Then DrawFitted(g, img)

            ' VB6 DrawUserControl printed the caption centred over the image
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

        ''' <summary>Lays an image out per SizeMode. Shared by painting and the mask region so the
        ''' cut-out outline lines up with the drawn image exactly.</summary>
        Private Sub DrawFitted(ByVal g As Graphics, ByVal img As Image)
            Select Case _sizeMode
                Case ImageSizeMode.Normal, ImageSizeMode.CenterImage, ImageSizeMode.AutoSize
                    Dim x As Integer = (Width - img.Width) \ 2
                    Dim y As Integer = (Height - img.Height) \ 2
                    g.DrawImage(img, New Rectangle(x, y, img.Width, img.Height))
                Case ImageSizeMode.HorizontalStretch
                    Skin.DrawStretch(g, img, ClientRectangle, horizontal:=True)
                Case ImageSizeMode.VerticalStretch
                    Skin.DrawStretch(g, img, ClientRectangle, horizontal:=False)
                Case Else ' StretchImage / Fill
                    g.DrawImage(img, ClientRectangle)
            End Select
        End Sub

        '=====================================================================
        ' Region (port of RegionUserControl)
        '=====================================================================
        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = BuildRegion()
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
            Invalidate()
        End Sub

        ''' <summary>Nothing (plain rectangle) without a MaskImage. Otherwise the mask is laid out on a
        ''' TransparencyKey-filled canvas the size of the button -- so the area outside the mask is cut
        ''' too -- with nearest-neighbour scaling so stretched edges keep the exact key colour.</summary>
        Private Function BuildRegion() As Region
            If _mask Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return Nothing
            Using bmp As New Bitmap(Width, Height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(_transparencyKey)
                    g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                    g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
                    DrawFitted(g, _mask)
                End Using
                Return RegionUtil.CreateRegionFromBitmap(bmp, _transparencyKey)
            End Using
        End Function

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            If _mask IsNot Nothing Then UpdateRegion()
        End Sub

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
            _pressShift = 0
            RaiseEvent MousePress(_pressButton, _pressShift)
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
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseHover = False
            _pressed = False
            _repeat.Stop()
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
            If Not Enabled Then _repeat.Stop()
            Invalidate()
        End Sub

        Private Sub OnRepeatTick(sender As Object, e As EventArgs)
            If Not _pressed OrElse Not Enabled Then
                _repeat.Stop()
                Return
            End If
            RaiseEvent MousePress(_pressButton, _pressShift)
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
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
