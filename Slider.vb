Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Port of the VB6 Aqua.Slider UserControl (Control\Slider.ctl): a draggable tick/thumb slider,
' horizontal or vertical, with optional tick marks above/below (or left/right of) the track.
' Owner-drawn Control (no children), the same shape as ScrollBar.vb/Headers.vb for painting --
' but unlike those, VB6's Slider also masked the whole control down to just the visible
' track+thumb pixels via a CreateFromPicture/SetWindowRgn(TransparencyColor=vbWhite) trick, since
' its bounding rectangle is mostly empty space around a thin groove and a round thumb. Here the
' parent's background is painted under the art instead, and the art's white surround is made
' transparent when loaded (SliderResources): an earlier GraphicsPath Region (groove band + thumb
' ellipse) left the white corners of the groove ends and the thumb showing.
Namespace Global.Aqua

    <DefaultEvent("ValueChanged")>
    Public Class Slider
        Inherits Control

        Private _min As Integer = 0
        Private _max As Integer = 10
        Private _value As Integer = 0
        Private _orientation As OrientationMode = OrientationMode.Horizontal
        Private _color As ColorConstants = ColorConstants.Blue
        Private _tickStyle As SliderTickMode = SliderTickMode.NoTicks
        Private _showTicks As Boolean = True
        Private _focused As Boolean = False
        Private _dragging As Boolean = False
        Private _soundClick, _soundEnterFocus, _soundExitFocus As String

        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event OrientationChanged(sender As Object, e As EventArgs)
        Public Shadows Event TickStyleChanged(sender As Object, e As EventArgs)
        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event ShowTicksChanged(sender As Object, e As EventArgs)
        Public Event MinChanged(sender As Object, e As EventArgs)
        Public Event MaxChanged(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.Selectable, True)
            UpdateRegion()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' Parent is reliably non-Nothing by now (unlike in the constructor, or a Resize that may
            ' never fire again if the control keeps its initial/default size) -- re-run so the
            ' corner-repaint request in UpdateRegion actually reaches a real parent at least once.
            UpdateRegion()
        End Sub

        ''' <summary>Repaints after the groove/thumb layout changed (Value, Orientation, TickStyle, Resize).
        ''' There is no window Region any more: the parent's background is painted underneath and the
        ''' art's white surround is transparent (see OnPaintBackground / SliderResources).</summary>
        Private Sub UpdateRegion()
            Invalidate()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(150, 24)
            End Get
        End Property

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("行為")>
        <DefaultValue(0)>
        Public Property Minimum As Integer
            Get
                Return _min
            End Get
            Set(value As Integer)
                If _min = value Then Return
                _min = value
                If _value < _min Then Value = _min
                UpdateRegion()
                Invalidate()
                RaiseEvent MinChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(10)>
        Public Property Maximum As Integer
            Get
                Return _max
            End Get
            Set(value As Integer)
                If _max = value Then Return
                _max = value
                If _value > _max Then Value = _max
                UpdateRegion()
                Invalidate()
                RaiseEvent MaxChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(0)>
        Public Property Value As Integer
            Get
                Return _value
            End Get
            Set(v As Integer)
                Dim nv As Integer = Math.Max(_min, Math.Min(_max, v))
                If _value = nv Then Return
                _value = nv
                UpdateRegion()
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(OrientationMode.Horizontal)>
        Public Property Orientation As OrientationMode
            Get
                Return _orientation
            End Get
            Set(value As OrientationMode)
                If _orientation = value Then Return
                _orientation = value
                UpdateRegion()
                Invalidate()
                RaiseEvent OrientationChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ColorConstants.Blue)>
        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                Invalidate()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(SliderTickMode.NoTicks)>
        Public Shadows Property TickStyle As SliderTickMode
            Get
                Return _tickStyle
            End Get
            Set(value As SliderTickMode)
                If _tickStyle = value Then Return
                _tickStyle = value
                UpdateRegion()
                Invalidate()
                RaiseEvent TickStyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property ShowTicks As Boolean
            Get
                Return _showTicks
            End Get
            Set(value As Boolean)
                If _showTicks = value Then Return
                _showTicks = value
                Invalidate()
                RaiseEvent ShowTicksChanged(Me, EventArgs.Empty)
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
        ' Layout (ports of GetTickBarStepArray / SetTickBarPos / SetTickBarValue)
        '=====================================================================
        Private ReadOnly Property IsHorizontal As Boolean
            Get
                Return _orientation = OrientationMode.Horizontal
            End Get
        End Property

        Private Function ThumbImage() As Image
            Return SliderResources.GetThumb(_orientation, _color, _tickStyle)
        End Function

        Private Function ThumbSize() As Size
            Dim img As Image = ThumbImage()
            Return If(img IsNot Nothing, img.Size, New Size(12, 18))
        End Function

        Private Function TrackLength() As Integer
            Dim ts As Size = ThumbSize()
            Return If(IsHorizontal, Width - ts.Width, Height - ts.Height)
        End Function

        ''' <summary>Pixel offset of the thumb's leading edge along the track for the current Value.</summary>
        Private Function ThumbOffset() As Integer
            Dim span As Integer = _max - _min
            If span <= 0 Then Return 0
            Dim track As Integer = Math.Max(0, TrackLength())
            Return CInt(Math.Round(track * (_value - _min) / CDbl(span)))
        End Function

        Private Function ThumbRect() As Rectangle
            Dim ts As Size = ThumbSize()
            Dim off As Integer = ThumbOffset()
            If IsHorizontal Then
                Return New Rectangle(off, (Height - ts.Height) \ 2, ts.Width, ts.Height)
            Else
                Return New Rectangle((Width - ts.Width) \ 2, off, ts.Width, ts.Height)
            End If
        End Function

        ''' <summary>Port of SetTickBarValue: nearest Value for a thumb leading-edge pixel offset.</summary>
        Private Function ValueFromOffset(ByVal offset As Integer) As Integer
            Dim span As Integer = _max - _min
            If span <= 0 Then Return _min
            Dim track As Integer = Math.Max(1, TrackLength())
            Dim v As Integer = _min + CInt(Math.Round(span * offset / CDbl(track)))
            Return Math.Max(_min, Math.Min(_max, v))
        End Function

        '=====================================================================
        ' Painting (ports of SetUsercontrolResource / DrawTickBar)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' VB6 cut the control down to the groove + thumb pixels; showing the parent's own
            ' background everywhere else gives the same look (with the smooth thumb edge intact)
            If Parent IsNot Nothing Then
                ButtonRenderer.DrawParentBackground(e.Graphics, ClientRectangle, Me)
            Else
                e.Graphics.Clear(BackColor)
            End If
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics

            Dim groove As Image = SliderResources.GetBackground(_orientation)
            If groove IsNot Nothing Then
                If IsHorizontal Then
                    Dim y As Integer = (Height - groove.Height) \ 2
                    Skin.DrawStretch(g, groove, New Rectangle(0, y, Width, groove.Height), horizontal:=True)
                Else
                    Dim x As Integer = (Width - groove.Width) \ 2
                    Skin.DrawStretch(g, groove, New Rectangle(x, 0, groove.Width, Height), horizontal:=False)
                End If
            End If

            If _showTicks AndAlso _tickStyle <> SliderTickMode.NoTicks Then DrawTicks(g)

            Dim thumb As Image = ThumbImage()
            If thumb IsNot Nothing Then g.DrawImage(thumb, ThumbRect())
        End Sub

        ''' <summary>Port of the tick-line loop in SetUsercontrolResource.</summary>
        Private Sub DrawTicks(g As Graphics)
            Dim span As Integer = _max - _min
            If span <= 0 Then Return
            Dim ts As Size = ThumbSize()
            Using p As New Pen(System.Drawing.Color.Black)
                For v As Integer = _min To _max
                    Dim off As Integer = CInt(Math.Round(Math.Max(0, TrackLength()) * (v - _min) / CDbl(span)))
                    If IsHorizontal Then
                        Dim x As Integer = off + ts.Width \ 2
                        If _tickStyle = SliderTickMode.TopLeft Then
                            g.DrawLine(p, x, Height \ 2 - 14, x, Height \ 2 - 19)
                        Else ' BottomRight
                            g.DrawLine(p, x, Height \ 2 + 17, x, Height \ 2 + 12)
                        End If
                    Else
                        Dim y As Integer = off + ts.Height \ 2
                        If _tickStyle = SliderTickMode.TopLeft Then
                            g.DrawLine(p, Width \ 2 - 14, y, Width \ 2 - 19, y)
                        Else
                            g.DrawLine(p, Width \ 2 + 17, y, Width \ 2 + 12, y)
                        End If
                    End If
                Next
            End Using
        End Sub

        '=====================================================================
        ' Interaction (ports of UserControl_MouseDown/MouseMove/MouseUp, UserControl_KeyDown)
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If e.Button <> MouseButtons.Left Then Return
            _dragging = True
            SoundUtil.PlaySound(_soundClick)
            Dim off As Integer = If(IsHorizontal, e.X - ThumbSize().Width \ 2, e.Y - ThumbSize().Height \ 2)
            Value = ValueFromOffset(off)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If Not _dragging Then Return
            Dim off As Integer = If(IsHorizontal, e.X - ThumbSize().Width \ 2, e.Y - ThumbSize().Height \ 2)
            Value = ValueFromOffset(off)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
        End Sub

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Left, Keys.Up
                    Value -= 1
                Case Keys.Right, Keys.Down
                    Value += 1
                Case Keys.PageUp
                    Value = _min
                Case Keys.PageDown
                    Value = _max
            End Select
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            _focused = True
            SoundUtil.PlaySound(_soundEnterFocus)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _focused = False
            SoundUtil.PlaySound(_soundExitFocus)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
            Invalidate()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            Invalidate()
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
