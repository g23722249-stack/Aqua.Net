Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Port of the VB6 Aqua.Loading UserControl (Control\Loading.ctl): a horizontally scrolling
' striped "busy" bar. VB6 had to hand-build two pre-tiled bitmaps (PaintFrontHorizontalStretchPicture
' / PaintBackHorizontalStretchPicture) and blit slices of them every tick because it had no tiled-
' brush primitive. GDI+ TextureBrush with WrapMode.Tile plus a TranslateTransform reproduces the
' same seamless infinite scroll directly, so that whole 3-way-splitting scheme isn't needed here.
Namespace Global.Aqua

    <DefaultEvent("PlayChanged")>
    Public Class Loading
        Inherits Control

        Private _color As ColorConstants = ColorConstants.Blue
        Private _playing As Boolean = False
        Private _offset As Integer = 0
        Private ReadOnly _timer As New Timer()

        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event PlayChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            TabStop = False
            _timer.Interval = 20
            AddHandler _timer.Tick, AddressOf OnTick
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(180, FixedHeight())
            End Get
        End Property

        ''' <summary>Natural height of the striped bar art (VB6: ResizeUserControl forced
        ''' UserControl.Height back to m_lngPictureHeight on every resize -- Height was never really
        ''' adjustable, only Width was. Ported as a hard clamp in SetBoundsCore below.</summary>
        Private Function FixedHeight() As Integer
            Dim bar As Image = LoadingResources.GetBar(_color)
            Return If(bar IsNot Nothing, bar.Height, 16)
        End Function

        ''' <summary>Locks Height to the bar art's natural height, exactly like VB6's
        ''' ResizeUserControl. Without this, a taller control makes TextureBrush's WrapMode.Tile
        ''' repeat the striped pattern vertically as well as horizontally -- the "重複顯示" bug.</summary>
        Protected Overrides Sub SetBoundsCore(x As Integer, y As Integer, width As Integer, height As Integer, specified As BoundsSpecified)
            MyBase.SetBoundsCore(x, y, width, FixedHeight(), specified)
        End Sub

        <Category("外觀")>
        <DefaultValue(ColorConstants.Blue)>
        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                Height = FixedHeight()   ' different colour art could in principle have a different bar height
                Invalidate()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Starts/stops the scroll animation (VB6: Play).</summary>
        <Browsable(False)>
        Public Property Play As Boolean
            Get
                Return _playing
            End Get
            Set(value As Boolean)
                If _playing = value Then Return
                _playing = value
                If _playing Then _timer.Start() Else _timer.Stop()
                RaiseEvent PlayChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim bar As Image = LoadingResources.GetBar(_color)
            Dim wrapWidth As Integer = If(bar IsNot Nothing, bar.Width, 1)
            _offset = (_offset + 1) Mod Math.Max(1, wrapWidth)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim bar As Image = LoadingResources.GetBar(_color)
            If bar IsNot Nothing Then
                Using brush As New TextureBrush(bar, WrapMode.Tile)
                    ' Height is locked to bar.Height (SetBoundsCore above), so only horizontal
                    ' scrolling by the animation offset is needed here.
                    brush.TranslateTransform(-_offset, 0)
                    g.FillRectangle(brush, ClientRectangle)
                End Using
            End If

            Using p As New Pen(System.Drawing.Color.FromArgb(102, 102, 102))
                g.DrawLine(p, 0, 0, 0, Height - 1)
                g.DrawLine(p, Width - 1, 0, Width - 1, Height - 1)
            End Using
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _timer.Stop()
                _timer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
