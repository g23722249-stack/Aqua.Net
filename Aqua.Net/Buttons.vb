Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Drawing.Design
Imports System.Windows.Forms

' Port of the VB6 Aqua.Buttons UserControl (Control\Buttons.ctl): a segmented button bar --
' one control hosting several buttons (AdditionButton), each an independently selectable
' left/middle/right (or single full-size) skin segment with optional icon+text.
'
' VB6 built this from a real PictureBox control array (one child per button) plus per-child
' Label/Image controls. This port instead owner-draws the whole bar directly (no children),
' matching the pattern already used for ProgressBar/TimeLine/Slider/UpDown -- and sidesteps the
' WinForms Designer hang that adding children dynamically in a Control constructor can cause.
' Each button's on-screen rectangle is tracked in a ButtonItem and hit-tested by X position in
' OnMouseDown instead of routing through per-child Click handlers.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class Buttons
        Inherits Control

        ''' <summary>One segment's data (VB6: udfButtons {Text, Icon}). Public so the Items
        ''' collection below can be edited in the VS Properties panel via the standard
        ''' CollectionEditor, the same way TabControl.TabPages is edited.</summary>
        <TypeConverter(GetType(TextIconItemConverter))>
        Public Class ButtonItem
            Public Sub New()
            End Sub

            Public Sub New(ByVal text As String, ByVal icon As Image)
                Me.Text = If(text, "")
                Me.Icon = icon
            End Sub

            Public Property Text As String = ""
            Public Property Icon As Image

            ''' <summary>Segment's on-screen rectangle, recomputed by DoLayout. Friend (not
            ''' Public), so it never shows up in the CollectionEditor's per-item property grid.</summary>
            Friend Property Bounds As Rectangle

            Public Overrides Function ToString() As String
                Return If(String.IsNullOrEmpty(Text), "ButtonItem", Text)
            End Function
        End Class

        ''' <summary>Owns Buttons' segment list; every mutation (Add/Remove/Set/Clear -- whether
        ''' from code or the design-time CollectionEditor dialog) re-runs layout and clamps
        ''' SelectedIndex the same way VB6's SetButtonProperty did.</summary>
        Public Class ButtonItemCollection
            Inherits Collection(Of ButtonItem)

            Private ReadOnly _owner As Buttons

            Friend Sub New(ByVal owner As Buttons)
                _owner = owner
            End Sub

            Protected Overrides Sub InsertItem(ByVal index As Integer, ByVal item As ButtonItem)
                MyBase.InsertItem(index, If(item, New ButtonItem()))
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub RemoveItem(ByVal index As Integer)
                MyBase.RemoveItem(index)
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub SetItem(ByVal index As Integer, ByVal item As ButtonItem)
                MyBase.SetItem(index, If(item, New ButtonItem()))
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub ClearItems()
                MyBase.ClearItems()
                _owner.OnItemsChanged()
            End Sub
        End Class

        Private Const Gap As Integer = 1              ' 1px seam between segments (VB6: Screen.TwipsPerPixelX)
        Private Const IconTextGapV As Integer = 2      ' VB6: 2 * Screen.TwipsPerPixelY
        Private Const IconTextGapH As Integer = 4      ' VB6: 4 * Screen.TwipsPerPixelX

        Private ReadOnly _items As ButtonItemCollection
        Private _selIndex As Integer = -1
        Private _color As ColorConstants = ColorConstants.Blue
        Private _iconAlignment As ButtonsIconAlignment = ButtonsIconAlignment.Top

        ' VB6 quirk preserved: Buttons.ctl declares m_lngHoverInterval and serializes it via
        ' PropBag, but (unlike Button.ctl/CheckBox.ctl/Panel.ctl and friends) never actually
        ' exposes a Public Property Get/Let HoverInterval -- so it can never be set above 0 from
        ' outside the control, and MouseHover below can in practice never fire. Kept private here
        ' with no public accessor for the same reason, rather than "fixing" a control nobody could
        ' ever have configured differently.
        Private ReadOnly _hoverInterval As Integer = 0
        Private _mouseHoverFired As Boolean = False
        Private ReadOnly _hoverTimer As New Timer()
        Private ReadOnly _pressTimer As New Timer()
        Private _pressButton As Integer
        Private _pressShift As Integer

        Private _soundClick, _soundMouseEnter, _soundMouseHover, _soundMouseLeave, _soundEnterFocus, _soundExitFocus As String

        ''' <summary>Raised when a segment is clicked (VB6: Click(index)). Shadows the base
        ''' parameterless Click event, which still fires too (via MyBase.OnClick).</summary>
        Public Shadows Event Click(sender As Object, index As Integer)
        Public Event MousePress(button As Integer, shift As Integer)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event IconAlignmentChanged(sender As Object, e As EventArgs)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)
        ''' <summary>Shadows Control's built-in MouseHover: VB6 gated this on a configurable
        ''' HoverInterval timer rather than the OS's fixed hover delay.</summary>
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.Selectable, True)
            _items = New ButtonItemCollection(Me)
            _pressTimer.Interval = 1000
            AddHandler _pressTimer.Tick, AddressOf OnPressTick
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick
            UpdateRegion()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(200, 27)
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
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
        <DefaultValue(ButtonsIconAlignment.Top)>
        Public Property IconAlignment As ButtonsIconAlignment
            Get
                Return _iconAlignment
            End Get
            Set(value As ButtonsIconAlignment)
                If _iconAlignment = value Then Return
                _iconAlignment = value
                Invalidate()
                RaiseEvent IconAlignmentChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>VB6 never guarded this setter with an equality check (the guard is commented
        ''' out in Buttons.ctl) -- it always redraws and always raises SelectedChanged, even when
        ''' set to the value it already had. Preserved as-is.</summary>
        <Category("行為")>
        <DefaultValue(-1)>
        Public Property SelectedIndex As Integer
            Get
                Return _selIndex
            End Get
            Set(value As Integer)
                _selIndex = value
                Invalidate()
                RaiseEvent SelectedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Design-time editable segment list -- click "..." in the Properties panel
        ''' (same CollectionEditor TabControl.TabPages uses) to add/remove/reorder buttons and
        ''' set each one's Text/Icon, instead of only via AdditionButton in code.</summary>
        <Category("行為")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        <Editor(GetType(CollectionEditor), GetType(UITypeEditor))>
        Public ReadOnly Property Items As ButtonItemCollection
            Get
                Return _items
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _items.Count
            End Get
        End Property

        Public Function GetText(ByVal index As Integer) As String
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            Return _items(index).Text
        End Function

        Public Function GetIcon(ByVal index As Integer) As Image
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            Return _items(index).Icon
        End Function

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
        ' Item management (ports of AdditionButton / Clear / SetButtonProperty).
        ' Items' InsertItem/RemoveItem/SetItem/ClearItems all funnel into OnItemsChanged below,
        ' so these three keep working exactly as before whether called from code or triggered by
        ' edits made in the Items CollectionEditor at design time.
        '=====================================================================
        Public Sub AdditionButton(ByVal text As String, ByVal icon As Image)
            _items.Add(New ButtonItem(text, icon))
        End Sub

        Public Sub Clear()
            _items.Clear()
        End Sub

        ''' <summary>VB6: re-clamps SelectedIndex and repaints after a batch of AdditionButton
        ''' calls (originally also poked the property-grid via PropertyChanged, which has no
        ''' meaningful WinForms equivalent here). Items' own mutators already do this, so this is
        ''' now just a manual "refresh" a caller can still invoke explicitly.</summary>
        Public Sub SetButtonProperty()
            OnItemsChanged()
        End Sub

        ''' <summary>Common tail of every Items mutation (VB6: the SelectedIndex-clamping half of
        ''' SetButtonProperty). Friend so ButtonItemCollection can call it.</summary>
        Friend Sub OnItemsChanged()
            If _selIndex > _items.Count - 1 Then _selIndex = -1
            DoLayout()
            Invalidate()
        End Sub

        '=====================================================================
        ' Layout (ports of SetUserControlSizeMixed / SetUserControlSizeTextMode)
        '=====================================================================
        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            DoLayout()
            Invalidate()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            DoLayout()
            Invalidate()
        End Sub

        Private Sub DoLayout()
            Dim n As Integer = _items.Count
            If n = 0 OrElse Width <= 0 OrElse Height <= 0 Then Return
            If n = 1 Then
                _items(0).Bounds = New Rectangle(0, 0, Width, Height)
                Return
            End If

            ' VB6 switches to proportional-by-text-width layout only when the WHOLE group has
            ' some text somewhere and NO icon anywhere at all (a global, not per-button, check) --
            ' any icon present anywhere falls back to equal division. Preserved as-is.
            Dim anyText As Boolean = False
            Dim anyIcon As Boolean = False
            For Each it In _items
                If it.Text.Trim().Length > 0 Then anyText = True
                If it.Icon IsNot Nothing Then anyIcon = True
            Next

            If anyText AndAlso Not anyIcon Then
                TextProportionalLayout()
            Else
                EqualLayout()
            End If

            ' Every segment before the last gets the same rounded/floored width, which can leave
            ' 1-2px of the control's own right edge uncovered by any segment's artwork -- the crisp
            ' border stroke baked into the right-cap art (see ButtonsResources/frmResButtons imgRight)
            ' never gets drawn there, so that edge looks faint against the parent background. Stretch
            ' the last segment to the control's true right edge so its border cap always lands there.
            Dim lastItem As ButtonItem = _items(n - 1)
            lastItem.Bounds = New Rectangle(lastItem.Bounds.X, lastItem.Bounds.Y, Width - lastItem.Bounds.X, lastItem.Bounds.Height)
        End Sub

        Private Sub EqualLayout()
            Dim n As Integer = _items.Count
            Dim avail As Integer = Width - (n - 1) * Gap
            Dim segW As Integer = CInt(Math.Round(avail / CDbl(n)))
            Dim x As Integer = 0
            For i = 0 To n - 1
                _items(i).Bounds = New Rectangle(x, 0, segW, Height)
                x += segW + Gap
            Next
        End Sub

        Private Sub TextProportionalLayout()
            Dim n As Integer = _items.Count
            Dim avail As Integer = Width - (n - 1) * Gap
            Dim measured(n - 1) As Integer
            Dim totalTextW As Integer = 0
            For i = 0 To n - 1
                measured(i) = TextRenderer.MeasureText(_items(i).Text, Font).Width
                totalTextW += measured(i)
            Next
            Dim scale As Double = If(avail > 0, totalTextW / CDbl(avail), 1.0)
            If scale <= 0 Then scale = 1.0
            Dim x As Integer = 0
            For i = 0 To n - 1
                Dim w As Integer = CInt(Math.Floor(measured(i) / scale)) + 1
                _items(i).Bounds = New Rectangle(x, 0, w, Height)
                x += w + Gap
            Next
        End Sub

        '=====================================================================
        ' Region (port of RegionUserControl: one stretched-mask region for the whole bar)
        '=====================================================================
        Private Sub UpdateRegion()
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim old As Region = Me.Region
            ' VB6's own mask art, stretched like the segments: a generic rounded rectangle didn't
            ' follow the art's corners and left white/grey specks of it outside the curve
            Me.Region = RegionUtil.CreateStretchedMaskRegion(ButtonsResources.GetMask(), Width, Height, horizontal:=True)
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        '=====================================================================
        ' Painting (ports of DrawUserControl / SetUserControlPosition)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim n As Integer = _items.Count
            If n = 0 Then Return

            For i = 0 To n - 1
                Dim item As ButtonItem = _items(i)
                Dim selected As Boolean = (i = _selIndex)
                Dim segment As ButtonsSegment
                If n <= 1 Then
                    segment = ButtonsSegment.Full
                ElseIf i = 0 Then
                    segment = ButtonsSegment.Left
                ElseIf i = n - 1 Then
                    segment = ButtonsSegment.Right
                Else
                    segment = ButtonsSegment.Middle
                End If

                Dim surface As Image = ButtonsResources.GetSurface(segment, _color, selected)
                If surface IsNot Nothing Then Skin.DrawStretch(g, surface, item.Bounds, horizontal:=True)

                DrawIconAndText(g, item)
            Next
        End Sub

        Private Sub DrawIconAndText(ByVal g As Graphics, ByVal item As ButtonItem)
            Dim hasText As Boolean = item.Text.Length > 0
            Dim hasIcon As Boolean = item.Icon IsNot Nothing
            If Not hasText AndAlso Not hasIcon Then Return

            Dim b As Rectangle = item.Bounds
            Dim textSize As Size = If(hasText, TextRenderer.MeasureText(item.Text, Font), Size.Empty)
            Dim iconSize As Size = If(hasIcon, item.Icon.Size, Size.Empty)
            Dim iconRect As Rectangle, textRect As Rectangle

            If hasText AndAlso hasIcon Then
                Select Case _iconAlignment
                    Case ButtonsIconAlignment.Top
                        Dim totalH As Integer = iconSize.Height + IconTextGapV + textSize.Height
                        Dim top As Integer = b.Top + (b.Height - totalH) \ 2
                        iconRect = New Rectangle(b.Left + (b.Width - iconSize.Width) \ 2, top, iconSize.Width, iconSize.Height)
                        textRect = New Rectangle(b.Left + (b.Width - textSize.Width) \ 2, top + iconSize.Height + IconTextGapV, textSize.Width, textSize.Height)
                    Case ButtonsIconAlignment.Bottom
                        Dim totalH As Integer = iconSize.Height + IconTextGapV + textSize.Height
                        Dim top As Integer = b.Top + (b.Height - totalH) \ 2
                        textRect = New Rectangle(b.Left + (b.Width - textSize.Width) \ 2, top, textSize.Width, textSize.Height)
                        iconRect = New Rectangle(b.Left + (b.Width - iconSize.Width) \ 2, top + textSize.Height + IconTextGapV, iconSize.Width, iconSize.Height)
                    Case ButtonsIconAlignment.Left
                        Dim totalW As Integer = iconSize.Width + IconTextGapH + textSize.Width
                        Dim left As Integer = b.Left + (b.Width - totalW) \ 2
                        iconRect = New Rectangle(left, b.Top + (b.Height - iconSize.Height) \ 2, iconSize.Width, iconSize.Height)
                        textRect = New Rectangle(left + iconSize.Width + IconTextGapH, b.Top + (b.Height - textSize.Height) \ 2, textSize.Width, textSize.Height)
                    Case Else ' Right
                        Dim totalW As Integer = iconSize.Width + IconTextGapH + textSize.Width
                        Dim left As Integer = b.Left + (b.Width - totalW) \ 2
                        textRect = New Rectangle(left, b.Top + (b.Height - textSize.Height) \ 2, textSize.Width, textSize.Height)
                        iconRect = New Rectangle(left + textSize.Width + IconTextGapH, b.Top + (b.Height - iconSize.Height) \ 2, iconSize.Width, iconSize.Height)
                End Select
            ElseIf hasText Then
                textRect = New Rectangle(b.Left + (b.Width - textSize.Width) \ 2, b.Top + (b.Height - textSize.Height) \ 2, textSize.Width, textSize.Height)
            Else
                iconRect = New Rectangle(b.Left + (b.Width - iconSize.Width) \ 2, b.Top + (b.Height - iconSize.Height) \ 2, iconSize.Width, iconSize.Height)
            End If

            If hasIcon Then g.DrawImage(item.Icon, iconRect)
            If hasText Then TextRenderer.DrawText(g, item.Text, Font, textRect, ForeColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Sub

        '=====================================================================
        ' Interaction (ports of picButton_Click / UserControl_MouseDown / _KeyDown / hover timers)
        '=====================================================================
        Private Function HitTest(ByVal x As Integer) As Integer
            For i = 0 To _items.Count - 1
                If x >= _items(i).Bounds.Left AndAlso x < _items(i).Bounds.Right Then Return i
            Next
            Return -1
        End Function

        Private Sub SelectByClick(ByVal index As Integer)
            Dim prev As Integer = _selIndex
            SoundUtil.PlaySound(_soundClick)
            _selIndex = index
            Invalidate()
            MyBase.OnClick(EventArgs.Empty)
            RaiseEvent Click(Me, index)
            If prev <> _selIndex Then RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If e.Button = MouseButtons.Left Then
                Dim idx As Integer = HitTest(e.X)
                If idx >= 0 Then SelectByClick(idx)
            End If
            If Not _pressTimer.Enabled Then
                _pressButton = ButtonBits(e.Button)
                _pressShift = ModifierBits()
                _pressTimer.Interval = 1000
                _pressTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressTimer.Stop()
        End Sub

        Private Sub OnPressTick(sender As Object, e As EventArgs)
            _pressTimer.Interval = 100   ' VB6: first fire waits 1000ms, then repeats every 100ms
            RaiseEvent MousePress(_pressButton, _pressShift)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _mouseHoverFired = False
            SoundUtil.PlaySound(_soundMouseEnter)
            If _hoverInterval > 0 Then
                _hoverTimer.Interval = _hoverInterval
                _hoverTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseHoverFired = False
            _hoverTimer.Stop()
            SoundUtil.PlaySound(_soundMouseLeave)
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If _mouseHoverFired Then Return
            _mouseHoverFired = True
            SoundUtil.PlaySound(_soundMouseHover)
            RaiseEvent MouseHover(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            SoundUtil.PlaySound(_soundEnterFocus)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            SoundUtil.PlaySound(_soundExitFocus)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
        End Sub

        ''' <summary>VB6 navigated between the real per-button PictureBox controls with Left/Right;
        ''' the 0/Count-1 boundary checks don't clamp a negative SelectedIndex, so repeatedly
        ''' pressing Left while nothing is selected (-1) walks it below -1 forever -- a real VB6
        ''' quirk, preserved rather than silently clamped.</summary>
        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Left
                    If SelectedIndex = 0 Then Return
                    SelectedIndex -= 1
                Case Keys.Right
                    If SelectedIndex = _items.Count - 1 Then Return
                    SelectedIndex += 1
            End Select
        End Sub

        ''' <summary>Adapted from VB6's per-button picButton_KeyPress(index, ...): since there is
        ''' one focusable control here (not one per segment), Enter/Space activates whichever
        ''' segment SelectedIndex currently points at.</summary>
        Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
            MyBase.OnKeyPress(e)
            If (e.KeyChar = ControlChars.Cr OrElse e.KeyChar = " "c) AndAlso _selIndex >= 0 AndAlso _selIndex < _items.Count Then
                SelectByClick(_selIndex)
            End If
        End Sub

        Protected Overrides Sub OnForeColorChanged(e As EventArgs)
            MyBase.OnForeColorChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        Private Shared Function ButtonBits(ByVal b As MouseButtons) As Integer
            Select Case b
                Case MouseButtons.Left : Return 1
                Case MouseButtons.Right : Return 2
                Case MouseButtons.Middle : Return 4
                Case Else : Return 0
            End Select
        End Function

        Private Shared Function ModifierBits() As Integer
            Dim m As Integer = 0
            If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then m = m Or 1
            If (Control.ModifierKeys And Keys.Control) = Keys.Control Then m = m Or 2
            If (Control.ModifierKeys And Keys.Alt) = Keys.Alt Then m = m Or 4
            Return m
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _pressTimer.Stop() : _pressTimer.Dispose()
                _hoverTimer.Stop() : _hoverTimer.Dispose()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
