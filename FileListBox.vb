Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

' Port of the VB6 Aqua.FileListBox UserControl (Control\FileListBox.ctl): a themed flat file
' listing with per-type icons and optional checkboxes. VB6 actually (ab)used a non-hierarchical
' MSComctlLib.TreeView for this; a System.Windows.Forms.ListView (SmallIcon view) is the more
' natural .NET fit for a flat icon list and is what's used here, composed the same way as
' EditBox.vb/DriveListBox.vb. Per-file icons come from the live Windows shell
' (Internal\ShellIcon.vb) rather than the VB6 original's hand-assembled icon set.
Namespace Global.Aqua

    <DefaultEvent("SelectedChanged")>
    Public Class FileListBox
        Inherits UserControl

        Private Const BorderInset As Integer = 4

        Private ReadOnly _list As New ListView()
        Private ReadOnly _images As New ImageList()

        Private _path As String = Application.StartupPath
        Private _pattern As String = "*"
        Private _showExtensionName As Boolean = True
        Private _checkboxes As Boolean = False
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _focused As Boolean = False
        Private _files As New List(Of String)()

        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event PathChanged(sender As Object, e As EventArgs)
        Public Event PatternChanged(sender As Object, e As EventArgs)
        Public Event CheckboxesChanged(sender As Object, e As EventArgs)
        Public Event ShowExtensionNameChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            MyBase.BackColor = SystemColors.Window

            _images.ImageSize = New Size(16, 16)
            _list.View = View.SmallIcon
            _list.SmallImageList = _images
            _list.BorderStyle = BorderStyle.None
            _list.TabStop = True
            _list.MultiSelect = False
            _list.HideSelection = False
            Controls.Add(_list)

            AddHandler _list.SelectedIndexChanged, AddressOf OnSelectedIndexChanged
            AddHandler _list.Enter, AddressOf OnListEnter
            AddHandler _list.Leave, AddressOf OnListLeave
            AddHandler _list.Click, Sub(sender, e) MyBase.OnClick(e)
            AddHandler _list.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler _list.KeyDown, Sub(sender, e) MyBase.OnKeyDown(e)
            AddHandler _list.KeyPress, Sub(sender, e) MyBase.OnKeyPress(e)
            AddHandler _list.KeyUp, Sub(sender, e) MyBase.OnKeyUp(e)
            AddHandler _list.MouseDown, Sub(sender, e) MyBase.OnMouseDown(CType(e, MouseEventArgs))
            AddHandler _list.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler _list.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))

            _list.Font = Font
            UpdateRegion()
            LayoutChildren()
            RefreshFiles()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(200, 160)
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        ' Set at run time (the folder on show); written into a .Designer.vb it was the designer's own temp folder.
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Path As String
            Get
                Return _path
            End Get
            Set(value As String)
                Dim full As String = If(String.IsNullOrEmpty(value), _path, value)
                If _path = full Then Return
                If Not Directory.Exists(full) Then Return
                _path = full
                RefreshFiles()
                RaiseEvent PathChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Semicolon-separated wildcard patterns, e.g. "*.txt;*.log" (VB6: Pattern).</summary>
        <Category("行為")>
        <DefaultValue("*")>
        Public Property Pattern As String
            Get
                Return _pattern
            End Get
            Set(value As String)
                Dim p As String = If(String.IsNullOrEmpty(value) OrElse value.Trim().Length = 0, "*", value)   ' IsNullOrWhiteSpace needs .NET 4.0+; this project also targets net35
                If _pattern = p Then Return
                _pattern = p
                RefreshFiles()
                RaiseEvent PatternChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(True)>
        Public Property ShowExtensionName As Boolean
            Get
                Return _showExtensionName
            End Get
            Set(value As Boolean)
                If _showExtensionName = value Then Return
                _showExtensionName = value
                RefreshLabels()
                RaiseEvent ShowExtensionNameChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("行為")>
        <DefaultValue(False)>
        Public Property Checkboxes As Boolean
            Get
                Return _checkboxes
            End Get
            Set(value As Boolean)
                If _checkboxes = value Then Return
                _checkboxes = value
                _list.CheckBoxes = value
                RaiseEvent CheckboxesChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        <DefaultValue(ObtusenessMode.None)>
        Public Property Obtuseness As ObtusenessMode
            Get
                Return _obtuseness
            End Get
            Set(value As ObtusenessMode)
                If _obtuseness = value Then Return
                _obtuseness = value
                UpdateRegion()
                Invalidate()
                RaiseEvent ObtusenessChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                If _borderColor = value Then Return
                _borderColor = value
                Invalidate()
                RaiseEvent BorderColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Category("外觀")>
        Public Property BorderFocusColor As Color
            Get
                Return _borderFocusColor
            End Get
            Set(value As Color)
                If _borderFocusColor = value Then Return
                _borderFocusColor = value
                Invalidate()
                RaiseEvent BorderFocusColorChanged(Me, EventArgs.Empty)
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property Count As Integer
            Get
                Return _list.Items.Count
            End Get
        End Property

        ' Selection is read per item rather than via ListView.SelectedIndices: before the ListView's
        ' handle exists (e.g. Path/Selected set in Form_Load) SelectedIndices reports nothing selected,
        ' while each ListViewItem.Selected still holds the real state.
        <Browsable(False)>
        Public ReadOnly Property SelectedIndex As Integer
            Get
                For i As Integer = 0 To _list.Items.Count - 1
                    If _list.Items(i).Selected Then Return i
                Next
                Return -1
            End Get
        End Property

        <Browsable(False)>
        Public ReadOnly Property FileName As String
            Get
                Dim i As Integer = SelectedIndex
                Return If(i >= 0 AndAlso i < _files.Count, _files(i), "")
            End Get
        End Property

        ''' <summary>Selected file's name with extension, no folder (VB6: File -- the TreeView node Key,
        ''' used as Path &amp; "\" &amp; File). Blank when nothing is selected.</summary>
        <Browsable(False)>
        Public ReadOnly Property File As String
            Get
                Return System.IO.Path.GetFileName(FileName)
            End Get
        End Property

        ''' <summary>Whether the row at index is selected; setting True selects it (single-select list,
        ''' raises SelectedChanged) and scrolls it into view (VB6: Selected).</summary>
        Public Property Selected(ByVal index As Integer) As Boolean
            Get
                Return index >= 0 AndAlso index < _list.Items.Count AndAlso _list.Items(index).Selected
            End Get
            Set(value As Boolean)
                If index < 0 OrElse index >= _list.Items.Count Then Return
                Dim it As ListViewItem = _list.Items(index)
                If value Then
                    ' the ListView only enforces single-select once its handle exists
                    For Each other As ListViewItem In _list.Items
                        If other IsNot it AndAlso other.Selected Then other.Selected = False
                    Next
                End If
                it.Selected = value
                If value Then
                    it.Focused = True
                    it.EnsureVisible()
                End If
            End Set
        End Property

        <Browsable(False)>
        Public ReadOnly Property SelectedCount As Integer
            Get
                Dim n As Integer = 0
                For Each it As ListViewItem In _list.Items
                    If it.Selected Then n += 1
                Next
                Return n
            End Get
        End Property

        ''' <summary>Re-enumerates Path (VB6: Refresh re-ran MoveTreeNodeToScreen), so files added to
        ''' the current folder show up even though Path itself didn't change.</summary>
        Public Overrides Sub Refresh()
            RefreshFiles()
            MyBase.Refresh()
        End Sub

        Public Function Item(ByVal index As Integer) As String
            Return If(index >= 0 AndAlso index < _list.Items.Count, _list.Items(index).Text, "")
        End Function

        Public Property Checked(ByVal index As Integer) As Boolean
            Get
                Return index >= 0 AndAlso index < _list.Items.Count AndAlso _list.Items(index).Checked
            End Get
            Set(value As Boolean)
                If index >= 0 AndAlso index < _list.Items.Count Then _list.Items(index).Checked = value
            End Set
        End Property

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            _list.Font = Font
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _list.Enabled = Enabled
            Invalidate()
        End Sub

        Public Shadows Function Focus() As Boolean
            Return _list.Focus()
        End Function

        '=====================================================================
        ' File enumeration (port of MoveTreeNodeToScreen)
        '=====================================================================
        Private Sub RefreshFiles()
            _list.Items.Clear()
            _files.Clear()
            If Not Directory.Exists(_path) Then Return

            Dim results As New List(Of String)()
            For Each pat As String In _pattern.Split(";"c)
                Dim p As String = pat.Trim()
                If p.Length = 0 Then Continue For
                ' VB6 Pattern lists bare extensions ("Jpg;Bmp"); a wildcard or dotted entry is used as is
                If p.IndexOfAny(New Char() {"*"c, "?"c, "."c}) < 0 Then p = "*." & p
                Try
                    results.AddRange(Directory.GetFiles(_path, p))
                Catch
                End Try
            Next

            For Each f As String In results.Distinct().OrderBy(Function(x) x)
                Dim ext As String = System.IO.Path.GetExtension(f)
                Dim key As String = If(ext = "", "(none)", ext)
                If Not _images.Images.ContainsKey(key) Then
                    Dim icon As Image = ShellIcon.GetExtensionIcon(ext)
                    If icon IsNot Nothing Then _images.Images.Add(key, icon)
                End If
                Dim label As String = If(_showExtensionName, System.IO.Path.GetFileName(f), System.IO.Path.GetFileNameWithoutExtension(f))
                Dim item As New ListViewItem(label)
                If _images.Images.ContainsKey(key) Then item.ImageKey = key
                _list.Items.Add(item)
                _files.Add(f)
            Next

            If _list.Items.Count > 0 Then _list.Items(0).Selected = True
        End Sub

        Private Sub RefreshLabels()
            For i As Integer = 0 To _list.Items.Count - 1
                If i >= _files.Count Then Exit For
                _list.Items(i).Text = If(_showExtensionName, System.IO.Path.GetFileName(_files(i)), System.IO.Path.GetFileNameWithoutExtension(_files(i)))
            Next
        End Sub

        Private Sub OnSelectedIndexChanged(sender As Object, e As EventArgs)
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        '=====================================================================
        ' Layout / region / painting (same pattern as DriveListBox.vb)
        '=====================================================================
        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            UpdateRegion()
            LayoutChildren()
        End Sub

        Private Sub UpdateRegion()
            Dim old As Region = Me.Region
            Me.Region = If(_obtuseness = ObtusenessMode.None OrElse Width <= 0 OrElse Height <= 0,
                           Nothing,
                           RegionUtil.CreateObtusenessRegion(_obtuseness, Width, Height))
            If old IsNot Nothing Then old.Dispose()
            If Parent IsNot Nothing Then Parent.Invalidate(Bounds, True)
        End Sub

        Private Sub LayoutChildren()
            Dim inset As Integer = BorderInset
            _list.SetBounds(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2))
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            BorderPainter.DrawThemedBorder(e.Graphics, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
        End Sub

        Private Sub OnListEnter(sender As Object, e As EventArgs)
            _focused = True
            Invalidate()
            MyBase.OnEnter(e)
        End Sub

        Private Sub OnListLeave(sender As Object, e As EventArgs)
            _focused = False
            Invalidate()
            MyBase.OnLeave(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                Dim r As Region = Me.Region
                If r IsNot Nothing Then r.Dispose()
                _images.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
