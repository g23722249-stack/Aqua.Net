Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.Icon UserControl (Control\Icon.ctl): a plain, non-focusable surface that
' shows a single Image at its native pixel size, and can optionally clip itself to that image's
' outline via colour-key transparency (RegionUtil.CreateRegionFromBitmap) -- letting an
' irregularly-shaped picture (e.g. a round badge) sit directly on a differently-coloured parent
' without a visible rectangular backing.
'
' Named IconBox, not Icon: VB6's "Icon" doesn't collide with anything in its own namespace, but
' this project imports System.Drawing everywhere (Internal\ShellIcon.vb and others use its Icon
' type, e.g. Icon.FromHandle, constantly) -- a class named Aqua.Icon would shadow that BCL type
' throughout the whole project. Same situation, same fix, as Button.ctl -> FlashButton.vb earlier.
'
' Simplifications vs the VB6 original: CanGetFocus/TabStop=False, and Click/DblClick/MouseDown/
' MouseMove/MouseUp are not re-declared -- Control already raises all of these natively, unlike
' VB6's UserControl which needed each one explicitly relayed from its own events.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class IconBox
        Inherits Control

        Private _transparency As Boolean = True
        Private _transparencyKey As Color = Color.FromArgb(255, 0, 255)   ' RGB(255,0,255), VB6 default
        Private _borderStyle As BorderStyle = BorderStyle.None

        Public Event ImageChanged(sender As Object, e As EventArgs)
        Public Event TransparencyChanged(sender As Object, e As EventArgs)
        Public Event TransparencyKeyChanged(sender As Object, e As EventArgs)
        Public Event BorderStyleChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            MyBase.BackColor = Color.Transparent
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(32, 32)
            End Get
        End Property

        '=====================================================================
        ' Properties
        '=====================================================================
        ''' <summary>The picture shown (VB6: Image/Picture). Setting it snaps the control's own
        ''' Size to the image's native pixel size, matching VB6's SetUserControlPosition -- Icon.ctl
        ''' was always exactly as big as whatever picture it held.</summary>
        <Category("外觀")>
        Public Property Image As Image
            Get
                Return GetImage()
            End Get
            Set(value As Image)
                SetImage(value)
            End Set
        End Property

        Private _image As Image

        Private Function GetImage() As Image
            Return _image
        End Function

        Private Sub SetImage(ByVal value As Image)
            If _image Is value Then Return
            _image = value
            If _image IsNot Nothing Then Size = New Size(_image.Width, _image.Height)
            UpdateRegion()
            Invalidate()
            RaiseEvent ImageChanged(Me, EventArgs.Empty)
        End Sub

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property Transparency As Boolean
            Get
                Return _transparency
            End Get
            Set(value As Boolean)
                If _transparency = value Then Return
                _transparency = value
                UpdateRegion()
                RaiseEvent TransparencyChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property TransparencyKey As Color
            Get
                Return _transparencyKey
            End Get
            Set(value As Color)
                If _transparencyKey = value Then Return
                _transparencyKey = value
                UpdateRegion()
                RaiseEvent TransparencyKeyChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(GetType(BorderStyle), "None")>
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

        ''' <summary>Native pixel width/height of the current image (VB6: ImageWidth/ImageHeight,
        ''' read-only). 0 when there is no image, matching the VB6 Property Get's implicit-Exit-Sub
        ''' (never assigns the return value) behaviour.</summary>
        <Browsable(False)>
        Public ReadOnly Property ImageWidth As Integer
            Get
                Return If(_image?.Width, 0)
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property ImageHeight As Integer
            Get
                Return If(_image?.Height, 0)
            End Get
        End Property

        '=====================================================================
        ' Region (port of RegionUserControl)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = BuildRegion()
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        ''' <summary>Nothing (no clip) unless Transparency is on and there's a real image to key
        ''' against -- an Icon (as opposed to Bitmap) source already carries its own per-pixel alpha
        ''' in .NET, so unlike VB6 there's no separate vbPicTypeIcon check to skip the colour-key
        ''' step for those; a caller supplying an already-alpha Image just won't have any pixels
        ''' that match TransparencyKey, so the scan harmlessly finds nothing to cut.</summary>
        Private Function BuildRegion() As Region
            If Not _transparency OrElse _image Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return Nothing
            Using bmp As New Bitmap(_image)
                Return RegionUtil.CreateRegionFromBitmap(bmp, _transparencyKey)
            End Using
        End Function

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If _transparency Then
                ' the parent's background (BackColor is Transparent): painting nothing left stale
                ' buffer pixels (black) wherever the image doesn't cover -- no image at all, or the
                ' see-through pixels of an alpha PNG, which the colour-key region keeps
                MyBase.OnPaintBackground(e)
            Else
                e.Graphics.Clear(If(BackColor.A > 0, BackColor, SystemColors.Control))
            End If
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If _image IsNot Nothing Then e.Graphics.DrawImageUnscaled(_image, 0, 0)
            If _borderStyle = BorderStyle.FixedSingle Then
                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid)
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
