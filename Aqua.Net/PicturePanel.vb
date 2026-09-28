Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.PictureBox UserControl (Control\PictureBox.ctl): a container
' (ControlContainer=True) whose background is an Image laid out per SizeMode, optionally shaped by
' colour-key transparency. iPhoto uses it as skinned panels: a small rounded 240x93 skin drawn with
' SizeMode=Fill (nine-slice, tiled -- see Skin.DrawSized) and magenta cut out of the region, with
' other controls placed inside.
'
' Named PicturePanel, not PictureBox: Aqua.PictureBox would shadow System.Windows.Forms.PictureBox
' throughout this project, and ExamControls (frmResource, ImageButtonColorEditor) use the WinForms
' one -- the same clash that made MediaItem's "As Panel" resolve to Aqua.Panel. Same fix as
' Icon.ctl -> IconBox.vb and ListBox.ctl -> ItemListBox.vb.
'
' Inherits System.Windows.Forms.Panel for the same reason Aqua.Panel does: hosting child controls
' placed in the designer is exactly what Panel/ParentControlDesigner already do correctly.
'
' Not ported: VB6 AutoSize (resize the control to the image; iPhoto never turns it on -- and
' Panel.AutoSize already means "grow to fit the children"), the VB6-only Border size mode, the
' Parhelia/Sound*/HoverInterval extras, and ScaleMode (VB6 twips).
Namespace Global.Aqua

    Public Class PicturePanel
        Inherits System.Windows.Forms.Panel

        Private _image As Image
        Private _sizeMode As ImageSizeMode = ImageSizeMode.Normal
        Private _transparency As Boolean = False
        Private _transparencyKey As Color = Color.Black   ' VB6 default vbBlack

        Public Event ImageChanged(sender As Object, e As EventArgs)
        Public Event SizeModeChanged(sender As Object, e As EventArgs)
        Public Event TransparencyChanged(sender As Object, e As EventArgs)
        Public Event TransparencyKeyChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        ''' <summary>Background picture (VB6: Image; the .frm stored it as "Picture").</summary>
        <Category("外觀"), Description("背景圖,依 SizeMode 排列。")>
        Public Property Image As Image
            Get
                Return _image
            End Get
            Set(value As Image)
                If _image Is value Then Return
                _image = value
                UpdateRegion()
                RaiseEvent ImageChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Function ShouldSerializeImage() As Boolean
            Return _image IsNot Nothing
        End Function

        ''' <summary>How Image is laid out; Fill keeps the corners and tiles the rest (VB6: SizeMode).</summary>
        <Category("外觀"), Description("背景圖排列方式;Fill 保留四角、其餘並排填滿。"), DefaultValue(ImageSizeMode.Normal)>
        Public Property SizeMode As ImageSizeMode
            Get
                Return _sizeMode
            End Get
            Set(value As ImageSizeMode)
                If _sizeMode = value Then Return
                _sizeMode = value
                UpdateRegion()
                RaiseEvent SizeModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Cut every TransparencyKey pixel of the laid-out Image out of the panel (VB6: Transparency).
        ''' Child controls in the cut-out area are clipped too, as in VB6.</summary>
        <Category("外觀"), Description("把背景圖中 TransparencyKey 顏色的部分挖空。"), DefaultValue(False)>
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

        <Category("外觀"), Description("Transparency 開啟時要挖空的顏色(iPhoto 用洋紅 FF00FF)。"), DefaultValue(GetType(Color), "Black")>
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

        '=====================================================================
        ' Painting / region
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            MyBase.OnPaintBackground(e)
            If _image IsNot Nothing Then Skin.DrawSized(e.Graphics, _image, ClientRectangle, _sizeMode)
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            If _transparency Then UpdateRegion()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = BuildRegion()
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
            Invalidate()
        End Sub

        ''' <summary>Port of RegionUserControl: VB6 built the region from the drawn UserControl.Picture,
        ''' so the key is tested on the laid-out image (nearest-neighbour, so stretched edges keep the
        ''' exact key colour); anything the image doesn't cover counts as part of the panel.</summary>
        Private Function BuildRegion() As Region
            If Not _transparency OrElse _image Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return Nothing
            Dim opaque As Color = If(_transparencyKey.ToArgb() = Color.White.ToArgb(), Color.Black, Color.White)
            Using bmp As New Bitmap(Width, Height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(opaque)
                    g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                    g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
                    Skin.DrawSized(g, _image, New Rectangle(0, 0, Width, Height), _sizeMode)
                End Using
                Return RegionUtil.CreateRegionFromBitmap(bmp, _transparencyKey)
            End Using
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
