Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.DropDownList UserControl (Control\DropDownList.ctl): a themed pill-shaped
' selector button -- click the right-hand push area to drop a menu of choices, pick one, its
' text/icon become the displayed value. VB6 built the dropdown itself as a dedicated frmMenu popup
' form holding a copy of the selected Aqua.MenuItem tree; this project already has that exact same
' machinery (MenuItem.vb + Internal\MenuPopupForm.vb, built for AquaForm's own main menu), so the
' dropdown here reuses it directly instead of re-implementing a popup list.
'
' Not ported: VB6's "Parhelia" glow-on-focus (DrawControlParhelia/ShowParhelia) -- same simplification
' FlashButton.vb already made (see its file header) for the same reason (no .NET equivalent of the
' VB6 sibling-container coordination it relied on). Also dropped: UserControl_Click's Wait(...)
' call, an 80ms blocking Sleep after every click with no render effect anywhere in the VB6 source
' (m_bolClick, the flag it set, was never actually read) -- keeping a blocking sleep on the UI
' thread would be a straight regression, not a faithful port.
Namespace Global.Aqua

    <DefaultEvent("SelectedChanged")>
    Public Class DropDownList
        Inherits Control

        Private Const IconTextGap As Integer = 4
        Private Const HoverIntervalMs As Integer = 100

        Private ReadOnly _root As New MenuItem()
        Private ReadOnly _hoverTimer As New Timer()

        Private _popup As MenuPopupForm
        Private _selectedItem As MenuItem
        Private _color As ColorConstants = ColorConstants.Blue
        Private _activeControl As Boolean = True
        Private _mouseEnter As Boolean = False
        Private _hover As Boolean = False
        Private _pushHover As Boolean = False
        Private _soundClick As String = ""
        Private _soundMouseEnter As String = ""
        Private _soundMouseHover As String = ""
        Private _soundMouseLeave As String = ""
        Private _soundEnterFocus As String = ""
        Private _soundExitFocus As String = ""

        Public Event MenuOpen(sender As Object, e As EventArgs)
        Public Event MenuClose(sender As Object, e As EventArgs)
        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ColorChanged(sender As Object, e As EventArgs)
        Public Event ActiveControlChanged(sender As Object, e As EventArgs)
        Public Shadows Event MouseHover(sender As Object, e As EventArgs)
        Public Event EnterFocus(sender As Object, e As EventArgs)
        Public Event ExitFocus(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            TabStop = True
            MyBase.Font = New Font("Segoe UI", 12.0!)

            _hoverTimer.Interval = HoverIntervalMs
            AddHandler _hoverTimer.Tick, AddressOf OnHoverTick

            UpdateRegion()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(220, FixedHeight())
            End Get
        End Property

        ''' <summary>Natural height of the border/mask/push-button art (all 21px tall). Skin.DrawStretch's
        ''' horizontal 3-slice only preserves the end caps' WIDTH -- vertically it just stretches the
        ''' whole source image to fill dest.Height, so any Height other than this exact native height
        ''' warps the rounded caps and the push button out of shape. VB6's own DropDownList.ctl never
        ''' actually locked its height (unlike Loading.ctl/ListBox.ctl, which did), so a consumer could
        ''' hit this distortion by resizing taller/shorter; locking it here the same way Loading.vb
        ''' locks its own bar-art height closes that off instead of leaving it as a trap.</summary>
        Private Function FixedHeight() As Integer
            Dim border As Image = DropDownListResources.GetBorder(True)
            Return If(border IsNot Nothing, border.Height, 21)
        End Function

        Protected Overrides Sub SetBoundsCore(x As Integer, y As Integer, width As Integer, height As Integer, specified As BoundsSpecified)
            MyBase.SetBoundsCore(x, y, width, FixedHeight(), specified)
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Items (ports of AddItem/Clear/Count/Item)
        '=====================================================================
        Private _items As MenuItemCollection

        ''' <summary>The choices, editable in the designer: Name is the item's value (VB6 Value),
        ''' Text / Icon / SelIcon what the list shows. AddItem / Clear work on the same list.</summary>
        <Category("行為"), Description("選項（Name = 值，Text / Icon / SelIcon = 顯示）。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        <Editor(GetType(System.ComponentModel.Design.CollectionEditor), GetType(System.Drawing.Design.UITypeEditor))>
        Public ReadOnly Property Items As MenuItemCollection
            Get
                If _items Is Nothing Then _items = New MenuItemCollection(AddressOf RebuildItems)
                Return _items
            End Get
        End Property

        Private Sub RebuildItems()
            _root.Clear()
            For Each item As MenuItem In Items
                item.Clear()
                item.CheckStyle = ItemCheckStyle.None
                _root.AddItem(item)
            Next
            If _selectedItem IsNot Nothing AndAlso Not Items.Contains(_selectedItem) Then _selectedItem = Nothing
            Invalidate()
        End Sub

        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _root.Count
            End Get
        End Property

        Public ReadOnly Property Item(ByVal index As Integer) As MenuItem
            Get
                Return _root(index)
            End Get
        End Property

        Public Function AddItem(ByVal value As String, Optional ByVal text As String = "") As MenuItem
            Dim item As New MenuItem() With {.Name = value, .Text = If(text, ""), .CheckStyle = ItemCheckStyle.None}
            Items.Add(item)
            Return item
        End Function

        Public Sub Clear()
            Items.Clear()
            _selectedItem = Nothing
            Invalidate()
        End Sub

        '=====================================================================
        ' Selection
        '=====================================================================
        <Browsable(False)>
        Public Property SelectedIndex As Integer
            Get
                If _selectedItem Is Nothing Then Return -1
                For i = 0 To _root.Count - 1
                    If ReferenceEquals(_root(i), _selectedItem) Then Return i
                Next
                Return -1
            End Get
            Set(value As Integer)
                For i = 0 To _root.Count - 1
                    Dim item As MenuItem = _root(i)
                    If i = value Then
                        ChooseItem(item)
                        item.Selected = True
                    Else
                        item.Selected = False
                    End If
                Next
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedItem As MenuItem
            Get
                Return _selectedItem
            End Get
        End Property

        ''' <summary>The selected item's Text, read-only, matching VB6's Text (VB6: Property Get
        ''' Text mapped straight to lblText.Caption, which only ever changed via item selection).</summary>
        Public Overrides Property Text As String
            Get
                Return If(_selectedItem IsNot Nothing, _selectedItem.Text, "")
            End Get
            Set(value As String)
                ' No public setter in VB6 either -- Text always came from picking an item.
            End Set
        End Property

        Private Sub ChooseItem(ByVal item As MenuItem)
            If ReferenceEquals(_selectedItem, item) Then Return
            _selectedItem = item
            Invalidate()
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
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

        ''' <summary>Grid-row style active/inactive tint (VB6: ActiveControl) -- safe to keep VB6's
        ''' own name here (unlike TextBox.vb's RowActive) since DropDownList inherits plain Control,
        ''' not UserControl/ContainerControl, so there's no built-in ActiveControl to collide with.</summary>
        <Browsable(False)>
        Public Property ActiveControl As Boolean
            Get
                Return _activeControl
            End Get
            Set(value As Boolean)
                _activeControl = value
                Invalidate()
                RaiseEvent ActiveControlChanged(Me, EventArgs.Empty)
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

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        '=====================================================================
        ' Layout (port of SetUserControlPosition / SetTextPosition)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
        End Sub

        Private Function ComputePushRect() As Rectangle
            Dim pushW As Integer = Math.Min(Width, CInt(Height / 7.0 * 6.0))
            Return New Rectangle(Width - pushW, 0, pushW, Height)
        End Function

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            ' same 3-slice stretch as the border/background art in OnPaint: stretching the whole mask
            ' widened its rounded ends, so on a wide list the region cut a gap between the drawn
            ' left/right caps and the body
            Me.Region = RegionUtil.CreateStretchedMaskRegion(DropDownListResources.GetMask(), Width, Height, horizontal:=True)
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        '=====================================================================
        ' Painting (ports of DrawBackground / DrawPushBackground / DrawUserControlText)
        '=====================================================================
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            ' full painting happens in OnPaint
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            Dim active As Boolean = Enabled AndAlso _activeControl

            Dim back As Image = DropDownListResources.GetBackground(active)
            If back IsNot Nothing Then Skin.DrawStretch(g, back, ClientRectangle, horizontal:=True)
            Dim border As Image = DropDownListResources.GetBorder(active)
            If border IsNot Nothing Then Skin.DrawStretch(g, border, ClientRectangle, horizontal:=True)

            Dim pushArea As Rectangle = ComputePushRect()
            Dim pushImg As Image = DropDownListResources.GetPushBackground(_color, active, active AndAlso _pushHover)
            If pushImg IsNot Nothing Then g.DrawImage(pushImg, pushArea)
            Dim arrow As Image = DropDownListResources.GetPushUpDown(active)
            If arrow IsNot Nothing Then
                g.DrawImage(arrow, New Point(pushArea.X + (pushArea.Width - arrow.Width) \ 2, pushArea.Y + (pushArea.Height - arrow.Height) \ 2))
            End If

            DrawContent(g, pushArea)
        End Sub

        Private Sub DrawContent(ByVal g As Graphics, ByVal pushArea As Rectangle)
            Dim text As String = Me.Text
            Dim icon As Image = If(_selectedItem IsNot Nothing, _selectedItem.Icon, Nothing)
            Dim ts As Size = If(String.IsNullOrEmpty(text), Size.Empty,
                                TextRenderer.MeasureText(g, text, Font, New Size(Integer.MaxValue, Integer.MaxValue),
                                                          TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine))
            Dim iconW As Integer = If(icon IsNot Nothing, icon.Width, 0)
            Dim groupW As Integer = iconW + If(icon IsNot Nothing AndAlso ts.Width > 0, IconTextGap, 0) + ts.Width
            Dim availableW As Integer = pushArea.Left
            Dim left As Integer = Math.Max(0, (availableW - groupW) \ 2)

            If icon IsNot Nothing Then
                g.DrawImage(icon, New Point(left, (Height - icon.Height) \ 2))
                left += iconW + IconTextGap
            End If

            If ts.Width > 0 Then
                Dim fc As Color = If(Enabled AndAlso _activeControl, ForeColor, ColorUtil.OleToColor(12435133))   ' gc_lngDisableForeColor
                TextRenderer.DrawText(g, text, Font, New Point(left, (Height - ts.Height) \ 2), fc,
                                      TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
            End If
        End Sub

        '=====================================================================
        ' Popup (ports of picPush_Click / m_lpMenu_ItemClick / CloseMenu)
        '=====================================================================
        Private Sub OpenPopup()
            If _root.Count <= 0 Then Return
            If _root.Count = 1 AndAlso _root(0).Selected Then Return
            CloseMenu()
            RaiseEvent MenuOpen(Me, EventArgs.Empty)

            _popup = New MenuPopupForm()
            AddHandler _popup.ItemClicked, AddressOf OnPopupItemClicked
            AddHandler _popup.AutoClosed, AddressOf OnPopupAutoClosed
            Dim anchor As Point = PointToScreen(New Point(0, Height))
            _popup.ShowFor(_root, anchor, Font)
        End Sub

        Private Sub OnPopupItemClicked(ByVal item As MenuItem)
            CloseMenu()
            ChooseItem(item)
        End Sub

        Private Sub OnPopupAutoClosed(sender As Object, e As EventArgs)
            CloseMenu()
        End Sub

        Public Sub CloseMenu()
            If _popup Is Nothing Then Return
            If Not _popup.IsDisposed Then
                _popup.CloseAll()
                _popup.Dispose()
            End If
            _popup = Nothing
            RaiseEvent MenuClose(Me, EventArgs.Empty)
        End Sub

        '=====================================================================
        ' Mouse / focus (ports of UserControl_MouseMove/EnterFocus/ExitFocus, minus Parhelia -- see header)
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            Focus()
            If ComputePushRect().Contains(e.Location) Then
                OpenPopup()
            End If
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            SoundUtil.PlaySound(_soundClick)
            MyBase.OnClick(e)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim overPush As Boolean = ComputePushRect().Contains(e.Location)
            If overPush <> _pushHover Then
                _pushHover = overPush
                Invalidate()
            End If
            If Not _mouseEnter Then
                _mouseEnter = True
                _hover = False
                SoundUtil.PlaySound(_soundMouseEnter)
                MyBase.OnMouseEnter(e)
                _hoverTimer.Start()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _mouseEnter = False
            _hover = False
            _hoverTimer.Stop()
            If _pushHover Then
                _pushHover = False
                Invalidate()
            End If
            SoundUtil.PlaySound(_soundMouseLeave)
        End Sub

        Private Sub OnHoverTick(sender As Object, e As EventArgs)
            _hoverTimer.Stop()
            If _hover Then Return
            _hover = True
            SoundUtil.PlaySound(_soundMouseHover)
            RaiseEvent MouseHover(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            SoundUtil.PlaySound(_soundEnterFocus)
            RaiseEvent EnterFocus(Me, EventArgs.Empty)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            SoundUtil.PlaySound(_soundExitFocus)
            RaiseEvent ExitFocus(Me, EventArgs.Empty)
            Invalidate()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _hoverTimer.Stop()
                _hoverTimer.Dispose()
                CloseMenu()
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
