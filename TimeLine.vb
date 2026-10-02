Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.TimeLine UserControl (Control\TimeLine.ctl): a horizontal seek/scrubber
' bar (CurrentPosition of 0..Duration, in 1/1000s per the VB6 comment) with a thin draggable
' thumb. Owner-drawn Control (no children), same reasoning as ProgressBar.vb/Slider.vb.
Namespace Global.Aqua

    <DefaultEvent("CurrentPositionChanged")>
    Public Class TimeLine
        Inherits Control

        Private Const DefaultDuration As Integer = 30000   ' mc_lngDefaultDuration (1/1000s)
        Private Const ThumbWidth As Integer = 2

        Private _duration As Integer = DefaultDuration
        Private _position As Integer = 0
        Private _color As ColorConstants = ColorConstants.Blue
        Private _style As TimeLineStyleConstants = TimeLineStyleConstants.Adjustable
        Private _dragging As Boolean = False

        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event CurrentPositionChanged(sender As Object, e As EventArgs)
        Public Event SlideChanged(sender As Object, e As EventArgs)
        Public Shadows Event StyleChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            TabStop = False
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(180, 16)
            End Get
        End Property

        <Category("行為")>
        <DefaultValue(0)>
        Public Property CurrentPosition As Integer
            Get
                Return _position
            End Get
            Set(v As Integer)
                Dim nv As Integer = Math.Max(0, Math.Min(_duration, v))
                If _position = nv Then Return
                _position = nv
                Invalidate()
                RaiseEvent CurrentPositionChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(DefaultDuration)>
        Public Property Duration As Integer
            Get
                Return _duration
            End Get
            Set(value As Integer)
                If value < 0 Then Throw New ArgumentOutOfRangeException(NameOf(value))
                If _duration = value Then Return
                _duration = value
                If _position > _duration Then _position = _duration
                Invalidate()
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

        ''' <summary>Adjustable (draggable thumb) or DisplayOnly (VB6: Style).</summary>
        <Category("行為")>
        <DefaultValue(TimeLineStyleConstants.Adjustable)>
        Public Shadows Property Style As TimeLineStyleConstants
            Get
                Return _style
            End Get
            Set(value As TimeLineStyleConstants)
                If _style = value Then Return
                _style = value
                Invalidate()
                RaiseEvent StyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Function FillWidth() As Integer
            If _duration <= 0 Then Return 0
            If _position <= 0 Then Return 0
            If _position >= _duration Then Return Width
            Return CInt(Width * (_position / CDbl(_duration)))
        End Function

        Private Function ThumbLeft() As Integer
            Return Math.Max(0, Math.Min(Width - ThumbWidth, FillWidth() - ThumbWidth \ 2))
        End Function

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics

            Dim bg As Image = TimeLineResources.GetBackground()
            If bg IsNot Nothing Then Skin.DrawStretch(g, bg, ClientRectangle, horizontal:=True)

            Dim fw As Integer = FillWidth()
            If fw > 0 Then
                Dim fill As Image = TimeLineResources.GetFill(_color)
                If fill IsNot Nothing Then
                    Dim oldClip As Region = g.Clip
                    g.SetClip(New Rectangle(0, 0, fw, Height))
                    Skin.DrawStretch(g, fill, ClientRectangle, horizontal:=True)
                    g.Clip = oldClip
                End If
            End If

            If _style = TimeLineStyleConstants.Adjustable Then
                Using b As New SolidBrush(System.Drawing.Color.FromArgb(64, 64, 64))
                    g.FillRectangle(b, ThumbLeft(), 0, ThumbWidth, Height)
                End Using
            End If
        End Sub

        Private Sub SeekToX(ByVal x As Integer)
            Dim clamped As Integer = Math.Max(0, Math.Min(Width, x))
            CurrentPosition = CInt(_duration * (clamped / CDbl(Math.Max(1, Width))))
            RaiseEvent SlideChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If _style <> TimeLineStyleConstants.Adjustable OrElse e.Button <> MouseButtons.Left Then Return
            _dragging = True
            SeekToX(e.X)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If Not _dragging Then Return
            SeekToX(e.X)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

    End Class

End Namespace
