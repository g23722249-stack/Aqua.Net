Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' A tabbed container with the Aqua PageHead + PageSheet skin, packaged as a single
' TabControl-like control: add AquaTabPages, set SelectedIndex/Alignment/Color. The tab
' strip is owner-drawn from the original shaped tab surfaces (three-slice stretched); the
' selected page is hosted in the bordered content area with the accent tab line.
Namespace Global.Aqua

    <DefaultEvent("SelectedIndexChanged")>
    <DefaultProperty("Alignment")>
    <Designer(GetType(AquaTabControlDesigner))>
    Public Class TabControl
        Inherits Control

        Private Const TopBottomStrip As Integer = 27   ' native tab surface height
        Private Const LeftRightStrip As Integer = 84   ' native tab surface width
        Private Const TabPad As Integer = 20           ' text padding inside a tab
        Private Const BorderPad As Integer = 3

        Private _selIndex As Integer = -1
        Private _alignment As PageStyle = PageStyle.Top
        Private _color As ColorConstants = ColorConstants.Blue
        Private _showTabLine As Boolean = True
        Private ReadOnly _tabPages As AquaTabPageCollection

        Public Event SelectedIndexChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            MyBase.BackColor = System.Drawing.Color.White
            _tabPages = New AquaTabPageCollection(Me)
            Size = New Size(400, 300)
        End Sub

        '=====================================================================
        ' Public API (TabControl-like)
        '=====================================================================
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        Public ReadOnly Property TabPages As AquaTabPageCollection
            Get
                Return _tabPages
            End Get
        End Property

        <Browsable(False)>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property SelectedIndex As Integer
            Get
                Return _selIndex
            End Get
            Set(value As Integer)
                If value < -1 OrElse value >= _tabPages.Count Then Return
                If _selIndex = value Then Return
                _selIndex = value
                UpdatePageVisibility()
                Invalidate()
                RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property SelectedTab As TabPage
            Get
                If _selIndex >= 0 AndAlso _selIndex < _tabPages.Count Then Return _tabPages(_selIndex)
                Return Nothing
            End Get
            Set(value As TabPage)
                Dim i As Integer = _tabPages.IndexOf(value)
                If i >= 0 Then SelectedIndex = i
            End Set
        End Property

        <DefaultValue(GetType(PageStyle), "Top")>
        Public Property Alignment As PageStyle
            Get
                Return _alignment
            End Get
            Set(value As PageStyle)
                If _alignment = value Then Return
                _alignment = value
                PerformLayoutPages()
                Invalidate()
            End Set
        End Property

        <DefaultValue(GetType(ColorConstants), "Blue")>
        Public Property Color As ColorConstants
            Get
                Return _color
            End Get
            Set(value As ColorConstants)
                If _color = value Then Return
                _color = value
                Invalidate()
            End Set
        End Property

        ''' <summary>How far the tab strip stays in from the ends of its edge: Left/Right for tabs on top or
        ''' bottom, Top/Bottom for tabs on the left or right (the other two values are not used).
        ''' Empty = the tabs span the whole edge. VB6 drew the tab strip (PageHead) as its own control,
        ''' often narrower than the pages under it; this reproduces that, e.g. centred with equal insets.</summary>
        <Category("外觀"), Description("標籤列兩端內縮的距離:標籤在上/下時用 Left、Right,在左/右時用 Top、Bottom。全部 0 = 標籤列佔滿整個邊。")>
        Public Property TabStripPadding As Padding
            Get
                Return _stripPadding
            End Get
            Set(value As Padding)
                If _stripPadding = value Then Return
                _stripPadding = value
                Invalidate()
            End Set
        End Property

        Private _stripPadding As Padding = Padding.Empty

        Private Function ShouldSerializeTabStripPadding() As Boolean
            Return _stripPadding <> Padding.Empty
        End Function

        Private Sub ResetTabStripPadding()
            TabStripPadding = Padding.Empty
        End Sub

        <DefaultValue(True)>
        Public Property ShowTabLine As Boolean
            Get
                Return _showTabLine
            End Get
            Set(value As Boolean)
                _showTabLine = value
                Invalidate()
            End Set
        End Property

        ''' <summary>Convenience: add a page with the given title and return it.</summary>
        Public Function AddTab(ByVal title As String) As TabPage
            Dim p As New TabPage(title)
            _tabPages.Add(p)
            Return p
        End Function

        '=====================================================================
        ' Page collection (single source of truth; parents pages into Controls).
        ' NOTE: removing a page does NOT dispose it -- the WinForms designer (and undo)
        ' keeps using the instance, exactly like TabControl.TabPages.
        '=====================================================================
        Public Class AquaTabPageCollection
            Inherits Collection(Of TabPage)

            Private ReadOnly _owner As TabControl

            Friend Sub New(owner As TabControl)
                _owner = owner
            End Sub

            Protected Overrides Sub InsertItem(index As Integer, item As TabPage)
                MyBase.InsertItem(index, item)
                If item IsNot Nothing AndAlso Not _owner.Controls.Contains(item) Then
                    _owner.Controls.Add(item)
                End If
                If _owner._selIndex < 0 Then _owner._selIndex = 0
                _owner.PerformLayoutPages()
                _owner.UpdatePageVisibility()
                _owner.Invalidate()
            End Sub

            Protected Overrides Sub RemoveItem(index As Integer)
                Dim item As TabPage = Me(index)
                MyBase.RemoveItem(index)
                If item IsNot Nothing AndAlso _owner.Controls.Contains(item) Then
                    _owner.Controls.Remove(item)   ' do not Dispose: designer owns lifetime
                End If
                If _owner._selIndex >= Count Then _owner._selIndex = Count - 1
                _owner.PerformLayoutPages()
                _owner.UpdatePageVisibility()
                _owner.Invalidate()
            End Sub

            Protected Overrides Sub SetItem(index As Integer, item As TabPage)
                Dim old As TabPage = Me(index)
                If old IsNot Nothing AndAlso Not ReferenceEquals(old, item) AndAlso _owner.Controls.Contains(old) Then
                    _owner.Controls.Remove(old)
                End If
                MyBase.SetItem(index, item)
                If item IsNot Nothing AndAlso Not _owner.Controls.Contains(item) Then
                    _owner.Controls.Add(item)
                End If
                _owner.PerformLayoutPages()
                _owner.UpdatePageVisibility()
                _owner.Invalidate()
            End Sub

            Protected Overrides Sub ClearItems()
                For Each p In Me
                    If p IsNot Nothing AndAlso _owner.Controls.Contains(p) Then _owner.Controls.Remove(p)
                Next
                MyBase.ClearItems()
                _owner._selIndex = -1
                _owner.Invalidate()
            End Sub
        End Class

        '=====================================================================
        ' Geometry
        '=====================================================================
        ''' <summary>
        ''' Pure hit-test: is the client point over the tab strip? Used by the designer's
        ''' GetHitTest to hand clicks to the control (which switches tabs in OnMouseDown).
        ''' Must have NO side effects — the designer calls it on every mouse move.
        ''' </summary>
        Friend Function IsOnHeader(ByVal clientPt As Point) As Boolean
            Return HeaderRect().Contains(clientPt)
        End Function

        Private ReadOnly Property IsHorizontalTabs As Boolean
            Get
                Return _alignment = PageStyle.Top OrElse _alignment = PageStyle.Bottom
            End Get
        End Property

        Private Function HeaderRect() As Rectangle
            Select Case _alignment
                Case PageStyle.Top : Return New Rectangle(0, 0, Width, TopBottomStrip)
                Case PageStyle.Bottom : Return New Rectangle(0, Height - TopBottomStrip, Width, TopBottomStrip)
                Case PageStyle.Left : Return New Rectangle(0, 0, LeftRightStrip, Height)
                Case Else : Return New Rectangle(Width - LeftRightStrip, 0, LeftRightStrip, Height)
            End Select
        End Function

        Private Function ContentRect() As Rectangle
            Select Case _alignment
                Case PageStyle.Top : Return New Rectangle(0, TopBottomStrip, Width, Height - TopBottomStrip)
                Case PageStyle.Bottom : Return New Rectangle(0, 0, Width, Height - TopBottomStrip)
                Case PageStyle.Left : Return New Rectangle(LeftRightStrip, 0, Width - LeftRightStrip, Height)
                Case Else : Return New Rectangle(0, 0, Width - LeftRightStrip, Height)
            End Select
        End Function

        ''' <summary>The part of the header band the tabs occupy (HeaderRect less TabStripPadding).</summary>
        Private Function StripRect() As Rectangle
            Dim hr As Rectangle = HeaderRect()
            If IsHorizontalTabs Then
                Dim l As Integer = Math.Min(_stripPadding.Left, hr.Width - 1)
                Dim w As Integer = Math.Max(1, hr.Width - l - _stripPadding.Right)
                Return New Rectangle(hr.Left + l, hr.Top, w, hr.Height)
            Else
                Dim t As Integer = Math.Min(_stripPadding.Top, hr.Height - 1)
                Dim h As Integer = Math.Max(1, hr.Height - t - _stripPadding.Bottom)
                Return New Rectangle(hr.Left, hr.Top + t, hr.Width, h)
            End If
        End Function

        Private Function TabRects() As Rectangle()
            Dim n As Integer = _tabPages.Count
            Dim rects(Math.Max(0, n - 1)) As Rectangle
            If n = 0 Then Return New Rectangle() {}
            Dim hr As Rectangle = StripRect()

            If IsHorizontalTabs Then
                Dim widths(n - 1) As Integer
                Dim total As Integer = 0
                For i = 0 To n - 1
                    widths(i) = TextRenderer.MeasureText(_tabPages(i).Title, Font).Width + TabPad
                    total += widths(i)
                Next
                If total <= 0 Then total = 1
                Dim x As Integer = hr.Left
                For i = 0 To n - 1
                    Dim w As Integer = CInt(widths(i) / CDbl(total) * hr.Width)
                    If i = n - 1 Then w = hr.Right - x
                    rects(i) = New Rectangle(x, hr.Top, w, hr.Height)
                    x += w
                Next
            Else
                Dim eachH As Integer = hr.Height \ n
                Dim y As Integer = hr.Top
                For i = 0 To n - 1
                    Dim h As Integer = If(i = n - 1, hr.Bottom - y, eachH)
                    rects(i) = New Rectangle(hr.Left, y, hr.Width, h)
                    y += h
                Next
            End If
            Return rects
        End Function

        '=====================================================================
        ' Layout of the hosted pages
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            PerformLayoutPages()
            Invalidate()
        End Sub

        Private Sub PerformLayoutPages()
            Dim cr As Rectangle = ContentRect()
            cr.Inflate(-BorderPad, -BorderPad)
            For Each p In _tabPages
                If p IsNot Nothing Then p.Bounds = cr
            Next
        End Sub

        Private Sub UpdatePageVisibility()
            For i = 0 To _tabPages.Count - 1
                Dim vis As Boolean = (i = _selIndex)
                _tabPages(i).Visible = vis
                If vis Then _tabPages(i).BringToFront()
            Next
        End Sub

        '=====================================================================
        ' Painting
        '=====================================================================
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            ' The header band shows the parent's own background -- its picture too, not just its
            ' colour -- so the shaped tabs' transparent corners and the ends of a narrower tab strip
            ' (TabStripPadding) look like the form behind, as the VB6 shaped-region PageHead did.
            If Parent IsNot Nothing Then
                Dim state As Drawing2D.GraphicsState = g.Save()
                g.SetClip(HeaderRect())
                ButtonRenderer.DrawParentBackground(g, ClientRectangle, Me)
                g.Restore(state)
            Else
                Using b As New SolidBrush(BackColor)
                    g.FillRectangle(b, HeaderRect())
                End Using
            End If

            DrawContentBorder(g, ContentRect())
            DrawTabLine(g)

            Dim rects As Rectangle() = TabRects()
            If rects.Length = 0 Then Return
            For i = 0 To _tabPages.Count - 1
                If i <> _selIndex Then DrawTab(g, i, rects(i))
            Next
            If _selIndex >= 0 AndAlso _selIndex < _tabPages.Count Then DrawTab(g, _selIndex, rects(_selIndex))
        End Sub

        Private Sub DrawTab(g As Graphics, ByVal index As Integer, ByVal rect As Rectangle)
            If rect.Width <= 0 OrElse rect.Height <= 0 Then Return
            Dim selected As Boolean = (index = _selIndex)
            Dim surf As Image = PageResources.GetTabSurface(_alignment, _color, selected)
            If surf IsNot Nothing Then Skin.DrawStretch(g, surf, rect, IsHorizontalTabs)

            Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                           TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis
            TextRenderer.DrawText(g, _tabPages(index).Title, Font, rect, ForeColor, flags)
        End Sub

        Private Sub DrawContentBorder(g As Graphics, ByVal rect As Rectangle)
            If rect.Width <= 2 OrElse rect.Height <= 2 Then Return
            Dim baseColor As Color = ColorUtil.OleToColor(12434877)   ' gc_lngBorderColor RGB(189,189,189)
            Using p As New Pen(baseColor, 2)
                g.DrawRectangle(p, rect.Left + 1, rect.Top + 1, rect.Width - 2, rect.Height - 2)
            End Using
            Using p As New Pen(ColorUtil.ShiftChannels(baseColor, -24), 1)
                g.DrawRectangle(p, rect.Left + 1, rect.Top + 1, rect.Width - 3, rect.Height - 3)
            End Using
        End Sub

        Private Sub DrawTabLine(g As Graphics)
            If Not _showTabLine OrElse _selIndex < 0 Then Return
            Dim cr As Rectangle = ContentRect()
            Dim c As Color = PageResources.TabLineColor(_color)
            Const t As Integer = 3
            Dim r As Rectangle
            Select Case _alignment
                Case PageStyle.Top : r = New Rectangle(cr.Left, cr.Top, cr.Width, t)
                Case PageStyle.Bottom : r = New Rectangle(cr.Left, cr.Bottom - t, cr.Width, t)
                Case PageStyle.Left : r = New Rectangle(cr.Left, cr.Top, t, cr.Height)
                Case Else : r = New Rectangle(cr.Right - t, cr.Top, t, cr.Height)
            End Select
            Using b As New SolidBrush(c)
                g.FillRectangle(b, r)
            End Using
        End Sub

        '=====================================================================
        ' Interaction
        '=====================================================================
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then SelectTabAt(e.Location)
        End Sub

        ''' <summary>Selects the tab whose head is under <paramref name="clientPt"/>; False when there
        ''' is none. Also used by AquaTabControlDesigner: the designer doesn't pass clicks on to
        ''' OnMouseDown, even where GetHitTest says the header is "live".</summary>
        Friend Function SelectTabAt(ByVal clientPt As Point) As Boolean
            Dim rects As Rectangle() = TabRects()
            For i = 0 To rects.Length - 1
                If rects(i).Contains(clientPt) Then
                    SelectedIndex = i
                    Return True
                End If
            Next
            Return False
        End Function

        '=====================================================================
        ' Design time: switching pages by clicking the tab heads
        '=====================================================================
        ' Visual Studio's out-of-process designer (.NET projects) never loads AquaTabControlDesigner --
        ' it is only used by the in-process one (.NET Framework projects) -- and no designer passes the
        ' click on to OnMouseDown. Nor does every click change the designer's selection (a second
        ' click on the already selected control doesn't), so at design time this control watches the
        ' left button itself: a short timer notices it going down over one of its tab heads (and not
        ' over some other window covering the designer) and shows that page. The control stays
        ' selected, as a stock TabControl does.
        ' Selecting a control that sits on another page (Document Outline, the property grid's list)
        ' brings that page to the front (the designer's selection service).
        Private _selService As System.ComponentModel.Design.ISelectionService
        Private _designTimer As Timer
        Private _designMouseWasDown As Boolean

        <Runtime.InteropServices.DllImport("user32.dll")>
        Private Shared Function GetAsyncKeyState(ByVal vKey As Integer) As Short
        End Function
        <Runtime.InteropServices.DllImport("user32.dll")>
        Private Shared Function GetSystemMetrics(ByVal nIndex As Integer) As Integer
        End Function
        <Runtime.InteropServices.DllImport("user32.dll")>
        Private Shared Function WindowFromPoint(ByVal pt As Point) As IntPtr
        End Function
        <Runtime.InteropServices.DllImport("user32.dll")>
        Private Shared Function GetWindowThreadProcessId(ByVal hWnd As IntPtr, ByRef processId As Integer) As Integer
        End Function

        Private Sub StartDesignMouseWatch()
            If _designTimer IsNot Nothing Then Return
            _designTimer = New Timer With {.Interval = 30}
            AddHandler _designTimer.Tick, AddressOf OnDesignTimerTick
            _designTimer.Start()
        End Sub

        Private Sub StopDesignMouseWatch()
            If _designTimer Is Nothing Then Return
            _designTimer.Stop()
            RemoveHandler _designTimer.Tick, AddressOf OnDesignTimerTick
            _designTimer.Dispose()
            _designTimer = Nothing
        End Sub

        Private Sub OnDesignTimerTick(sender As Object, e As EventArgs)
            Const VK_LBUTTON As Integer = &H1, VK_RBUTTON As Integer = &H2, SM_SWAPBUTTON As Integer = 23
            Dim vk As Integer = If(GetSystemMetrics(SM_SWAPBUTTON) <> 0, VK_RBUTTON, VK_LBUTTON)   ' the primary button
            Dim down As Boolean = (GetAsyncKeyState(vk) And &H8000) <> 0
            If down AndAlso Not _designMouseWasDown AndAlso IsHandleCreated AndAlso Visible Then
                Dim scr As Point = Control.MousePosition
                Dim pt As Point = PointToClient(scr)
                If IsOnHeader(pt) AndAlso NotCovered(WindowFromPoint(scr)) Then SelectTabAt(pt)
            End If
            _designMouseWasDown = down
        End Sub

        Private Const GA_ROOT As Integer = 2
        <Runtime.InteropServices.DllImport("user32.dll")>
        Private Shared Function GetAncestor(ByVal hWnd As IntPtr, ByVal flags As Integer) As IntPtr
        End Function

        ''' <summary>The window under the mouse belongs to the designer (this process, or the same top-level
        ''' window as this control -- in Visual Studio's out-of-process designer the surface sits in a
        ''' Visual Studio window), not to some other application lying over it.</summary>
        Private Function NotCovered(ByVal under As IntPtr) As Boolean
            If under = IntPtr.Zero Then Return False
            Dim pid As Integer
            GetWindowThreadProcessId(under, pid)
            If pid = Diagnostics.Process.GetCurrentProcess().Id Then Return True
            Return GetAncestor(under, GA_ROOT) = GetAncestor(Handle, GA_ROOT)
        End Function

        Public Overrides Property Site As ISite
            Get
                Return MyBase.Site
            End Get
            Set(value As ISite)
                If _selService IsNot Nothing Then
                    RemoveHandler _selService.SelectionChanged, AddressOf OnDesignSelectionChanged
                    _selService = Nothing
                End If
                StopDesignMouseWatch()
                MyBase.Site = value
                If value IsNot Nothing AndAlso value.DesignMode Then
                    _selService = TryCast(value.GetService(GetType(System.ComponentModel.Design.ISelectionService)), System.ComponentModel.Design.ISelectionService)
                    If _selService IsNot Nothing Then AddHandler _selService.SelectionChanged, AddressOf OnDesignSelectionChanged
                    StartDesignMouseWatch()
                End If
            End Set
        End Property

        Private Sub OnDesignSelectionChanged(sender As Object, e As EventArgs)
            Dim svc As System.ComponentModel.Design.ISelectionService = _selService
            If svc Is Nothing OrElse IsDisposed OrElse Not IsHandleCreated Then Return
            Dim primary As Control = TryCast(svc.PrimarySelection, Control)
            If primary Is Nothing Then Return

            If primary Is Me Then Return   ' clicks on the tab heads: OnDesignTimerTick

            ' a control on one of the pages: show that page
            Dim c As Control = primary
            While c IsNot Nothing AndAlso c.Parent IsNot Me
                c = c.Parent
            End While
            Dim owner As TabPage = TryCast(c, TabPage)
            If owner IsNot Nothing AndAlso _tabPages.Contains(owner) AndAlso owner IsNot SelectedTab Then
                SelectedTab = owner
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                StopDesignMouseWatch()
                If _selService IsNot Nothing Then
                    RemoveHandler _selService.SelectionChanged, AddressOf OnDesignSelectionChanged
                    _selService = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Invalidate()
        End Sub

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const WS_EX_COMPOSITED As Integer = &H2000000
                Dim cp As CreateParams = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or WS_EX_COMPOSITED
                Return cp
            End Get
        End Property

    End Class

End Namespace
