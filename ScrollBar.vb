Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.ScrollBar UserControl (Control\ScrollBar.ctl).
' The background groove, track, draggable thumb and the always-present end spinner are
' all painted directly in OnPaint from the original extracted bitmaps (three-slice
' stretched so the rounded end caps stay crisp). Drawing the thumb/spinner inline --
' rather than as transparent child controls -- avoids the child-over-owner-draw
' compositing artefacts that washed out the thumb. The value maths is ported verbatim.
Namespace Global.Aqua

    <DefaultEvent("Scroll")>
    Public Class ScrollBar
        Inherits Control

        Private Const Thickness As Integer = 18
        Private Const MinThumb As Integer = 18
        Private Const SizeThreshold As Integer = 24

        Private Const DirUp As Integer = 0
        Private Const DirDown As Integer = 1
        Private Const DirLeft As Integer = 2
        Private Const DirRight As Integer = 3

        Private _min As Integer = 1
        Private _max As Integer = 1
        Private _value As Integer = 1
        Private _largeChange As Integer = 1
        Private _orientation As OrientationMode = OrientationMode.Vertical
        Private _color As ColorConstants = ColorConstants.Blue
        Private _active As Boolean = True

        ' interaction state
        Private _dragging As Boolean = False
        Private _dragStartMain As Integer
        Private _dragStartThumbPos As Integer
        Private _thumbHover As Boolean = False
        Private _decPressed As Boolean = False
        Private _incPressed As Boolean = False

        Public Event ValueChanged(sender As Object, e As EventArgs)
        Public Event Scroll(sender As Object, e As EventArgs)
        Public Event IncrementClick(sender As Object, e As EventArgs)
        Public Event DecrementClick(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event OrientationChanged(sender As Object, e As EventArgs)
        Public Event Active(sender As Object, e As EventArgs)
        Public Event Deactivate(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            MyBase.Enabled = False   ' VB6 InitProperties: scrollbars start disabled
            TabStop = False
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <DefaultValue(1)> Public Property Minimum As Integer
            Get
                Return _min
            End Get
            Set(value As Integer)
                If _min = value Then Return
                _min = value : ClampValue() : Invalidate()
            End Set
        End Property

        <DefaultValue(1)> Public Property Maximum As Integer
            Get
                Return _max
            End Get
            Set(value As Integer)
                If _max = value Then Return
                _max = value : ClampValue() : Invalidate()
            End Set
        End Property

        <DefaultValue(1)> Public Property Value As Integer
            Get
                Return _value
            End Get
            Set(v As Integer)
                Dim nv As Integer = v
                If nv < _min Then nv = _min
                If nv > _max Then nv = _max
                If _value = nv Then Return
                _value = nv
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
                RaiseEvent Scroll(Me, EventArgs.Empty)
            End Set
        End Property

        <DefaultValue(1)> Public Property LargeChange As Integer
            Get
                Return _largeChange
            End Get
            Set(value As Integer)
                _largeChange = value
            End Set
        End Property

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

        <Browsable(False)> Public Property ActiveControl As Boolean
            Get
                Return _active
            End Get
            Set(value As Boolean)
                _active = value
                Invalidate()
                If value Then RaiseEvent Active(Me, EventArgs.Empty) Else RaiseEvent Deactivate(Me, EventArgs.Empty)
            End Set
        End Property

        Private Sub ClampValue()
            If _value < _min Then _value = _min
            If _value > _max Then _value = _max
        End Sub

        '=====================================================================
        ' Geometry (ports of SetScrollBarSize / SetScrollBarLocation / GetScrollBarValue)
        '=====================================================================
        Private ReadOnly Property IsHorizontal As Boolean
            Get
                Return _orientation = OrientationMode.Horizontal
            End Get
        End Property

        Private Function SpinnerSize() As Integer
            Return CInt(Thickness * 1.8)
        End Function

        Private Function IsUpDownMode() As Boolean
            Dim mainLen As Integer = If(IsHorizontal, Width, Height)
            Return mainLen < SpinnerSize() + SizeThreshold
        End Function

        Private Function SpinnerRect() As Rectangle
            Dim s As Integer = SpinnerSize()
            If IsUpDownMode() Then Return New Rectangle(0, 0, Width, Height)
            If IsHorizontal Then Return New Rectangle(Width - s, 0, s, Height)
            Return New Rectangle(0, Height - s, Width, s)
        End Function

        Private Function TrackRect() As Rectangle
            If IsUpDownMode() Then Return Rectangle.Empty
            Dim s As Integer = SpinnerSize()
            If IsHorizontal Then Return New Rectangle(0, 0, Width - s, Height)
            Return New Rectangle(0, 0, Width, Height - s)
        End Function

        Private Function TrackLength() As Integer
            Dim tr As Rectangle = TrackRect()
            Return If(IsHorizontal, tr.Width, tr.Height)
        End Function

        Private Function ThumbLength() As Integer
            Dim L As Integer = TrackLength()
            If L <= 0 Then Return 0
            Dim total As Integer
            If _max = _min Then Return L
            total = If(_min = 0, _max + 1, _max - _min)
            Dim interval As Double = If(total = 1, L * 80.0 / 84.0, L * (80.0 - total) / 84.0)
            If interval <= SizeThreshold Then interval = MinThumb
            Dim len As Integer = CInt(interval)
            If len > L Then len = L
            Return len
        End Function

        Private Function ReallyInterval() As Double
            Dim L As Integer = TrackLength()
            Dim tl As Integer = ThumbLength()
            If (L - tl) > 0 AndAlso (_max - _min) > 0 Then Return (L - tl) / CDbl(_max - _min)
            Return 0
        End Function

        Private Function ThumbPos() As Integer
            Dim L As Integer = TrackLength()
            Dim tl As Integer = ThumbLength()
            If _value = _min Then Return 0
            If _value = _max Then Return L - tl
            Return CInt(ReallyInterval() * (_value - _min))
        End Function

        Private Function ThumbRect() As Rectangle
            Dim tr As Rectangle = TrackRect()
            Dim tl As Integer = ThumbLength()
            Dim pos As Integer = ThumbPos()
            If IsHorizontal Then Return New Rectangle(tr.X + pos, tr.Y, tl, tr.Height)
            Return New Rectangle(tr.X, tr.Y + pos, tr.Width, tl)
        End Function

        Private Function ValueFromPos(ByVal pos As Integer) As Integer
            Dim L As Integer = TrackLength()
            Dim tl As Integer = ThumbLength()
            Dim ri As Double = ReallyInterval()
            If pos <= 0 Then Return _min
            If pos + tl >= L Then Return _max
            If ri <= 0 Then Return _min
            If pos <= ri / 2 Then Return _min
            If pos + tl >= L - ri / 2 Then Return _max
            If pos / ri <= _min Then Return _min + 1
            If pos / ri >= _max Then Return _max
            Dim v As Integer
            If (pos Mod CInt(Math.Max(1, ri))) > ri / 2 Then
                v = CInt(Math.Floor(pos / ri)) + 1 + _min
            Else
                v = CInt(Math.Floor(pos / ri)) + _min
            End If
            If v > _max Then v = _max
            Return v
        End Function

        Private Function ThumbState() As ScrollState
            If Not _active Then Return ScrollState.Deactivate
            If _dragging Then Return ScrollState.Click
            If _thumbHover Then Return ScrollState.Hover
            Return ScrollState.ExitFocus
        End Function

        Private Function ThumbSizeClass() As ScrollBarSize
            Return If(ThumbLength() > SizeThreshold, ScrollBarSize.Large, ScrollBarSize.Small)
        End Function

        '=====================================================================
        ' Painting (all inline: background groove, track, thumb, spinner)
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics

            ' whole-control background groove
            Dim bg As Image = ScrollBarResources.GetBackground(_orientation)
            If bg IsNot Nothing Then Skin.DrawStretch(g, bg, ClientRectangle, IsHorizontal)

            If Not Enabled Then Return   ' VB6 hides thumb/track/spinner when disabled

            If Not IsUpDownMode() Then
                ' track
                Dim tr As Rectangle = TrackRect()
                Dim track As Image = ScrollBarResources.GetTrack(_orientation)
                If track IsNot Nothing Then Skin.DrawStretch(g, track, tr, IsHorizontal)

                ' thumb (three-slice so the rounded caps stay crisp on a long thumb)
                Dim thumb As Image = ScrollBarResources.GetSurface(_orientation, ThumbState(), _color, ThumbSizeClass())
                If thumb IsNot Nothing Then Skin.DrawStretch(g, thumb, ThumbRect(), IsHorizontal)
            End If

            DrawSpinner(g)
        End Sub

        Private Sub DrawSpinner(g As Graphics)
            Dim sp As Rectangle = SpinnerRect()
            Dim decDir As Integer = If(IsHorizontal, DirLeft, DirUp)
            Dim incDir As Integer = If(IsHorizontal, DirRight, DirDown)
            Dim decRect As Rectangle, incRect As Rectangle

            If IsHorizontal Then
                Dim halfW As Integer = sp.Width \ 2
                decRect = New Rectangle(sp.X, sp.Y, halfW, sp.Height)
                incRect = New Rectangle(sp.X + halfW, sp.Y, sp.Width - halfW, sp.Height)
            Else
                Dim halfH As Integer = sp.Height \ 2
                decRect = New Rectangle(sp.X, sp.Y, sp.Width, halfH)
                incRect = New Rectangle(sp.X, sp.Y + halfH, sp.Width, sp.Height - halfH)
            End If

            DrawSpinButton(g, decRect, decDir, _decPressed)
            DrawSpinButton(g, incRect, incDir, _incPressed)
        End Sub

        Private Sub DrawSpinButton(g As Graphics, ByVal rect As Rectangle, ByVal dir As Integer, ByVal pressed As Boolean)
            Dim state As ScrollState
            If pressed Then
                state = ScrollState.Click
            ElseIf Not _active Then
                state = ScrollState.Deactivate
            Else
                state = ScrollState.ExitFocus
            End If
            Dim surf As Image = ScrollBarResources.GetSpinSurface(state, _color, dir)
            If surf IsNot Nothing Then g.DrawImage(surf, rect)

            Dim arrow As Image = ScrollBarResources.GetSpinArrow(dir, Enabled)
            If arrow IsNot Nothing Then
                Dim ax As Integer = rect.X + (rect.Width - arrow.Width) \ 2
                Dim ay As Integer = rect.Y + (rect.Height - arrow.Height) \ 2
                g.DrawImage(arrow, New Rectangle(ax, ay, arrow.Width, arrow.Height))
            End If
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If Not Enabled OrElse e.Button <> MouseButtons.Left Then Return

            Dim sp As Rectangle = SpinnerRect()
            If sp.Contains(e.Location) Then
                HandleSpinnerDown(e.Location, sp)
                Return
            End If

            If IsUpDownMode() Then Return
            Dim tRect As Rectangle = ThumbRect()
            If tRect.Contains(e.Location) Then
                _dragging = True
                _dragStartMain = If(IsHorizontal, e.X, e.Y)
                _dragStartThumbPos = ThumbPos()
                Invalidate()
            Else
                Dim mainPos As Integer = If(IsHorizontal, e.X, e.Y)
                Dim thumbMain As Integer = If(IsHorizontal, tRect.X, tRect.Y)
                If mainPos < thumbMain Then
                    Value = Math.Max(_min, _value - _largeChange)
                Else
                    Value = Math.Min(_max, _value + _largeChange)
                End If
            End If
        End Sub

        Private Sub HandleSpinnerDown(ByVal p As Point, ByVal sp As Rectangle)
            Dim decrement As Boolean
            If IsHorizontal Then
                decrement = p.X < sp.X + sp.Width \ 2
            Else
                decrement = p.Y < sp.Y + sp.Height \ 2
            End If
            If decrement Then
                _decPressed = True : Invalidate()
                If _value > _min Then
                    Value = _value - 1
                    RaiseEvent DecrementClick(Me, EventArgs.Empty)
                End If
            Else
                _incPressed = True : Invalidate()
                If _value < _max Then
                    Value = _value + 1
                    RaiseEvent IncrementClick(Me, EventArgs.Empty)
                End If
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If Not Enabled Then Return

            If _dragging Then
                Dim mainNow As Integer = If(IsHorizontal, e.X, e.Y)
                Dim newPos As Integer = _dragStartThumbPos + (mainNow - _dragStartMain)
                Dim L As Integer = TrackLength()
                Dim tl As Integer = ThumbLength()
                If newPos < 0 Then newPos = 0
                If newPos > L - tl Then newPos = L - tl
                Dim nv As Integer = ValueFromPos(newPos)
                If nv <> _value Then Value = nv
                Return
            End If

            Dim hov As Boolean = (Not IsUpDownMode()) AndAlso ThumbRect().Contains(e.Location)
            If hov <> _thumbHover Then
                _thumbHover = hov
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
            _decPressed = False
            _incPressed = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _thumbHover Then
                _thumbHover = False
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

    End Class

End Namespace
