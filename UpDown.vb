Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Standalone port of the VB6 Aqua.UpDown UserControl (Control\UpDown.ctl): a spinner
' built from two Aqua.ImageButtons whose skins are the original frmResSpin surfaces with
' the direction arrow composited on. Vertical shows Up/Down, horizontal shows Left/Right.
' Holding a button auto-repeats via ImageButton.MousePress.
Namespace Global.Aqua

    <DefaultEvent("ValueChanged")>
    Public Class UpDown
        Inherits Control

        ' spin directions (enumDirection): edUp=0 edDown=1 edLeft=2 edRight=3
        Private Const DirUp As Integer = 0
        Private Const DirDown As Integer = 1
        Private Const DirLeft As Integer = 2
        Private Const DirRight As Integer = 3

        Private ReadOnly _dec As New ImageButton()   ' up / left
        Private ReadOnly _inc As New ImageButton()   ' down / right
        Private _orientation As OrientationMode = OrientationMode.Vertical
        Private _color As ColorConstants = ColorConstants.Blue
        ' VB6's own UserControl_InitProperties default is Independence (rounded), not Alignment.
        Private _style As UpDownStyle = UpDownStyle.Independence
        Private _min As Integer = 0
        Private _max As Integer = 100
        Private _value As Integer = 0

        Public Event IncrementClick(sender As Object, e As EventArgs)
        Public Event DecrementClick(sender As Object, e As EventArgs)
        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event Change(sender As Object, e As EventArgs)
        Public Event OrientationChanged(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            ' RebuildSkins() (run from OnHandleCreated, before any painting) sets the real SizeMode
            ' per orientation (three-slice stretch); this is just a harmless placeholder until then.
            _dec.SizeMode = ImageSizeMode.VerticalStretch
            _inc.SizeMode = ImageSizeMode.VerticalStretch
            _dec.TabStop = False
            _inc.TabStop = False
            Controls.Add(_dec)
            Controls.Add(_inc)
            AddHandler _dec.MousePress, Sub(b, s) Decrement()
            AddHandler _inc.MousePress, Sub(b, s) Increment()
            UpdateRegion()
        End Sub

        ''' <summary>Port of RegionUserControl: Independence style is cut to VB6's spin mask, stretched
        ''' along the orientation (a generic rounded rectangle didn't follow the art and left its light
        ''' corners showing); Alignment is square, meant to butt flush against an adjacent control.</summary>
        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(_style <> UpDownStyle.Independence,
                           Nothing,
                           RegionUtil.CreateStretchedMaskRegion(ScrollBarResources.GetSpinMask(_orientation), Width, Height,
                                                                horizontal:=(_orientation = OrientationMode.Horizontal)))
            If old IsNot Nothing Then old.Dispose()
            ' SetWindowRgn on a child doesn't itself make the parent repaint the corner pixels the
            ' new (smaller) region just exposed -- without this they can be left showing whatever was
            ' there before (stale/garbage), instead of the parent's real current background.
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        Public Property Orientation As OrientationMode
            Get
                Return _orientation
            End Get
            Set(value As OrientationMode)
                If _orientation = value Then Return
                _orientation = value
                UpdateRegion()   ' the spin mask differs per orientation
                DoLayout()
                RebuildSkins()
                RaiseEvent OrientationChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                RebuildSkins()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property Style As UpDownStyle
            Get
                Return _style
            End Get
            Set(value As UpDownStyle)
                If _style = value Then Return
                _style = value
                UpdateRegion()
            End Set
        End Property

        <DefaultValue(0)>
        Public Property Minimum As Integer
            Get
                Return _min
            End Get
            Set(value As Integer)
                _min = value
                If _value < _min Then Value = _min
            End Set
        End Property

        <DefaultValue(100)>
        Public Property Maximum As Integer
            Get
                Return _max
            End Get
            Set(value As Integer)
                _max = value
                If _value > _max Then Value = _max
            End Set
        End Property

        <DefaultValue(0)>
        Public Property Value As Integer
            Get
                Return _value
            End Get
            Set(v As Integer)
                Dim nv As Integer = v
                If nv < _min Then nv = _min
                If nv > _max Then nv = _max
                If _value = nv Then Return
                _value = nv
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
                RaiseEvent Change(Me, EventArgs.Empty)
            End Set
        End Property

        Public Sub Increment()
            If _value < _max Then
                Value = _value + 1
                RaiseEvent IncrementClick(Me, EventArgs.Empty)
            End If
        End Sub

        Public Sub Decrement()
            If _value > _min Then
                Value = _value - 1
                RaiseEvent DecrementClick(Me, EventArgs.Empty)
            End If
        End Sub

        '=====================================================================
        ' Skinning + layout
        '=====================================================================
        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' Parent is reliably non-Nothing by now (unlike in the constructor, or Resize events that
            ' may never fire again if the control keeps its initial/default size) -- re-run so the
            ' corner-repaint request in UpdateRegion actually reaches a real parent at least once.
            UpdateRegion()
            DoLayout()
            RebuildSkins()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            DoLayout()
            RebuildSkins()   ' the composited surfaces are pre-sized to _dec/_inc's own bounds -- must redo whenever those change
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _dec.Enabled = Enabled
            _inc.Enabled = Enabled
        End Sub

        Private Sub DoLayout()
            If _orientation = OrientationMode.Horizontal Then
                Dim half As Integer = Width \ 2
                _dec.SetBounds(0, 0, half, Height)
                _inc.SetBounds(half, 0, Width - half, Height)
            Else
                Dim half As Integer = Height \ 2
                _dec.SetBounds(0, 0, Width, half)
                _inc.SetBounds(0, half, Width, Height - half)
            End If
        End Sub

        ''' <summary>
        ''' Composites each state's surface (three-slice stretched to the button's own current size,
        ''' rounded end caps kept crisp) with the direction arrow drawn at its natural size on top --
        ''' pre-baked to the final pixel size so ImageButton can just draw it 1:1 (SizeMode Normal).
        ''' Must re-run whenever _dec/_inc's own size changes (OnResize), not just on Orientation/Color.
        ''' </summary>
        Private Sub RebuildSkins()
            _dec.SizeMode = ImageSizeMode.Normal
            _inc.SizeMode = ImageSizeMode.Normal

            Dim horizontal As Boolean = (_orientation = OrientationMode.Horizontal)
            Dim decDir As Integer = If(horizontal, DirLeft, DirUp)
            Dim incDir As Integer = If(horizontal, DirRight, DirDown)
            ApplySkin(_dec, decDir, horizontal)
            ApplySkin(_inc, incDir, horizontal)
        End Sub

        Private Sub ApplySkin(ByVal btn As ImageButton, ByVal dir As Integer, ByVal horizontal As Boolean)
            Dim w As Integer = btn.Width
            Dim h As Integer = btn.Height
            btn.ExitFocusImage = Composite(ScrollBarResources.GetSpinSurface(ScrollState.ExitFocus, _color, dir),
                                           ScrollBarResources.GetSpinArrow(dir, True), w, h, horizontal)
            btn.EnterFocusImage = btn.ExitFocusImage
            btn.MouseHoverImage = Composite(ScrollBarResources.GetSpinSurface(ScrollState.Hover, _color, dir),
                                            ScrollBarResources.GetSpinArrow(dir, True), w, h, horizontal)
            btn.ClickImage = Composite(ScrollBarResources.GetSpinSurface(ScrollState.Click, _color, dir),
                                       ScrollBarResources.GetSpinArrow(dir, True), w, h, horizontal)
            btn.DisableImage = Composite(ScrollBarResources.GetSpinSurface(ScrollState.Deactivate, _color, dir),
                                         ScrollBarResources.GetSpinArrow(dir, False), w, h, horizontal)
            btn.Invalidate()
        End Sub

        ''' <summary>Three-slice-stretches the surface to (targetWidth, targetHeight), then draws the
        ''' arrow centred at its natural size on top -- NOT baked in before stretching, which would
        ''' smear the arrow across the stretched middle instead of keeping it crisp.</summary>
        Private Shared Function Composite(ByVal surface As Image, ByVal arrow As Image,
                                          ByVal targetWidth As Integer, ByVal targetHeight As Integer,
                                          ByVal horizontal As Boolean) As Image
            If targetWidth <= 0 OrElse targetHeight <= 0 Then Return If(surface, arrow)
            If surface Is Nothing Then Return arrow

            Dim bmp As New Bitmap(targetWidth, targetHeight)
            Using g = Graphics.FromImage(bmp)
                Skin.DrawStretch(g, surface, New Rectangle(0, 0, targetWidth, targetHeight), horizontal)
                If arrow IsNot Nothing Then
                    Dim x As Integer = (targetWidth - arrow.Width) \ 2
                    Dim y As Integer = (targetHeight - arrow.Height) \ 2
                    g.DrawImage(arrow, New Rectangle(x, y, arrow.Width, arrow.Height))
                End If
            End Using
            Return bmp
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
