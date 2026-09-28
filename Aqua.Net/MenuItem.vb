Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing

' Port of the VB6 Aqua.MenuItem class (CoClass\MenuItem.cls): a recursive menu-tree node used by
' AquaForm's main menu bar and its dropdown popups. Simpler than the VB6 original -- VB6 had no
' generic collection type, so it hand-rolled AddItem/Insert/Delete/Copy over a resizable array;
' here that's just a List(Of MenuItem).
' AquaMenu.Items holds MenuItems as a flat list edited in the designer (Name / Text / Level / Icon /
' SelIcon, like VB6's MenuName_i ... properties); MenuItemConverter writes each one as
' "New MenuItem(name, text, level, icon, selIcon)".
Namespace Global.Aqua

    <TypeConverter(GetType(MenuItemConverter))>
    Public Class MenuItem

        Private ReadOnly _items As New List(Of MenuItem)()

        Public Sub New()
        End Sub

        Public Sub New(ByVal text As String)
            Me.Text = text
        End Sub

        ''' <summary>One entry as the designer (and VB6's MenuName_i / MenuText_i / MenuLevel_i /
        ''' MenuIcon_i / MenuSelIcon_i) describes it.</summary>
        Public Sub New(ByVal name As String, ByVal text As String, ByVal level As Integer, ByVal icon As Image, ByVal selIcon As Image)
            Me.Name = If(name, "")
            Me.Text = If(text, "")
            Me.Level = level
            Me.Icon = icon
            Me.SelIcon = selIcon
        End Sub

        <Category("設計"), Description("程式用來辨認選項的名稱（VB6 MenuName）。")>
        Public Property Name As String = ""
        <Category("外觀"), Description("顯示的文字；「-」是分隔線。")>
        Public Property Text As String = ""
        <Browsable(False)>
        Public Property Tag As String = ""
        <Category("行為"), Description("層級：1 = 第一層，2 = 上一個第 1 層選項的子選單，依此類推。")>
        Public Property Level As Integer = 0
        <Category("外觀")>
        Public Property Icon As Image
        <Category("外觀"), Description("滑鼠移到選項上時的圖示。")>
        Public Property SelIcon As Image
        <Browsable(False)>
        Public Property Enabled As Boolean = True
        <Browsable(False)>
        Public Property Visible As Boolean = True
        <Browsable(False)>
        Public Property Color As ColorConstants = ColorConstants.Blue
        <Browsable(False)>
        Public Property CheckStyle As ItemCheckStyle = ItemCheckStyle.None

        Private _checked As Boolean = False
        <Browsable(False)>
        Public Property Checked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If _checked = value Then Return
                _checked = value
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private _selected As Boolean = False
        ''' <summary>True while the dropdown popup is hovering this item. Internal render state,
        ''' not something application code normally sets.</summary>
        <Browsable(False)>
        Public Property Selected As Boolean
            Get
                Return _selected
            End Get
            Set(value As Boolean)
                If _selected = value Then Return
                _selected = value
                RaiseEvent SelectedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Event CheckedChanged(sender As Object, e As EventArgs)
        Public Event SelectedChanged(sender As Object, e As EventArgs)

        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _items.Count
            End Get
        End Property

        ''' <summary>How the designer's collection editor lists it: indented by level, "-" as a line.</summary>
        Public Overrides Function ToString() As String
            Dim caption As String = If(Text = "-", "────", Text)
            Return New String(" "c, Math.Max(0, Level - 1) * 4) & caption & If(Name <> "", "  (" & Name & ")", "")
        End Function

        Default Public ReadOnly Property Item(ByVal index As Integer) As MenuItem
            Get
                Return _items(index)
            End Get
        End Property

        Public Function AddItem(ByVal item As MenuItem) As MenuItem
            item.Level = Me.Level + 1
            _items.Add(item)
            Return item
        End Function

        Public Sub Insert(ByVal item As MenuItem, Optional ByVal index As Integer = -1)
            item.Level = Me.Level + 1
            If index < 0 OrElse index >= _items.Count Then
                _items.Add(item)
            Else
                _items.Insert(index, item)
            End If
        End Sub

        Public Sub Delete(ByVal index As Integer)
            If index < 0 OrElse index >= _items.Count Then Return
            _items.RemoveAt(index)
        End Sub

        Public Sub Clear()
            _items.Clear()
        End Sub

        Public Function Copy() As MenuItem
            Dim c As New MenuItem() With {
                .Name = Name, .Text = Text, .Tag = Tag, .Level = Level,
                .Icon = Icon, .SelIcon = SelIcon, .Enabled = Enabled, .Visible = Visible,
                .Color = Color, .Checked = Checked, .CheckStyle = CheckStyle
            }
            For Each child In _items
                c._items.Add(child.Copy())
            Next
            Return c
        End Function

    End Class

End Namespace
