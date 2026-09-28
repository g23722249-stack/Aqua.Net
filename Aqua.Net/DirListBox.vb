Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

' Port of the VB6 Aqua.DirListBox UserControl (Control\DirListBox.ctl): a themed folder tree
' rooted at a given path, expanded lazily. Composed like EditBox.vb/DriveListBox.vb -- a real
' child TreeView hosts the list, Aqua chrome drawn around it. Icons come from the live Windows
' shell (Internal\ShellIcon.vb) rather than a fixed bundled icon set.
'
' ShowSpecialFolders adds a Windows-Explorer-style "quick access" group (桌面/下載/文件/圖片/音樂/影片,
' and "本機" with the drives, as VB6's DeskTop tree had 我的電腦;
' Internal\SpecialFolders.vb) above the Root tree -- this is also where VB6's separate DeskTop.ctl
' ended up: that control was never ported as its own class, since DirListBox already had the
' tree/lazy-expand machinery its "well-known shell folder" shortcut idea needed.
Namespace Global.Aqua

    <DefaultEvent("SelectedChanged")>
    Public Class DirListBox
        Inherits UserControl

        Private Const BorderInset As Integer = 4
        Private Const DummyNodeName As String = "$dummy$"

        Private ReadOnly _tree As New TreeView()
        Private ReadOnly _images As New ImageList()

        Private _root As String = Application.StartupPath
        Private _path As String = ""
        Private _showSpecialFolders As Boolean = True
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _focused As Boolean = False

        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event RootChanged(sender As Object, e As EventArgs)
        Public Event ShowSpecialFoldersChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            MyBase.BackColor = SystemColors.Window

            _images.ImageSize = New Size(16, 16)
            Dim folderIcon As Image = ShellIcon.GetFolderIcon()
            If folderIcon IsNot Nothing Then _images.Images.Add("folder", folderIcon)
            _tree.ImageList = _images
            _tree.BorderStyle = BorderStyle.None
            _tree.TabStop = True
            Controls.Add(_tree)

            AddHandler _tree.AfterSelect, AddressOf OnAfterSelect
            AddHandler _tree.BeforeExpand, AddressOf OnBeforeExpand
            AddHandler _tree.Enter, AddressOf OnTreeEnter
            AddHandler _tree.Leave, AddressOf OnTreeLeave
            AddHandler _tree.Click, Sub(sender, e) MyBase.OnClick(e)
            AddHandler _tree.DoubleClick, Sub(sender, e) MyBase.OnDoubleClick(e)
            AddHandler _tree.KeyDown, Sub(sender, e) MyBase.OnKeyDown(e)
            AddHandler _tree.KeyPress, Sub(sender, e) MyBase.OnKeyPress(e)
            AddHandler _tree.KeyUp, Sub(sender, e) MyBase.OnKeyUp(e)
            AddHandler _tree.MouseDown, Sub(sender, e) MyBase.OnMouseDown(CType(e, MouseEventArgs))
            AddHandler _tree.MouseMove, Sub(sender, e) MyBase.OnMouseMove(CType(e, MouseEventArgs))
            AddHandler _tree.MouseUp, Sub(sender, e) MyBase.OnMouseUp(CType(e, MouseEventArgs))

            _tree.Font = Font
            UpdateRegion()
            LayoutChildren()
            PopulateRoot()
        End Sub

        Protected Overrides ReadOnly Property DefaultSize As Size
            Get
                Return New Size(180, 220)
            End Get
        End Property

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateRegion()
        End Sub

        '=====================================================================
        ' Properties
        '=====================================================================
        <Category("行為")>
        Public Property Root As String
            Get
                Return _root
            End Get
            Set(value As String)
                If _root = value Then Return
                _root = value
                PopulateRoot()
                RaiseEvent RootChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ' Root defaults to the program's folder: only a different root is written into the .Designer.vb
        ' (the designer used to save its own temp folder as the root).
        Private Function ShouldSerializeRoot() As Boolean
            Return Not String.Equals(_root, Application.StartupPath, StringComparison.OrdinalIgnoreCase)
        End Function

        Private Sub ResetRoot()
            Root = Application.StartupPath
        End Sub

        ''' <summary>Whether a Windows-Explorer-style "quick access" group (桌面/下載/文件/圖片/音樂/影片)
        ''' is listed above the Root tree -- see Internal\SpecialFolders.vb and the file header.</summary>
        <Category("行為")>
        <DefaultValue(True)>
        Public Property ShowSpecialFolders As Boolean
            Get
                Return _showSpecialFolders
            End Get
            Set(value As Boolean)
                If _showSpecialFolders = value Then Return
                _showSpecialFolders = value
                PopulateRoot()
                RaiseEvent ShowSpecialFoldersChanged(Me, EventArgs.Empty)
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

        ''' <summary>Selected folder's full path (VB6: Path, read-only).</summary>
        <Browsable(False)>
        Public ReadOnly Property Path As String
            Get
                Return _path
            End Get
        End Property

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            _tree.Font = Font
            LayoutChildren()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            _tree.Enabled = Enabled
            Invalidate()
        End Sub

        Public Shadows Function Focus() As Boolean
            Return _tree.Focus()
        End Function

        '=====================================================================
        ' Folder enumeration (ports of MoveTreeNodeToScreen / NodeExpand, made lazy via BeforeExpand)
        '=====================================================================
        Private Sub PopulateRoot()
            _tree.Nodes.Clear()
            If _showSpecialFolders Then
                AddSpecialFolderNodes()
                AddDriveNodes()
            End If
            Try
                If Not Directory.Exists(_root) Then Return
                Dim rootNode As New TreeNode(GetDisplayName(_root)) With {.Tag = _root, .ImageKey = "folder", .SelectedImageKey = "folder"}
                AddDummyIfHasSubfolders(rootNode, _root)
                _tree.Nodes.Add(rootNode)
                rootNode.Expand()
                _tree.SelectedNode = rootNode
                _path = _root
            Catch
                Dim errNode As New TreeNode("(磁區或資料夾不存在)")
                _tree.Nodes.Add(errNode)
            End Try
        End Sub

        ''' <summary>Explorer-style quick-access group (桌面/下載/文件/圖片/音樂/影片), each node acting as
        ''' its own lazily-expandable root exactly like the Root node below -- OnBeforeExpand doesn't
        ''' care which node it's expanding, only the path in its Tag.</summary>
        Private Sub AddSpecialFolderNodes()
            For Each folder As SpecialFolders.QuickAccessFolder In SpecialFolders.AllQuickAccessFolders()
                Dim path As String = SpecialFolders.GetPath(folder)
                Try
                    If Not Directory.Exists(path) Then Continue For
                Catch
                    Continue For
                End Try

                If Not _images.Images.ContainsKey(path) Then
                    Dim icon As Image = ShellIcon.GetIcon(path)
                    If icon IsNot Nothing Then _images.Images.Add(path, icon)
                End If
                Dim imageKey As String = If(_images.Images.ContainsKey(path), path, "folder")

                Dim node As New TreeNode(SpecialFolders.GetDisplayName(folder)) With {.Tag = path, .ImageKey = imageKey, .SelectedImageKey = imageKey}
                AddDummyIfHasSubfolders(node, path)
                _tree.Nodes.Add(node)
            Next
        End Sub

        ''' <summary>"本機" with every ready drive (fixed, removable -- the camera's memory card --, CD,
        ''' network), as VB6's DeskTop tree listed them under 我的電腦; without it nothing outside the
        ''' user's own folders could be reached. The node itself has no path; the drives expand lazily.</summary>
        Private Sub AddDriveNodes()
            Dim drives As New List(Of TreeNode)
            For Each d As DriveInfo In DriveInfo.GetDrives()
                Try
                    If Not d.IsReady Then Continue For
                    Dim root As String = d.RootDirectory.FullName
                    Dim letter As String = root.TrimEnd("\"c)
                    Dim label As String = ""
                    Try
                        label = d.VolumeLabel
                    Catch
                    End Try
                    If String.IsNullOrEmpty(label) Then
                        Select Case d.DriveType
                            Case DriveType.Removable : label = "卸除式磁碟"
                            Case DriveType.CDRom : label = "光碟機"
                            Case DriveType.Network : label = "網路磁碟機"
                            Case Else : label = "本機磁碟"
                        End Select
                    End If
                    If Not _images.Images.ContainsKey(root) Then
                        Dim icon As Image = ShellIcon.GetIcon(root)
                        If icon IsNot Nothing Then _images.Images.Add(root, icon)
                    End If
                    Dim imageKey As String = If(_images.Images.ContainsKey(root), root, "folder")
                    Dim node As New TreeNode($"{label} ({letter})") With {.Tag = root, .ImageKey = imageKey, .SelectedImageKey = imageKey}
                    AddDummyIfHasSubfolders(node, root)
                    drives.Add(node)
                Catch
                    ' a drive that vanished or can't be read is just left out
                End Try
            Next
            If drives.Count = 0 Then Return
            Dim pcKey As String = drives(0).ImageKey
            Dim pc As New TreeNode("本機") With {.ImageKey = pcKey, .SelectedImageKey = pcKey}
            pc.Nodes.AddRange(drives.ToArray())
            _tree.Nodes.Add(pc)
            pc.Expand()
        End Sub

        Private Sub AddDummyIfHasSubfolders(ByVal node As TreeNode, ByVal path As String)
            Try
                If Directory.GetDirectories(path).Length > 0 Then
                    node.Nodes.Add(New TreeNode(DummyNodeName) With {.Name = DummyNodeName})
                End If
            Catch
            End Try
        End Sub

        Private Sub OnBeforeExpand(sender As Object, e As TreeViewCancelEventArgs)
            Dim node As TreeNode = e.Node
            If node.Nodes.Count <> 1 OrElse node.Nodes(0).Name <> DummyNodeName Then Return
            node.Nodes.Clear()
            Dim path As String = TryCast(node.Tag, String)
            If String.IsNullOrEmpty(path) Then Return
            Try
                For Each dir As String In Directory.GetDirectories(path)
                    Dim child As New TreeNode(System.IO.Path.GetFileName(dir)) With {.Tag = dir, .ImageKey = "folder", .SelectedImageKey = "folder"}
                    AddDummyIfHasSubfolders(child, dir)
                    node.Nodes.Add(child)
                Next
            Catch
            End Try
        End Sub

        Private Shared Function GetDisplayName(ByVal path As String) As String
            Dim name As String = System.IO.Path.GetFileName(path.TrimEnd("\"c))
            Return If(String.IsNullOrEmpty(name), path, name)
        End Function

        Private Sub OnAfterSelect(sender As Object, e As TreeViewEventArgs)
            Dim newPath As String = If(TryCast(e.Node?.Tag, String), "")
            If newPath = _path Then Return
            _path = newPath
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
            _tree.SetBounds(inset, inset, Math.Max(0, Width - inset * 2), Math.Max(0, Height - inset * 2))
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            BorderPainter.DrawThemedBorder(e.Graphics, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
        End Sub

        Private Sub OnTreeEnter(sender As Object, e As EventArgs)
            _focused = True
            Invalidate()
            MyBase.OnEnter(e)
        End Sub

        Private Sub OnTreeLeave(sender As Object, e As EventArgs)
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
