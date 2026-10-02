Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Windows.Forms

' Port of the VB6 Aqua.Panel UserControl (Control\Panel.ctl): a themed container panel.
' Inherits System.Windows.Forms.Panel directly (not Control/UserControl like the composed
' text-entry controls) because Panel's whole job in VB6 was ControlContainer=True -- hosting
' arbitrary child controls placed by whoever uses it in the designer. WinForms' own Panel
' already does exactly that correctly (it's what ParentControlDesigner is built for); wrapping
' it and only adding the Aqua chrome on top avoids reinventing container/scroll support and
' avoids the whole class of designer-hang risk composed controls like TextBox.vb have to guard
' against (see that file's notes) -- there's no internal child being added in the constructor here.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class Panel
        Inherits System.Windows.Forms.Panel

        Private _style As PanelStyleMode = PanelStyleMode.Flat
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor
        Private _borderFocusColor As Color = ColorUtil.OleToColor(12434877)
        Private _image As Image
        Private _sizeMode As ImageSizeMode = ImageSizeMode.Normal
        Private _hoverInterval As Integer = 0
        Private _focused As Boolean = False
        Private _mouseHovered As Boolean = False
        Private _soundClick, _soundMouseEnter, _soundMouseHover, _soundMouseLeave, _soundEnterFocus, _soundExitFocus As String
        Private ReadOnly _hoverTimer As New Timer()
        Private ReadOnly _pressTimer As New Timer()
        Private _pressButton As MouseButtons

        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event SizeModeChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Shadows Event PanelStyleChanged(sender As Object, e As EventArgs)
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)
        Public Event MousePress(sender As Object, e As MouseEventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick
            _pressTimer.Interval = 100
            AddHandler _pressTimer.Tick, Sub(sender, e) RaiseEvent MousePress(Me, New MouseEventArgs(_pressButton, 1, 0, 0, 0))
            UpdateRegion()
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If Not _mouseHovered Then
                _mouseHovered = True
                SoundUtil.PlaySound(_soundMouseHover)
                RaiseEvent MouseHover(Me, EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' Parent is reliably non-Nothing by now (unlike in the constructor, or a Resize that may
            ' never fire again if the control keeps its initial/default size) -- re-run so the
            ' corner-repaint request in UpdateRegion actually reaches a real parent at least once.
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("外觀")>
        <DefaultValue(PanelStyleMode.Flat)>
        Public Property PanelStyle As PanelStyleMode
            Get
                Return _style
            End Get
            Set(value As PanelStyleMode)
                If _style = value Then Return
                _style = value
                UpdateRegion()
                Invalidate()
                RaiseEvent PanelStyleChanged(Me, EventArgs.Empty)
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

        <Category("外觀")>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                If _image Is value Then Return
                _image = value
                Invalidate()
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ImageSizeMode.Normal)>
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

        ''' <summary>Milliseconds of continuous hover before MouseHover fires; 0 disables it (VB6: HoverInterval).</summary>
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

        '=====================================================================
        ' Region (port of RegionUserControl)
        '=====================================================================
        Private Sub UpdateRegion()
            Dim rounds As Boolean = (_style = PanelStyleMode.Flat OrElse _style = PanelStyleMode.Container) AndAlso _obtuseness <> ObtusenessMode.None
            Dim old As Region = Me.Region
            Me.Region = If(Not rounds OrElse Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(_obtuseness, Width, Height))
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Painting (ports of DrawUserControl / DrawControlPanelStyle / DrawControlParhelia)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If (IsTextureStyle() OrElse _style = PanelStyleMode.Simulation) AndAlso Parent IsNot Nothing Then
                ' the texture's red rounded corners are keyed out in OnPaint: what shows there must
                ' be the parent's own background, not this panel's BackColor. Simulation is nothing
                ' but the parent's background showing through.
                ButtonRenderer.DrawParentBackground(e.Graphics, ClientRectangle, Me)
            Else
                e.Graphics.Clear(BackColor)
            End If
            If _image IsNot Nothing Then DrawImageFill(e.Graphics)
        End Sub

        Private Function IsTextureStyle() As Boolean
            Return _style <> PanelStyleMode.Flat AndAlso _style <> PanelStyleMode.Container AndAlso _style <> PanelStyleMode.Simulation
        End Function

        Private Sub DrawImageFill(g As Graphics)
            Select Case _sizeMode
                Case ImageSizeMode.HorizontalStretch
                    Skin.DrawStretch(g, _image, ClientRectangle, horizontal:=True)
                Case ImageSizeMode.VerticalStretch
                    Skin.DrawStretch(g, _image, ClientRectangle, horizontal:=False)
                Case ImageSizeMode.CenterImage, ImageSizeMode.Normal
                    Dim x As Integer = (Width - _image.Width) \ 2
                    Dim y As Integer = (Height - _image.Height) \ 2
                    g.DrawImage(_image, New Rectangle(x, y, _image.Width, _image.Height))
                Case Else ' StretchImage / Fill
                    g.DrawImage(_image, ClientRectangle)
            End Select
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim g As Graphics = e.Graphics
            Select Case _style
                Case PanelStyleMode.Flat
                    DrawBevelBorder(g, _borderColor, inset:=0)
                Case PanelStyleMode.Container
                    DrawBevelBorder(g, If(_focused, _borderFocusColor, _borderColor), inset:=0)
                    If _focused Then BorderPainter.DrawParheliaGlow(g, Width, Height, _borderFocusColor)
                Case PanelStyleMode.Simulation
                    ' VB6 faked transparency here by re-painting a snapshot of the parent form's own
                    ' background bitmap (Extender.Container.Picture/Image); OnPaintBackground paints
                    ' the real parent's background instead.
                Case Else ' texture styles
                    Dim surface As Bitmap = TextureSurface()
                    If surface IsNot Nothing Then
                        ' only the part being repainted: a transparent child (a PngButton animating
                        ' on the tool bar) repaints just its own rectangle of this panel every frame
                        Dim clip As Rectangle = Rectangle.Intersect(e.ClipRectangle, New Rectangle(Point.Empty, surface.Size))
                        If Not clip.IsEmpty Then g.DrawImage(surface, clip, clip, GraphicsUnit.Pixel)
                    End If
            End Select
        End Sub

        Private _textureSurface As Bitmap
        Private _textureStyle As PanelStyleMode

        ''' <summary>The texture laid out over the whole panel, its red outline already transparent;
        ''' built once per size/style instead of on every paint.
        ''' The source bitmap outlines its rounded-corner boundary in pure red, which VB6 punched out via
        ''' a CreateFromPicture/SetWindowRgn(TransparencyColor=vbRed) mask; keying red out gets the same
        ''' result without a Region: OnPaintBackground has painted the parent's background underneath,
        ''' which then shows through. VB6 DrawControlTexture drew it with Quartz SizeMode Fill: nine-
        ''' slice, corners at native size and edges/centre tiled (Skin.DrawFill). Stretching the whole
        ''' small bitmap instead smeared the rounded ends into wide grey blocks on long panels.</summary>
        Private Function TextureSurface() As Bitmap
            If Width <= 0 OrElse Height <= 0 Then Return Nothing
            If _textureSurface IsNot Nothing AndAlso _textureStyle = _style AndAlso _textureSurface.Size = Size Then Return _textureSurface
            DropTextureSurface()
            Dim tex As Image = PanelResources.GetSurface(_style)
            If tex Is Nothing Then Return Nothing
            Dim surface As New Bitmap(Width, Height, PixelFormat.Format32bppPArgb)   ' PArgb: fastest to draw
            Using filled As New Bitmap(Width, Height)
                Using fg As Graphics = Graphics.FromImage(filled)
                    Skin.DrawFill(fg, tex, New Rectangle(0, 0, Width, Height))
                End Using
                Using sg As Graphics = Graphics.FromImage(surface), attrs As New ImageAttributes()
                    attrs.SetColorKey(System.Drawing.Color.Red, System.Drawing.Color.Red)
                    sg.DrawImage(filled, New Rectangle(0, 0, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, attrs)
                End Using
            End Using
            _textureSurface = surface
            _textureStyle = _style
            Return surface
        End Function

        Private Sub DropTextureSurface()
            If _textureSurface Is Nothing Then Return
            _textureSurface.Dispose()
            _textureSurface = Nothing
        End Sub

        ''' <summary>Port of DrawControlFlat/DrawControlBorder: a 2px outer line, a darker 1px inset
        ''' shade line, and an even darker 1px accent along the top edge.</summary>
        Private Sub DrawBevelBorder(g As Graphics, lineColor As Color, inset As Integer)
            If Width <= 2 OrElse Height <= 2 Then Return
            Using p As New Pen(lineColor, 2)
                g.DrawRectangle(p, inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2)
            End Using
            Dim shade As Color = ColorUtil.ShiftChannels(lineColor, -24)
            Using p As New Pen(shade, 1)
                g.DrawRectangle(p, inset + 1, inset + 1, Width - 3 - inset * 2, Height - 3 - inset * 2)
            End Using
            Dim topAccent As Color = ColorUtil.ShiftChannels(shade, -36)
            Using p As New Pen(topAccent, 1)
                g.DrawLine(p, inset + 2, inset + 1, Width - 3 - inset, inset + 1)
            End Using
        End Sub

        Protected Overrides Sub OnEnter(e As EventArgs)
            MyBase.OnEnter(e)
            If _style = PanelStyleMode.Container Then
                _focused = True
                SoundUtil.PlaySound(_soundEnterFocus)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnLeave(e As EventArgs)
            MyBase.OnLeave(e)
            If _style = PanelStyleMode.Container Then
                _focused = False
                SoundUtil.PlaySound(_soundExitFocus)
                Invalidate()
            End If
        End Sub

        '=====================================================================
        ' Mouse interaction (hover timer, click, press auto-repeat)
        '=====================================================================
        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _mouseHovered = False
            SoundUtil.PlaySound(_soundMouseEnter)
            If _hoverInterval > 0 Then
                _hoverTimer.Interval = Math.Max(1, _hoverInterval)
                _hoverTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverTimer.Stop()
            _mouseHovered = False
            SoundUtil.PlaySound(_soundMouseLeave)
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            SoundUtil.PlaySound(_soundClick)
            MyBase.OnClick(e)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            _pressButton = e.Button
            _pressTimer.Start()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressTimer.Stop()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _hoverTimer.Stop() : _hoverTimer.Dispose()
                _pressTimer.Stop() : _pressTimer.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
                DropTextureSurface()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
