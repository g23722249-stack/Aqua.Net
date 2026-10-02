Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Owner-drawn port of the VB6 Aqua.Label UserControl (Control\Label.ctl): a themed
' surface behind an optional icon + text, with three click behaviours (Conduct):
' Normal (always shows the accent-coloured surface, not clickable), Header (click
' toggles Selected and stays highlighted while selected) and Button (click flashes
' the accent colour, then reverts). Style picks a rounded-pill or square surface;
' the rounded corners are now cut with RegionUtil (procedural) instead of the VB6
' mask bitmaps, which fed a CreateFromPicture/SetWindowRgn call for the same effect.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class Label
        Inherits Control

        Private Const ClickWaitMs As Integer = 80  ' gc_lngButtonClickWaitInterval
        Private Const IconTextGap As Integer = 6

        Private _icon As Image
        Private _style As LabelStyle = LabelStyle.Rectangle
        Private _conduct As LabelConduct = LabelConduct.Header
        Private _iconAlignment As LabelIconAlignment = LabelIconAlignment.MiddleLeft
        Private _color As ColorConstants = ColorConstants.Blue
        Private _selected As Boolean = True
        Private _leftOfIconPixels As Integer = 0
        Private _leftOfTextPixels As Integer = 0
        Private _hue As Integer = 0
        Private _saturation As Integer = 0
        Private _luminosity As Integer = 0

        Private _iconRect As Rectangle = Rectangle.Empty
        Private _clickFlash As Boolean = False
        Private _pendingRealClick As Boolean = False
        Private ReadOnly _flashTimer As New Timer()

        Private _tintedCache As Bitmap
        Private _tintedCacheKey As String = Nothing

        Public Event IconClick(sender As Object, e As EventArgs)
        Public Shadows Event StyleChanged(sender As Object, e As EventArgs)
        Public Event ModeChanged(sender As Object, e As EventArgs)   ' Conduct changed (VB6 name)
        Public Event IconAlignmentChanged(sender As Object, e As EventArgs)
        Public Event IconChanged(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event LeftOfIconPixelsChanged(sender As Object, e As EventArgs)
        Public Event LeftOfTextPixelsChanged(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            Text = "Label"
            _flashTimer.Interval = ClickWaitMs
            AddHandler _flashTimer.Tick, AddressOf OnFlashTick
            UpdateRegion()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ' Parent is reliably non-Nothing by now (unlike in the constructor, or a Resize that may
            ' never fire again if the control keeps its initial/default size) -- re-run so the
            ' corner-repaint request in UpdateRegion actually reaches a real parent at least once.
            UpdateRegion()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(75, 23)
            End Get
        End Property

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("外觀")>
        Public Property Icon As Image
            Get
                Return _icon
            End Get
            Set(value As Image)
                If _icon Is value Then Return
                _icon = value
                Invalidate()
                RaiseEvent IconChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(LabelStyle.Rectangle)>
        Public Property Style As LabelStyle
            Get
                Return _style
            End Get
            Set(value As LabelStyle)
                If _style = value Then Return
                _style = value
                InvalidateTintCache()
                UpdateRegion()
                Invalidate()
                RaiseEvent StyleChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(LabelConduct.Header)>
        Public Property Conduct As LabelConduct
            Get
                Return _conduct
            End Get
            Set(value As LabelConduct)
                If _conduct = value Then Return
                _conduct = value
                Invalidate()
                RaiseEvent ModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(LabelIconAlignment.MiddleLeft)>
        Public Property IconAlignment As LabelIconAlignment
            Get
                Return _iconAlignment
            End Get
            Set(value As LabelIconAlignment)
                If _iconAlignment = value Then Return
                _iconAlignment = value
                Invalidate()
                RaiseEvent IconAlignmentChanged(Me, EventArgs.Empty)
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
                InvalidateTintCache()
                Invalidate()
                RaiseEvent ColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Only visually meaningful for Conduct = Header, matching the VB6 setter.</summary>
        <Category("行為")>
        <DefaultValue(True)>
        Public Property Selected As Boolean
            Get
                Return _selected
            End Get
            Set(value As Boolean)
                If _selected = value Then Return
                _selected = value
                If _conduct = LabelConduct.Header Then
                    Invalidate()
                    RaiseEvent SelectedChanged(Me, EventArgs.Empty)
                End If
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property LeftOfIconPixels As Integer
            Get
                Return _leftOfIconPixels
            End Get
            Set(value As Integer)
                If value < 0 Then value = 0
                If _leftOfIconPixels = value Then Return
                _leftOfIconPixels = value
                Invalidate()
                RaiseEvent LeftOfIconPixelsChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property LeftOfTextPixels As Integer
            Get
                Return _leftOfTextPixels
            End Get
            Set(value As Integer)
                If value < 0 Then value = 0
                If _leftOfTextPixels = value Then Return
                _leftOfTextPixels = value
                Invalidate()
                RaiseEvent LeftOfTextPixelsChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Fine-tunes the accent-coloured surface only (degrees). 0 = untouched, matching VB6's no-op default.</summary>
        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfHue As Integer
            Get
                Return _hue
            End Get
            Set(value As Integer)
                If _hue = value Then Return
                _hue = value
                InvalidateTintCache()
                Invalidate()
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfSaturation As Integer
            Get
                Return _saturation
            End Get
            Set(value As Integer)
                If _saturation = value Then Return
                _saturation = value
                InvalidateTintCache()
                Invalidate()
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(0)>
        Public Property ColorOfLuminosity As Integer
            Get
                Return _luminosity
            End Get
            Set(value As Integer)
                If _luminosity = value Then Return
                _luminosity = value
                InvalidateTintCache()
                Invalidate()
            End Set
        End Property

        '=====================================================================
        ' Region (port of RegionUserControl, using the mask bitmaps' shape procedurally)
        '=====================================================================
        Private Sub UpdateRegion()
            Dim newRegion As Region = Nothing
            If _style = LabelStyle.Obtuseness AndAlso Width > 0 AndAlso Height > 0 Then
                newRegion = RegionUtil.CreateObtusenessRegion(ObtusenessMode.All, Width, Height)
            End If
            Dim old As Region = Me.Region
            Me.Region = newRegion
            If old IsNot Nothing Then old.Dispose()
            ' SetWindowRgn on a child doesn't itself make the parent repaint the corner pixels the
            ' new (smaller) region just exposed -- without this they can be left showing whatever was
            ' there before (stale/garbage), instead of the parent's real current background.
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            Invalidate()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        '=====================================================================
        ' Painting (ports of DrawUserControl / DrawBackground / SetUserControlPosition)
        '=====================================================================
        Private Function CurrentState() As LabelState
            If Not Enabled Then Return LabelState.Disable
            Select Case _conduct
                Case LabelConduct.Normal
                    Return LabelState.Click   ' always shows the accent surface
                Case LabelConduct.Header
                    If _clickFlash OrElse _selected Then Return LabelState.Click
                    Return LabelState.Normal
                Case Else ' Button
                    If _clickFlash Then Return LabelState.Click
                    Return LabelState.Normal
            End Select
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim state As LabelState = CurrentState()

            Dim surface As Image = LabelResources.GetSurface(state, _color, _style)
            If state = LabelState.Click AndAlso (_hue <> 0 OrElse _saturation <> 0 OrElse _luminosity <> 0) Then
                surface = TintedSurface(surface)
            End If
            If surface IsNot Nothing Then Skin.DrawStretch(g, surface, ClientRectangle, horizontal:=True)

            DrawContent(g)
        End Sub

        Private Function TintedSurface(ByVal baseImage As Image) As Image
            If baseImage Is Nothing Then Return Nothing
            Dim key As String = $"{_color}|{_style}|{_hue}|{_saturation}|{_luminosity}"
            If _tintedCacheKey = key AndAlso _tintedCache IsNot Nothing Then Return _tintedCache
            _tintedCache?.Dispose()
            _tintedCache = ColorUtil.ApplyHslShift(baseImage, _hue, _saturation, _luminosity)
            _tintedCacheKey = key
            Return _tintedCache
        End Function

        Private Sub InvalidateTintCache()
            _tintedCache?.Dispose()
            _tintedCache = Nothing
            _tintedCacheKey = Nothing
        End Sub

        Private Sub DrawContent(g As Graphics)
            Dim hasIcon As Boolean = _icon IsNot Nothing
            Dim text As String = Me.Text
            Dim hasText As Boolean = Not String.IsNullOrEmpty(text)
            Dim textSize As Size = If(hasText,
                TextRenderer.MeasureText(text, Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                         TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine),
                Size.Empty)
            Dim iconW As Integer = If(hasIcon, _icon.Width, 0)
            Dim iconH As Integer = If(hasIcon, _icon.Height, 0)

            Dim textTop As Integer = (Height - textSize.Height) \ 2
            Dim iconTop As Integer = (Height - iconH) \ 2
            Dim textLeft As Integer
            Dim iconLeft As Integer

            If Not hasIcon Then
                _iconRect = Rectangle.Empty
                textLeft = If(_leftOfTextPixels > 0, _leftOfTextPixels, (Width - textSize.Width) \ 2)
            Else
                If _leftOfIconPixels > 0 Then
                    iconLeft = _leftOfIconPixels
                    textLeft = If(_leftOfTextPixels > 0, _leftOfTextPixels, (Width - textSize.Width) \ 2)
                Else
                    Dim contentW As Integer = If(hasText, iconW + IconTextGap + textSize.Width, iconW)
                    Dim left0 As Integer = (Width - contentW) \ 2
                    Select Case _iconAlignment
                        Case LabelIconAlignment.MiddleRight
                            textLeft = left0
                            iconLeft = left0 + textSize.Width + IconTextGap
                        Case Else ' MiddleLeft
                            iconLeft = left0
                            textLeft = left0 + iconW + IconTextGap
                    End Select
                End If
                _iconRect = New Rectangle(iconLeft, iconTop, iconW, iconH)
                g.DrawImage(_icon, _iconRect)
            End If

            If hasText Then
                Dim fg As Color = If(Enabled, ForeColor, GridConst.DisableForeColor)
                TextRenderer.DrawText(g, text, Font, New Point(textLeft, textTop), fg,
                                      TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            End If
        End Sub

        '=====================================================================
        ' Interaction (ports of UserControl_Click / imgIcon_Click / UserControl_KeyPress)
        '=====================================================================
        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button <> MouseButtons.Left Then Return
            If Not _iconRect.IsEmpty AndAlso _iconRect.Contains(e.Location) Then
                RaiseEvent IconClick(Me, EventArgs.Empty)
            End If
            StartFlash(isRealClick:=True)
        End Sub

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If Enabled AndAlso (e.KeyCode = Keys.Enter OrElse e.KeyCode = Keys.Space) Then
                StartFlash(isRealClick:=False)
            End If
        End Sub

        ''' <summary>
        ''' Starts the accent-colour flash. VB6 blocked for 80ms (Wait) before settling and raising
        ''' Click; here the settle + Click happen on the timer tick instead, so the UI stays responsive.
        ''' </summary>
        Private Sub StartFlash(ByVal isRealClick As Boolean)
            _clickFlash = True
            _pendingRealClick = isRealClick
            Invalidate()
            _flashTimer.Stop()
            _flashTimer.Start()
        End Sub

        Private Sub OnFlashTick(sender As Object, e As EventArgs)
            _flashTimer.Stop()
            _clickFlash = False
            If _pendingRealClick Then
                _pendingRealClick = False
                If _conduct = LabelConduct.Header Then
                    Selected = Not Selected   ' setter Invalidates + raises SelectedChanged
                Else
                    Invalidate()
                End If
                MyBase.OnClick(EventArgs.Empty)
            Else
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _flashTimer.Stop()
                _flashTimer.Dispose()
                _tintedCache?.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
