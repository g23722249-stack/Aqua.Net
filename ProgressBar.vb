Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.ProgressBar UserControl (Control\ProgressBar.ctl): a themed determinate
' progress bar, horizontal or vertical. Owner-drawn Control (no children) -- VB6's picProgress
' child PictureBox was just a clipped rectangle showing the "filled" portion, which g.DrawImage
' with a sized destination rectangle does directly.
Namespace Global.Aqua

    <DefaultEvent("ValueChanged")>
    Public Class ProgressBar
        Inherits Control

        Private _min As Integer = 1
        Private _max As Integer = 10
        Private _value As Integer = 0
        Private _color As ColorConstants = ColorConstants.Blue
        Private _orientation As OrientationMode = OrientationMode.Horizontal

        Public Event Complete(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event OrientationChanged(sender As Object, e As EventArgs)
        Public Event ValueChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            TabStop = False
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(150, 18)
            End Get
        End Property

        <Category("行為")>
        <DefaultValue(1)>
        Public Property Minimum As Integer
            Get
                Return _min
            End Get
            Set(value As Integer)
                If _min = value Then Return
                _min = value
                Invalidate()
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(10)>
        Public Property Maximum As Integer
            Get
                Return _max
            End Get
            Set(value As Integer)
                If value <= 0 OrElse value < _min Then Throw New ArgumentOutOfRangeException(NameOf(value))
                If _max = value Then Return
                _max = value
                If _value > _max Then _value = _max
                Invalidate()
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(0)>
        Public Property Value As Integer
            Get
                Return _value
            End Get
            Set(v As Integer)
                Dim nv As Integer = Math.Min(v, _max)
                If _value = nv Then Return
                _value = nv
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
                If _value = _max Then RaiseEvent Complete(Me, EventArgs.Empty)
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
        <DefaultValue(OrientationMode.Horizontal)>
        Public Property Orientation As OrientationMode
            Get
                Return _orientation
            End Get
            Set(value As OrientationMode)
                If _orientation = value Then Return
                _orientation = value
                Invalidate()
                RaiseEvent OrientationChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim horizontal As Boolean = (_orientation = OrientationMode.Horizontal)

            Dim bg As Image = ProgressBarResources.GetBackground(_orientation)
            If bg IsNot Nothing Then Skin.DrawStretch(g, bg, ClientRectangle, horizontal)

            Dim rect As Rectangle = FillRect()
            If rect.Width > 0 AndAlso rect.Height > 0 Then
                Dim fill As Image = ProgressBarResources.GetFill(_color, _orientation)
                If fill IsNot Nothing Then
                    ' clip to the filled portion only, then stretch the fill art across the FULL
                    ' control rect within that clip -- matches VB6 moving/resizing a child PictureBox
                    ' over the same background rather than cropping the source image itself.
                    Dim oldClip As Region = g.Clip
                    g.SetClip(rect)
                    Skin.DrawStretch(g, fill, ClientRectangle, horizontal)
                    g.Clip = oldClip
                End If
            End If
        End Sub

        Private Function FillRect() As Rectangle
            If _value < _min Then Return Rectangle.Empty
            If _value >= _max Then Return New Rectangle(0, 0, Width, Height)
            Dim span As Integer = _max - _min
            If span <= 0 Then Return Rectangle.Empty
            If _orientation = OrientationMode.Horizontal Then
                Dim w As Integer = CInt(Width * (_value / CDbl(span)))
                Return New Rectangle(0, 0, w, Height)
            Else
                Dim h As Integer = CInt(Height * (_value / CDbl(span)))
                Return New Rectangle(0, Height - h, Width, h)
            End If
        End Function

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

    End Class

End Namespace
