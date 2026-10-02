Option Strict On
Option Explicit On

Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Drawing.Design
Imports System.Drawing.Imaging
Imports System.Windows.Forms

' Port of the VB6 Aqua.ToolBar UserControl (Control\ToolBar.ctl): a flat strip of icon+text
' buttons (iPhoto's viewer: 左轉90° / 右轉90° / 紅眼 / 裁切 ...) that raises Click(index).
'
' VB6 built it from a PictureBox control array (one per button) holding an Aqua.Icon + Label pair.
' Like Buttons.vb, this port owner-draws the whole strip and hit-tests instead, and exposes the
' buttons as an Items collection editable in the designer (VB6 persisted them as Count / Text_i /
' Icon_i; AdditionButton / Clear / SetButtonProperty still work from code).
'
' Layout (ports of SetUserControlSizeMixed / SetUserControlSizeTextMode / SetUserControlPosition):
' horizontal strips split the width evenly -- or, when no button has an icon, in proportion to the
' text widths -- vertical strips split the height evenly; icon and text are stacked per
' IconAlignment with a 4px gap. Hover fills the button with HoverColor between two grey side lines
' (DrawHoverConot); pressing shows HoverColor 30 darker. VB6 did that with a blocking 100ms Wait
' before raising Click; here the darker colour shows while the mouse is down and Click fires on
' release over the same button, which is the same visual without freezing the UI thread.
'
' Icons are drawn with IconTransparencyKey (magenta by default, the Aqua.Icon default VB6 used for
' them) made transparent, so the BMPs from the .frx can be used as-is.
Namespace Global.Aqua

    <DefaultEvent("Click")>
    Public Class ToolBar
        Inherits Control

        ''' <summary>One button (VB6: Text_i / Icon_i).</summary>
        <TypeConverter(GetType(TextIconItemConverter))>
        Public Class ToolBarItem
            Public Sub New()
            End Sub

            Public Sub New(ByVal text As String, ByVal icon As Image)
                Me.Text = If(text, "")
                Me.Icon = icon
            End Sub

            Public Property Text As String = ""
            Public Property Icon As Image

            Friend Property Bounds As Rectangle

            Public Overrides Function ToString() As String
                Return If(String.IsNullOrEmpty(Text), "ToolBarItem", Text)
            End Function
        End Class

        Public Class ToolBarItemCollection
            Inherits Collection(Of ToolBarItem)

            Private ReadOnly _owner As ToolBar

            Friend Sub New(ByVal owner As ToolBar)
                _owner = owner
            End Sub

            Protected Overrides Sub InsertItem(ByVal index As Integer, ByVal item As ToolBarItem)
                MyBase.InsertItem(index, If(item, New ToolBarItem()))
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub RemoveItem(ByVal index As Integer)
                MyBase.RemoveItem(index)
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub SetItem(ByVal index As Integer, ByVal item As ToolBarItem)
                MyBase.SetItem(index, If(item, New ToolBarItem()))
                _owner.OnItemsChanged()
            End Sub

            Protected Overrides Sub ClearItems()
                MyBase.ClearItems()
                _owner.OnItemsChanged()
            End Sub
        End Class

        Private Const IconTextGap As Integer = 4     ' VB6: 4 * Screen.TwipsPerPixel
        Private Const SideMargin As Integer = 6      ' VB6 vertical strip, Left/Right icon alignment

        Private ReadOnly _items As ToolBarItemCollection
        Private _iconAlignment As ButtonsIconAlignment = ButtonsIconAlignment.Top
        Private _orientation As OrientationMode = OrientationMode.Horizontal
        Private _hoverColor As Color = Color.FromArgb(224, 224, 224)   ' gc_lngHoverColor &HE0E0E0
        Private _iconKey As Color = Color.FromArgb(255, 0, 255)
        Private _hoverIndex As Integer = -1
        Private _pressIndex As Integer = -1
        Private _soundClick, _soundMouseEnter, _soundMouseLeave As String

        ''' <summary>A button was clicked (VB6: Click(index)). Shadows the parameterless Click,
        ''' which still fires as well.</summary>
        Public Shadows Event Click(sender As Object, index As Integer)
        ''' <summary>The mouse moved onto / off a button (VB6: MouseEnter(index) / MouseLeave(index)).</summary>
        Public Event ButtonMouseEnter(sender As Object, index As Integer)
        Public Event ButtonMouseLeave(sender As Object, index As Integer)
        Public Event IconAlignmentChanged(sender As Object, e As EventArgs)
        Public Event OrientationChanged(sender As Object, e As EventArgs)
        Public Event HoverColorChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            _items = New ToolBarItemCollection(Me)
            ' VB6 InitProperties: vbWhite. BackColor = Transparent (not possible in VB6) shows the
            ' parent instead: the base background painting handles that.
            MyBase.BackColor = Color.White
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(400, 72)
            End Get
        End Property

        '=====================================================================
        ' Buttons
        '=====================================================================
        <Category("行為")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        <Editor(GetType(CollectionEditor), GetType(UITypeEditor))>
        Public ReadOnly Property Items As ToolBarItemCollection
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

        ''' <summary>Button text (VB6: Text(index)).</summary>
        Public Function GetText(ByVal index As Integer) As String
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            Return _items(index).Text
        End Function

        ''' <summary>Button icon (VB6: Icon(index)).</summary>
        Public Function GetIcon(ByVal index As Integer) As Image
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            Return _items(index).Icon
        End Function

        Public Sub AdditionButton(ByVal text As String, ByVal icon As Image)
            _items.Add(New ToolBarItem(text, icon))
        End Sub

        Public Sub Clear()
            _items.Clear()
        End Sub

        ''' <summary>VB6: relayout after a batch of AdditionButton calls. Items already relayouts on
        ''' every change, so this is only an explicit refresh.</summary>
        Public Sub SetButtonProperty()
            OnItemsChanged()
        End Sub

        Friend Sub OnItemsChanged()
            _hoverIndex = -1
            _pressIndex = -1
            DoLayout()
            Invalidate()
        End Sub

        ''' <summary>Index of the button under <paramref name="p"/> (client coordinates), or -1.</summary>
        Public Function HitTest(ByVal p As Point) As Integer
            For i = 0 To _items.Count - 1
                If _items(i).Bounds.Contains(p) Then Return i
            Next
            Return -1
        End Function

        '=====================================================================
        ' Appearance
        '=====================================================================
        <Category("外觀"), DefaultValue(ButtonsIconAlignment.Top)>
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

        <Category("外觀"), DefaultValue(OrientationMode.Horizontal)>
        Public Property Orientation As OrientationMode
            Get
                Return _orientation
            End Get
            Set(value As OrientationMode)
                If _orientation = value Then Return
                _orientation = value
                DoLayout()
                Invalidate()
                RaiseEvent OrientationChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀"), DefaultValue(GetType(Color), "224, 224, 224")>
        Public Property HoverColor As Color
            Get
                Return _hoverColor
            End Get
            Set(value As Color)
                If _hoverColor = value Then Return
                _hoverColor = value
                Invalidate()
                RaiseEvent HoverColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Icon colour drawn as transparent (VB6 drew the icons with Aqua.Icon, default key magenta).
        ''' Color.Empty draws icons as they are.</summary>
        <Category("外觀"), DefaultValue(GetType(Color), "Magenta")>
        Public Property IconTransparencyKey As Color
            Get
                Return _iconKey
            End Get
            Set(value As Color)
                _iconKey = value
                Invalidate()
            End Set
        End Property

        <DefaultValue(GetType(Color), "White")>
        Public Overrides Property BackColor As Color
            Get
                Return MyBase.BackColor
            End Get
            Set(value As Color)
                MyBase.BackColor = value
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
        Public Property SoundFileOfMouseEnter As String
            Get
                Return _soundMouseEnter
            End Get
            Set(value As String)
                _soundMouseEnter = value
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

        '=====================================================================
        ' Layout
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            DoLayout()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            DoLayout()
            Invalidate()
        End Sub

        Private Sub DoLayout()
            Dim n As Integer = _items.Count
            If n = 0 Then Return
            If _orientation = OrientationMode.Vertical Then
                Dim y As Integer = 0
                For i = 0 To n - 1
                    Dim h As Integer = If(i = n - 1, Height - y, Height \ n)
                    _items(i).Bounds = New Rectangle(0, y, Width, h)
                    y += h
                Next
                Return
            End If

            Dim anyIcon As Boolean = False
            For Each it In _items
                If it.Icon IsNot Nothing Then anyIcon = True : Exit For
            Next
            Dim widths(n - 1) As Integer
            If anyIcon OrElse n = 1 Then
                For i = 0 To n - 1 : widths(i) = Width \ n : Next
            Else
                ' text-only strip: widths in proportion to the text (SetUserControlSizeTextMode)
                Dim total As Integer = 0
                For i = 0 To n - 1
                    widths(i) = Math.Max(1, TextRenderer.MeasureText(_items(i).Text, Font).Width)
                    total += widths(i)
                Next
                For i = 0 To n - 1 : widths(i) = CInt(CLng(widths(i)) * Width \ total) : Next
            End If
            Dim x As Integer = 0
            For i = 0 To n - 1
                Dim w As Integer = If(i = n - 1, Width - x, widths(i))   ' last one takes the remainder
                _items(i).Bounds = New Rectangle(x, 0, w, Height)
                x += w
            Next
        End Sub

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics   ' background (BackColor, or the parent's) is already painted
            For i = 0 To _items.Count - 1
                Dim it As ToolBarItem = _items(i)
                Dim r As Rectangle = it.Bounds
                If Enabled AndAlso (i = _hoverIndex OrElse i = _pressIndex) Then
                    Dim fill As Color = If(i = _pressIndex AndAlso i = _hoverIndex, ColorUtil.ShiftChannels(_hoverColor, -30), _hoverColor)
                    Using b As New SolidBrush(fill)
                        g.FillRectangle(b, r)
                    End Using
                    Using p As New Pen(Color.FromArgb(165, 166, 165), 2)
                        g.DrawLine(p, r.Left + 1, r.Top, r.Left + 1, r.Bottom)
                        g.DrawLine(p, r.Right - 1, r.Top, r.Right - 1, r.Bottom)
                    End Using
                End If
                DrawContent(g, it)
            Next
        End Sub

        Private Sub DrawContent(ByVal g As Graphics, ByVal it As ToolBarItem)
            Dim r As Rectangle = it.Bounds
            Dim hasText As Boolean = it.Text IsNot Nothing AndAlso it.Text.Trim().Length > 0
            Dim icon As Image = it.Icon
            Dim ts As Size = If(hasText, TextRenderer.MeasureText(g, it.Text, Font, Size.Empty, TextFormatFlags.NoPadding), Size.Empty)
            Dim isz As Size = If(icon IsNot Nothing, icon.Size, Size.Empty)
            Dim iconPt As Point, textPt As Point

            If hasText AndAlso icon IsNot Nothing Then
                Select Case _iconAlignment
                    Case ButtonsIconAlignment.Top, ButtonsIconAlignment.Bottom
                        Dim top As Integer = r.Top + (r.Height - (isz.Height + IconTextGap + ts.Height)) \ 2
                        Dim iconFirst As Boolean = (_iconAlignment = ButtonsIconAlignment.Top)
                        iconPt = New Point(r.Left + (r.Width - isz.Width) \ 2, If(iconFirst, top, top + ts.Height + IconTextGap))
                        textPt = New Point(r.Left + (r.Width - ts.Width) \ 2, If(iconFirst, top + isz.Height + IconTextGap, top))
                    Case Else ' Left / Right
                        Dim iconFirst As Boolean = (_iconAlignment = ButtonsIconAlignment.Left)
                        Dim left As Integer
                        If _orientation = OrientationMode.Vertical Then
                            ' vertical strip: pinned to the side, not centred (VB6)
                            left = If(iconFirst, r.Left + SideMargin, r.Right - SideMargin - isz.Width - IconTextGap - ts.Width)
                        Else
                            left = r.Left + (r.Width - (isz.Width + IconTextGap + ts.Width)) \ 2
                        End If
                        iconPt = New Point(If(iconFirst, left, left + ts.Width + IconTextGap), r.Top + (r.Height - isz.Height) \ 2)
                        textPt = New Point(If(iconFirst, left + isz.Width + IconTextGap, left), r.Top + (r.Height - ts.Height) \ 2)
                End Select
                DrawIcon(g, icon, iconPt)
                DrawText(g, it.Text, textPt)
            ElseIf hasText Then
                DrawText(g, it.Text, New Point(r.Left + (r.Width - ts.Width) \ 2, r.Top + (r.Height - ts.Height) \ 2))
            ElseIf icon IsNot Nothing Then
                DrawIcon(g, icon, New Point(r.Left + (r.Width - isz.Width) \ 2, r.Top + (r.Height - isz.Height) \ 2))
            End If
        End Sub

        Private Sub DrawText(ByVal g As Graphics, ByVal text As String, ByVal pt As Point)
            Dim fc As Color = If(Enabled, ForeColor, ColorUtil.OleToColor(12435133))   ' gc_lngDisableForeColor
            TextRenderer.DrawText(g, text, Font, pt, fc, TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
        End Sub

        Private Sub DrawIcon(ByVal g As Graphics, ByVal icon As Image, ByVal pt As Point)
            Dim dest As New Rectangle(pt, icon.Size)
            If _iconKey.IsEmpty AndAlso Enabled Then
                g.DrawImage(icon, dest)
                Return
            End If
            Using ia As New ImageAttributes()
                If Not _iconKey.IsEmpty Then ia.SetColorKey(_iconKey, _iconKey)
                If Not Enabled Then
                    ' disabled: greyscale at half strength, as VB6's disabled Aqua.Icon looked
                    ia.SetColorMatrix(New ColorMatrix(New Single()() {
                        New Single() {0.3F, 0.3F, 0.3F, 0, 0},
                        New Single() {0.59F, 0.59F, 0.59F, 0, 0},
                        New Single() {0.11F, 0.11F, 0.11F, 0, 0},
                        New Single() {0, 0, 0, 0.5F, 0},
                        New Single() {0, 0, 0, 0, 1}}))
                End If
                g.DrawImage(icon, dest, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia)
            End Using
        End Sub

        '=====================================================================
        ' Mouse (ports of picButton_MouseMove / Timer1 leave detection / picButton_Click)
        '=====================================================================
        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            SetHover(HitTest(e.Location))
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            SetHover(-1)
        End Sub

        Private Sub SetHover(ByVal index As Integer)
            If index = _hoverIndex Then Return
            Dim old As Integer = _hoverIndex
            _hoverIndex = index
            Invalidate()
            If old >= 0 Then
                SoundUtil.PlaySound(_soundMouseLeave)
                RaiseEvent ButtonMouseLeave(Me, old)
            End If
            If index >= 0 Then
                SoundUtil.PlaySound(_soundMouseEnter)
                RaiseEvent ButtonMouseEnter(Me, index)
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left OrElse Not Enabled Then Return
            _pressIndex = HitTest(e.Location)
            If _pressIndex >= 0 Then Invalidate()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            Dim pressed As Integer = _pressIndex
            _pressIndex = -1
            If pressed < 0 Then Return
            Invalidate()
            If e.Button = MouseButtons.Left AndAlso HitTest(e.Location) = pressed Then
                SoundUtil.PlaySound(_soundClick)
                RaiseEvent Click(Me, pressed)
            End If
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _hoverIndex = -1
            _pressIndex = -1
            Invalidate()
        End Sub

    End Class

End Namespace
