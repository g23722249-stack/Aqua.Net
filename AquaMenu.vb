Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

' Port of the VB6 Aqua.AquaMenu UserControl (Control\AquaMenu.ctl): a standalone popup (context)
' menu. VB6 made it InvisibleAtRuntime -- it only held a MenuItem tree and popped it up with
' ShowMenu(x, y) -- so here it is a Component (sits in the designer's component tray) rather than
' a Control. The popup itself is the same Internal\MenuPopupForm that AquaForm's menu bar and
' DropDownList already use, so it looks and behaves identically (icons, check marks, "-"
' separators, cascading sub-menus).
'
' Defining the menu: VB6 persisted it as flat designer properties (MenuName_i / MenuText_i /
' MenuLevel_i / MenuIcon_i / MenuSelIcon_i ...) and rebuilt the tree from the levels on load
' (LibMenu.RestoreMultiLevelMenu). Items is that same flat list -- editable in the designer's
' collection editor and written into InitializeComponent -- and the tree is rebuilt from the levels
' whenever it changes. AddItem appends to it, so code-built menus work the same way. The
' hWnd/MenuhWnd_i values were only random ids for VB6's own bookkeeping and have no equivalent here.
Namespace Global.Aqua

    ''' <summary>A flat, ordered MenuItem list (AquaMenu.Items, DropDownList.Items); every change
    ''' calls back so the owner rebuilds its menu tree.</summary>
    Public Class MenuItemCollection
        Inherits System.Collections.ObjectModel.Collection(Of MenuItem)

        Private ReadOnly _changed As Action

        Friend Sub New(ByVal changed As Action)
            _changed = changed
        End Sub

        Protected Overrides Sub InsertItem(ByVal index As Integer, ByVal item As MenuItem)
            MyBase.InsertItem(index, If(item, New MenuItem()))
            _changed()
        End Sub

        Protected Overrides Sub RemoveItem(ByVal index As Integer)
            MyBase.RemoveItem(index)
            _changed()
        End Sub

        Protected Overrides Sub SetItem(ByVal index As Integer, ByVal item As MenuItem)
            MyBase.SetItem(index, If(item, New MenuItem()))
            _changed()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _changed()
        End Sub
    End Class

    <DefaultEvent("MenuSelected")>
    Public Class AquaMenu
        Inherits Component

        ' SystemFonts.MenuFont hands out a new Font on every call -- read it once.
        Private Shared ReadOnly DefaultMenuFont As Font = SystemFonts.MenuFont

        Private _root As New MenuItem()
        Private ReadOnly _items As MenuItemCollection
        Private _selectedMenu As MenuItem
        Private _popup As MenuPopupForm
        Private _font As Font = Nothing

        ''' <summary>Raised just before the popup opens (VB6: MenuOpen) -- update Enabled/Visible here.</summary>
        Public Event MenuOpen(sender As Object, e As EventArgs)
        ''' <summary>Raised when the popup closes, whether by a pick, CloseMenu or clicking elsewhere (VB6: MenuClose).</summary>
        Public Event MenuClose(sender As Object, e As EventArgs)
        ''' <summary>Raised when an item without a sub-menu is clicked; the popup is already closed (VB6: MenuSelected).</summary>
        Public Event MenuSelected(sender As Object, item As MenuItem)

        Public Sub New()
            _items = New MenuItemCollection(AddressOf RebuildTree)
        End Sub

        Public Sub New(ByVal container As IContainer)
            Me.New()
            If container IsNot Nothing Then container.Add(Me)
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        ''' <summary>Font of the popup items. Nothing = the system menu font.</summary>
        <Category("外觀")>
        Public Property Font As Font
            Get
                Return If(_font, DefaultMenuFont)
            End Get
            Set(value As Font)
                _font = value
            End Set
        End Property

        Private Function ShouldSerializeFont() As Boolean
            Return _font IsNot Nothing
        End Function

        Private Sub ResetFont()
            _font = Nothing
        End Sub

        ''' <summary>Every item in menu order, each with its Level (1 = first level, 2 = under the
        ''' previous level-1 item ...) -- the designer's way of defining the menu.</summary>
        <Category("行為"), Description("選單項目（依序排列；Level 決定層級，Text 為「-」是分隔線）。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Content)>
        <MergableProperty(False)>
        <Editor(GetType(System.ComponentModel.Design.CollectionEditor), GetType(System.Drawing.Design.UITypeEditor))>
        Public ReadOnly Property Items As MenuItemCollection
            Get
                Return _items
            End Get
        End Property

        ''' <summary>Rebuilds the tree from Items (VB6 LibMenu.RestoreMultiLevelMenu): a level that
        ''' skips ahead is clamped to one below the previous item.</summary>
        Friend Sub RebuildTree()
            CloseMenu()
            _root.Clear()
            For Each item As MenuItem In _items
                item.Clear()
                Dim parent As MenuItem = _root
                For l = 2 To Math.Max(1, item.Level)
                    If parent.Count = 0 Then Exit For
                    parent = parent(parent.Count - 1)
                Next
                parent.AddItem(item)   ' also sets item.Level to its real depth
            Next
        End Sub

        ''' <summary>The root of the menu tree; its children are the first-level items (VB6: Menu).</summary>
        <Browsable(False)>
        Public ReadOnly Property Menu As MenuItem
            Get
                Return _root
            End Get
        End Property

        ''' <summary>The item picked last (VB6: SelectedMenu).</summary>
        <Browsable(False)>
        Public ReadOnly Property SelectedMenu As MenuItem
            Get
                Return _selectedMenu
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _root.Count
            End Get
        End Property

        ''' <summary>First-level item by position; Nothing when out of range (VB6: Item(index)).</summary>
        <Browsable(False)>
        Default Public ReadOnly Property Item(ByVal index As Integer) As MenuItem
            Get
                If index < 0 OrElse index >= _root.Count Then Return Nothing
                Return _root(index)
            End Get
        End Property

        ''' <summary>First-level item by Name, case-insensitive; Nothing when not found (VB6: Item(name)).
        ''' Use FindMenu to search sub-menus too.</summary>
        <Browsable(False)>
        Default Public ReadOnly Property Item(ByVal name As String) As MenuItem
            Get
                If name Is Nothing OrElse name.Trim().Length = 0 Then Return Nothing
                For i = 0 To _root.Count - 1
                    If String.Equals(_root(i).Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) Then Return _root(i)
                Next
                Return Nothing
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property IsOpen As Boolean
            Get
                Return _popup IsNot Nothing
            End Get
        End Property

        '=====================================================================
        ' Building the menu
        '=====================================================================
        ''' <summary>Appends one item the way VB6's designer properties described it: level 1 is a
        ''' first-level item, level 2 goes under the most recent level-1 item, and so on (a level that
        ''' skips ahead is clamped to one below the previous item). Text "-" makes a separator.</summary>
        Public Function AddItem(ByVal name As String, ByVal text As String, Optional ByVal level As Integer = 1,
                                Optional ByVal icon As Image = Nothing, Optional ByVal selIcon As Image = Nothing) As MenuItem
            Dim item As New MenuItem(name, text, Math.Max(1, level), icon, selIcon)
            _items.Add(item)
            Return item
        End Function

        ''' <summary>Replaces the whole menu with this tree (VB6: AddMenu); Items becomes its flattened list.</summary>
        Public Function AddMenu(ByVal mainMenu As MenuItem) As MenuItem
            Dim flat As New List(Of MenuItem)
            If mainMenu IsNot Nothing Then Flatten(mainMenu, 1, flat)
            _items.Clear()
            For Each m As MenuItem In flat
                _items.Add(m)
            Next
            Return _root
        End Function

        Private Shared Sub Flatten(ByVal parent As MenuItem, ByVal level As Integer, ByVal into As List(Of MenuItem))
            For i = 0 To parent.Count - 1
                Dim m As MenuItem = parent(i)
                m.Level = level
                into.Add(m)
                Flatten(m, level + 1, into)
            Next
        End Sub

        ''' <summary>Removes every item (VB6: ClearMenu).</summary>
        Public Sub ClearMenu()
            _items.Clear()
            _selectedMenu = Nothing
        End Sub

        ''' <summary>Item with this Name anywhere in the tree (depth-first), or Nothing.</summary>
        Public Function FindMenu(ByVal name As String) As MenuItem
            If name Is Nothing Then Return Nothing
            Return FindIn(_root, name.Trim())
        End Function

        Private Shared Function FindIn(ByVal parent As MenuItem, ByVal name As String) As MenuItem
            For i = 0 To parent.Count - 1
                Dim m As MenuItem = parent(i)
                If String.Equals(m.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) Then Return m
                Dim found As MenuItem = FindIn(m, name)
                If found IsNot Nothing Then Return found
            Next
            Return Nothing
        End Function

        '=====================================================================
        ' Showing / closing (ports of ShowMenu / CreateMenu / CloseMenu / m_lpMenu_ItemClick)
        '=====================================================================
        ''' <summary>Pops the menu up with its top-left at (left, top) in screen pixels; with both
        ''' omitted (negative) it opens at the mouse pointer, as in VB6. Does nothing while the menu has
        ''' no items.</summary>
        Public Sub ShowMenu(Optional ByVal left As Integer = -1, Optional ByVal top As Integer = -1)
            If _root.Count <= 0 Then Return
            If left < 0 AndAlso top <= 0 Then
                Dim p As Point = Cursor.Position
                left = p.X : top = p.Y
            End If
            CloseMenu()
            RaiseEvent MenuOpen(Me, EventArgs.Empty)   ' VB6 raised it before building the popup
            If _root.Count <= 0 Then Return             ' a MenuOpen handler may have cleared it
            _popup = New MenuPopupForm()
            AddHandler _popup.ItemClicked, AddressOf OnPopupItemClicked
            AddHandler _popup.AutoClosed, AddressOf OnPopupAutoClosed
            _popup.ShowFor(_root, New Point(left, top), Font)
        End Sub

        ''' <summary>Pops the menu up at a point given in <paramref name="control"/>'s client coordinates.</summary>
        Public Sub ShowMenu(ByVal control As Control, ByVal location As Point)
            If control Is Nothing Then
                ShowMenu()
            Else
                Dim p As Point = control.PointToScreen(location)
                ShowMenu(p.X, p.Y)
            End If
        End Sub

        ''' <summary>Closes the popup if it is open (VB6: CloseMenu); raises MenuClose only then.</summary>
        Public Sub CloseMenu()
            If _popup Is Nothing Then Return
            If Not _popup.IsDisposed Then
                _popup.CloseAll()
                _popup.Dispose()
            End If
            _popup = Nothing
            RaiseEvent MenuClose(Me, EventArgs.Empty)
        End Sub

        Private Sub OnPopupItemClicked(ByVal item As MenuItem)
            CloseMenu()
            _selectedMenu = item
            RaiseEvent MenuSelected(Me, item)
        End Sub

        Private Sub OnPopupAutoClosed(sender As Object, e As EventArgs)
            CloseMenu()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then CloseMenu()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
