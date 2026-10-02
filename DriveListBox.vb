Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

' Port of the VB6 Aqua.DriveListBox UserControl (Control\DriveListBox.ctl): a themed tree of
' the machine's drives. Composed the same way as EditBox.vb -- a real child TreeView hosts the
' actual list, Aqua chrome (border/region/focus glow) drawn around it.
'
' Simplifications vs the VB6 original: drive/folder icons come from the live Windows shell
' (Internal\ShellIcon.vb, SHGetFileInfo) instead of a fixed hand-built icon set, so they always
' match the user's actual theme; and the VB6 "Network Neighborhood" branch (enumerated via the
' Carbon.NetResource COM object, a custom in-house networking wrapper with no .NET equivalent)
' is not ported -- DriveMode still supports every VB6 mode, just without that one legacy branch.
'
' VB6's separate DeskTop.ctl (a dedicated TreeView control whose tree started at a "桌面" root
' wrapping My Computer/My Document/Network) was never ported as its own class -- ShowDesktop below
' absorbs the one piece of it that still pulls its weight, a "桌面" entry point above the drives.
' The rest of DeskTop.ctl's tree (My Document's subfolders) has its equivalent in DirListBox's own
' ShowSpecialFolders quick-access set.
Namespace Global.Aqua

    <DefaultEvent("SelectedChanged")>
    Public Class DriveListBox
        Inherits UserControl

        Private Const BorderInset As Integer = 4

        Private ReadOnly _tree As New TreeView()
        Private ReadOnly _images As New ImageList()

        Private _driveMode As ListDriveMode = ListDriveMode.All
        Private _showDesktop As Boolean = True
        Private _obtuseness As ObtusenessMode = ObtusenessMode.None
        Private _borderColor As Color = ColorUtil.OleToColor(12434877)
        Private _borderFocusColor As Color = GridConst.BorderFocusColor
        Private _path As String = ""
        Private _focused As Boolean = False

        Public Event SelectedChanged(sender As Object, e As EventArgs)
        Public Event ObtusenessChanged(sender As Object, e As EventArgs)
        Public Event BorderColorChanged(sender As Object, e As EventArgs)
        Public Event BorderFocusColorChanged(sender As Object, e As EventArgs)
        Public Event ListDriveModeChanged(sender As Object, e As EventArgs)
        Public Event ShowDesktopChanged(sender As Object, e As EventArgs)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.ContainerControl, True)
            TabStop = False
            MyBase.BackColor = SystemColors.Window

            _images.ImageSize = New Size(16, 16)
            _tree.ImageList = _images
            _tree.BorderStyle = BorderStyle.None
            _tree.TabStop = True
            Controls.Add(_tree)

            AddHandler _tree.AfterSelect, AddressOf OnAfterSelect
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
            PopulateDrives()
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
        <DefaultValue(ListDriveMode.All)>
        Public Property DriveMode As ListDriveMode
            Get
                Return _driveMode
            End Get
            Set(value As ListDriveMode)
                If _driveMode = value Then Return
                _driveMode = value
                PopulateDrives()
                RaiseEvent ListDriveModeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Whether a "桌面" (Desktop) node is listed above the drives -- ported from
        ''' DeskTop.ctl's dedicated Desktop tree root, which DriveListBox absorbs instead of that
        ''' control being ported separately (see Internal\SpecialFolders.vb).</summary>
        <Category("行為")>
        <DefaultValue(True)>
        Public Property ShowDesktop As Boolean
            Get
                Return _showDesktop
            End Get
            Set(value As Boolean)
                If _showDesktop = value Then Return
                _showDesktop = value
                PopulateDrives()
                RaiseEvent ShowDesktopChanged(Me, EventArgs.Empty)
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

        ''' <summary>Selected drive's root path, e.g. "C:\" (VB6: Path, read-only).</summary>
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
        ' Drive enumeration (port of MoveTreeNodeToScreen)
        '=====================================================================
        Private Sub PopulateDrives()
            _tree.Nodes.Clear()
            If _showDesktop Then AddDesktopNode()
            For Each di As DriveInfo In DriveInfo.GetDrives()
                If Not Supports(di.DriveType) Then Continue For
                Dim key As String = di.Name
                If Not _images.Images.ContainsKey(key) Then
                    Dim icon As Image = ShellIcon.GetIcon(di.Name)
                    If icon IsNot Nothing Then _images.Images.Add(key, icon)
                End If
                Dim label As String = di.Name
                Try
                    If di.IsReady AndAlso Not String.IsNullOrEmpty(di.VolumeLabel) Then
                        label = $"{di.VolumeLabel} ({di.Name.TrimEnd("\"c)})"
                    End If
                Catch
                End Try
                Dim node As New TreeNode(label) With {.Name = di.Name, .Tag = di.Name}
                If _images.Images.ContainsKey(key) Then
                    node.ImageKey = key : node.SelectedImageKey = key
                End If
                _tree.Nodes.Add(node)
            Next
            If _tree.Nodes.Count > 0 Then _tree.SelectedNode = _tree.Nodes(0)
        End Sub

        Private Sub AddDesktopNode()
            Dim path As String = SpecialFolders.GetPath(SpecialFolders.QuickAccessFolder.Desktop)
            If Not _images.Images.ContainsKey(path) Then
                Dim icon As Image = ShellIcon.GetIcon(path)
                If icon IsNot Nothing Then _images.Images.Add(path, icon)
            End If
            Dim node As New TreeNode(SpecialFolders.GetDisplayName(SpecialFolders.QuickAccessFolder.Desktop)) With {.Name = path, .Tag = path}
            If _images.Images.ContainsKey(path) Then
                node.ImageKey = path : node.SelectedImageKey = path
            End If
            _tree.Nodes.Add(node)
        End Sub

        Private Function Supports(ByVal t As DriveType) As Boolean
            Select Case _driveMode
                Case ListDriveMode.FixedOnly : Return t = DriveType.Fixed OrElse t = DriveType.Ram
                Case ListDriveMode.NetworkOnly : Return t = DriveType.Network
                Case ListDriveMode.RemovableOnly : Return t = DriveType.Removable OrElse t = DriveType.CDRom
                Case ListDriveMode.NetworkNeighborhoodOnly : Return False   ' not ported, see file header
                Case Else : Return t <> DriveType.Unknown   ' All / AllWithNetworkNeighborhood
            End Select
        End Function

        Private Sub OnAfterSelect(sender As Object, e As TreeViewEventArgs)
            Dim newPath As String = If(TryCast(e.Node?.Tag, String), "")
            If newPath = _path Then Return
            _path = newPath
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End Sub

        '=====================================================================
        ' Layout / region / painting (ports of RegionUserControl / SetUserControlPosition / DrawControlBorder)
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
