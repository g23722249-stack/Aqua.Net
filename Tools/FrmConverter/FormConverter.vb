Imports System.ComponentModel
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Resources
Imports System.Text
Imports System.Windows.Forms

''' <summary>
''' Converts one parsed VB6 form into .Designer.vb + .resx + .Vb6.vb.
'''
''' Mapping rules (VB6 type -> .NET type, renamed properties, special cases) are all in this class; any
''' remaining property is set generically when the target type has a writable property of that name --
''' the value is converted from the property's real type via reflection (Boolean / number / String / Char /
''' enum / Color / Image / Icon / Font). Everything that can't be translated is reported as a TODO in the
''' generated .Vb6.vb and in the conversion report, never silently dropped (apart from GlobalSkip).
''' </summary>
Public Class FormConverter

    '======================================================================================
    ' Rules
    '======================================================================================
    ''' <summary>VB6 designer properties with no meaning in WinForms (units, OLE, VB6-only plumbing).</summary>
    Private Shared ReadOnly GlobalSkip As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "_ExtentX", "_ExtentY", "_Version", "Appearance", "ScaleMode", "ScaleWidth", "ScaleHeight", "ScaleLeft", "ScaleTop",
        "MouseIcon", "MousePointer", "OLEDropMode", "OLEDragMode", "LinkTopic", "LockControls", "SelText", "HelpContextID",
        "WhatsThisHelpID", "DragMode", "DragIcon", "AutoRedraw", "DrawStyle", "DrawWidth", "DrawMode", "ClientLeft", "ClientTop",
        "ClientWidth", "ClientHeight", "Left", "Top", "Width", "Height", "TabIndex", "ToolTipText", "HasDC", "ClipControls",
        "PaletteMode", "Palette", "RightToLeft", "NegotiateMenus", "NegotiateToolbars", "WhatsThisButton", "WhatsThisHelp", "Moveable"}

    ''' <summary>VB6 type -> .NET type used in the generated code.</summary>
    Private Shared ReadOnly TypeMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"VB.Label", "System.Windows.Forms.Label"},
        {"VB.TextBox", "System.Windows.Forms.TextBox"},
        {"VB.ListBox", "System.Windows.Forms.ListBox"},
        {"VB.Image", "System.Windows.Forms.PictureBox"},
        {"VB.Line", "System.Windows.Forms.Label"},
        {"VB.Shape", "System.Windows.Forms.Label"},
        {"VB.Timer", "System.Windows.Forms.Timer"},
        {"VB.DirListBox", "Aqua.DirListBox"},
        {"VB.FileListBox", "Aqua.FileListBox"},
        {"MSComctlLib.TreeView", "System.Windows.Forms.TreeView"},
        {"MSComctlLib.ListView", "System.Windows.Forms.ListView"},
        {"MSComctlLib.ImageList", "System.Windows.Forms.ImageList"},
        {"MSMask.MaskEdBox", "System.Windows.Forms.MaskedTextBox"},
        {"MediaPlayerCtl.MediaPlayer", "Aqua.MediaViewerControl"},
        {"Aqua.Button", "Aqua.FlashButton"},
        {"Aqua.Icon", "Aqua.IconBox"},
        {"Aqua.ListBox", "Aqua.ItemListBox"},
        {"Aqua.PictureBox", "Aqua.PicturePanel"},
        {"Aqua.DeskTop", "Aqua.DirListBox"},
        {"Aqua.MultiLineTextBox", "Aqua.TextBox"},
        {"Aqua.iTextBox", "Aqua.ITextBox"},
        {"Aqua.PageHead", "Aqua.TabControl"},
        {"Aqua.PageSheet", "Aqua.TabPage"}}

    ''' <summary>Components (no geometry, created with Me.components).</summary>
    Private Shared ReadOnly ComponentTypes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "VB.Timer", "MSComctlLib.ImageList", "Aqua.AquaMenu"}

    ''' <summary>Per-type renames VB6 name -> .NET name ("" = drop silently, it's handled elsewhere or meaningless).</summary>
    Private Shared ReadOnly Renames As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase) From {
        {"*", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Caption", "Text"}}},
        {"VB.Form", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"MaxButton", "MaximizeBox"}, {"MinButton", "MinimizeBox"}, {"Picture", "BackgroundImage"},
            {"BorderStyle", ""}, {"StartUpPosition", ""}, {"FillStyle", ""}, {"FillColor", ""}}},
        {"Aqua.AquaForm", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Title", "Text"}, {"Picture", "Image"}, {"TabStop", ""}, {"BorderStyle", "WindowBorderStyle"}}},
        {"Aqua.iForm", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Title", "Text"}, {"Picture", "Image"}, {"TabStop", ""}, {"BorderStyle", "WindowBorderStyle"}}},
        {"Aqua.PageSheet", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"Picture", ""}, {"SizeMode", ""}, {"BorderColor", ""}, {"BorderFocusColor", ""}, {"Selected", ""}}},   ' a TabPage paints the sheet texture itself
        {"VB.TextBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"MultiLine", "Multiline"}, {"Locked", "ReadOnly"}, {"Alignment", "TextAlign"}, {"BorderStyle", ""}}},
        {"VB.Label", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Alignment", ""}, {"BackStyle", ""}, {"WordWrap", ""}, {"UseMnemonic", "UseMnemonic"}}},
        {"VB.Image", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Picture", "Image"}, {"Stretch", ""}}},
        {"VB.PictureBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Picture", ""}, {"BorderStyle", ""}, {"AutoSize", ""}}},
        {"VB.Line", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"X1", ""}, {"X2", ""}, {"Y1", ""}, {"Y2", ""}, {"BorderColor", ""}, {"BorderWidth", ""}, {"BorderStyle", ""}}},
        {"VB.Shape", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Shape", ""}, {"BorderColor", ""}, {"BorderStyle", ""}, {"BorderWidth", ""}, {"FillColor", ""}, {"FillStyle", ""}, {"BackStyle", ""}, {"BackColor", ""}}},
        {"Aqua.Icon", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Picture", "Image"}}},
        {"Aqua.PictureBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Picture", "Image"}, {"AutoSize", ""}}},
        {"Aqua.iTextBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Picture", "Icon"}}},
        {"Aqua.CheckBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Value", "Checked"}}},
        {"Aqua.RadioButton", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Value", "Checked"}, {"Group", "GroupName"}}},
        {"Aqua.Slider", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Max", "Maximum"}, {"Min", "Minimum"}, {"SliderTickMode", "TickStyle"}}},
        {"Aqua.UpDown", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Max", "Maximum"}, {"Min", "Minimum"}}},
        {"Aqua.ProgressBar", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Max", "Maximum"}, {"Min", "Minimum"}}},
        {"Aqua.Buttons", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Count", ""}}},
        {"Aqua.ToolBar", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Count", ""}}},
        {"Aqua.Grid", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Count", ""}}},
        {"Aqua.PageHead", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"Count", ""}}},
        {"Aqua.AquaMenu", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"MenuCount", ""}}},
        {"Aqua.MediaList", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"MediaDragIcon", ""}}},
        {"Aqua.FileListBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"ImageHeight", ""}, {"ImageWidth", ""}}},
        {"Aqua.DeskTop", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"CDRom", ""}, {"Desktop", ""}, {"Domain", ""}, {"Fixed", ""}, {"Flash", ""}, {"FolderClose", ""}, {"FolderOpen", ""},
            {"ImageHeight", ""}, {"ImageWidth", ""}, {"MsNetwork", ""}, {"MyComputer", ""}, {"MyDocument", ""}, {"NetComputer", ""},
            {"NetShareFolder", ""}, {"NetworkNeighborhood", ""}, {"RamDisk", ""}, {"RemoteOffLine", ""}, {"RemoteOnLine", ""},
            {"Removable", ""}, {"Unknown", ""}, {"Zip", ""}}},
        {"MSComctlLib.TreeView", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"LabelEdit", ""}, {"Style", ""}, {"SingleSel", ""}}},
        {"MSComctlLib.ListView", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"LabelEdit", ""}, {"View", ""}, {"HideColumnHeaders", ""}, {"NumItems", ""}, {"AllowReorder", "AllowColumnReorder"}, {"LabelWrap", "LabelWrap"}}},
        {"MSComctlLib.ImageList", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"ImageWidth", ""}, {"ImageHeight", ""}, {"MaskColor", ""}, {"BackColor", ""}, {"UseMaskColor", ""}}},
        {"MSMask.MaskEdBox", New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"MaxLength", ""}}}}

    ''' <summary>Images whose magenta (FF00FF) background must become transparent because the target
    ''' draws them as-is (menus, the MediaList mark).</summary>
    Private Shared Function WantsMagentaTransparent(ByVal prop As String) As Boolean
        Return prop.StartsWith("MenuIcon_", StringComparison.OrdinalIgnoreCase) OrElse
               prop.StartsWith("MenuSelIcon_", StringComparison.OrdinalIgnoreCase) OrElse
               prop.Equals("MarkImage", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared ReadOnly SysColors As String() = {
        "ScrollBar", "Desktop", "ActiveCaption", "InactiveCaption", "Menu", "Window", "WindowFrame", "MenuText", "WindowText",
        "ActiveCaptionText", "ActiveBorder", "InactiveBorder", "AppWorkspace", "Highlight", "HighlightText", "Control",
        "ControlDark", "GrayText", "ControlText", "InactiveCaptionText", "ControlLightLight", "ControlDarkDark", "ControlLight",
        "InfoText", "Info"}

    Private Shared ReadOnly VbKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Class", "Module", "Error", "End", "Property", "Sub", "Function", "Me", "Next", "Loop", "Step", "Stop", "Set", "Get", "Date", "Text"}

    '======================================================================================
    ' State
    '======================================================================================
    Private ReadOnly _root As VbNode
    Private ReadOnly _formName As String
    Private ReadOnly _frmDir As String
    Private ReadOnly _frx As FrxReader
    Private ReadOnly _hasOwnCtor As Boolean
    Private ReadOnly _shell As VbNode                 ' Aqua.AquaForm / iForm block, merged into the form
    Private ReadOnly _baseType As Type

    Private ReadOnly _names As New Dictionary(Of VbNode, String)
    Private ReadOnly _types As New Dictionary(Of VbNode, String)
    Private ReadOnly _decl As New List(Of String)
    Private ReadOnly _create As New List(Of String)
    Private ReadOnly _suspend As New List(Of String)
    Private ReadOnly _sections As New StringBuilder()
    Private ReadOnly _extra As New List(Of String)
    Private ReadOnly _arrays As New SortedDictionary(Of String, SortedDictionary(Of Integer, String))(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _arrayTypes As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _resources As New SortedDictionary(Of String, Object)(StringComparer.Ordinal)
    Private _needComponents As Boolean
    Private _toolTips As New List(Of String)

    Public ReadOnly Property Todos As New List(Of String)
    Public Property ControlCount As Integer
    Public Property PlaceholderCount As Integer

    Public ReadOnly Property BaseTypeName As String
        Get
            Return If(_shell Is Nothing, "System.Windows.Forms.Form", "Aqua." & _shell.ShortType)
        End Get
    End Property

    Public Sub New(ByVal root As VbNode, ByVal formName As String, ByVal frmPath As String, ByVal frx As FrxReader, ByVal hasOwnCtor As Boolean)
        _root = root
        _formName = formName
        _frmDir = Path.GetDirectoryName(frmPath)
        _frx = frx
        _hasOwnCtor = hasOwnCtor
        For Each c In root.Children
            If c.TypeName.Equals("Aqua.AquaForm", StringComparison.OrdinalIgnoreCase) OrElse c.TypeName.Equals("Aqua.iForm", StringComparison.OrdinalIgnoreCase) Then
                _shell = c : Exit For
            End If
        Next
        _baseType = If(_shell Is Nothing, GetType(Form), ResolveType("Aqua." & _shell.ShortType))
    End Sub

    '======================================================================================
    ' Entry
    '======================================================================================
    Public Sub Convert(ByVal designerPath As String, ByVal resxPath As String, ByVal vb6Path As String, ByVal relSource As String)
        ' the form's own children: the shell's children (VB6 nested everything inside the AquaForm) + the other top-level nodes
        Dim top As New List(Of VbNode)
        For Each c In _root.Children
            If c Is _shell Then
                top.AddRange(c.Children)
            Else
                top.Add(c)
            End If
        Next
        RegroupPages(top)

        ' names first, so arrays and cross references are known
        For Each n In Walk(top)
            AssignName(n)
        Next

        Dim formLines As New List(Of String)
        Dim adds As New List(Of String)
        For Each n In top.OrderBy(Function(x) IsWindowless(x)).ThenByDescending(Function(x) x.Order)
            ' units come from the real VB6 parent: the AquaForm block for its children, the VB.Form for
            ' anything placed beside it (timers, menus ...)
            If EmitNode(n, n.Parent) Then
                If Not IsComponent(n) Then adds.Add("Me.Controls.Add(Me." & _names(n) & ")")
            End If
        Next
        EmitForm(formLines)
        formLines.AddRange(adds)

        WriteDesigner(designerPath, formLines)
        WriteResx(resxPath)
        WriteVb6(vb6Path, relSource)
    End Sub

    Private Iterator Function Walk(ByVal nodes As IEnumerable(Of VbNode)) As IEnumerable(Of VbNode)
        For Each n In nodes
            Yield n
            For Each c In Walk(n.Children)
                Yield c
            Next
        Next
    End Function

    ''' <summary>Aqua.PageHead + its Aqua.PageSheet siblings become one Aqua.TabControl whose TabPages
    ''' are the sheets (VB6 drew the tab strip and the pages as separate controls).</summary>
    Private Sub RegroupPages(ByVal nodes As List(Of VbNode))
        For Each container In Walk(nodes).ToList().Concat({_root})
            Dim list As List(Of VbNode) = If(container Is _root, nodes, container.Children)
            Dim head = list.FirstOrDefault(Function(x) x.TypeName.Equals("Aqua.PageHead", StringComparison.OrdinalIgnoreCase))
            If head Is Nothing Then Continue For
            Dim sheets = list.Where(Function(x) x.TypeName.Equals("Aqua.PageSheet", StringComparison.OrdinalIgnoreCase)).OrderBy(Function(x) x.Index).ToList()
            For Each s In sheets
                list.Remove(s)
                s.Parent = head
                head.Children.Add(s)
            Next
        Next
    End Sub

    '======================================================================================
    ' Names
    '======================================================================================
    Private Sub AssignName(ByVal n As VbNode)
        Dim baseName As String = n.Name
        If _baseType.GetMember(baseName, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance Or BindingFlags.Static Or BindingFlags.IgnoreCase).Length > 0 OrElse
           VbKeywords.Contains(baseName) OrElse baseName.Equals(_formName, StringComparison.OrdinalIgnoreCase) Then
            Todos.Add($"{n}: renamed to {baseName}_ (the name clashes with a member of {_baseType.Name} or a VB keyword)")
            baseName &= "_"
        End If
        Dim name As String = If(n.Index >= 0, baseName & "_" & n.Index, baseName)
        _names(n) = name
        If n.Index >= 0 Then
            If Not _arrays.ContainsKey(baseName) Then _arrays(baseName) = New SortedDictionary(Of Integer, String)
            _arrays(baseName)(n.Index) = name
        End If
    End Sub

    '======================================================================================
    ' Controls
    '======================================================================================
    Private Function TargetTypeName(ByVal n As VbNode) As String
        Dim t As String = Nothing
        If TypeMap.TryGetValue(n.TypeName, t) Then Return t
        If n.TypeName.Equals("VB.PictureBox", StringComparison.OrdinalIgnoreCase) Then
            Return If(n.Children.Count > 0, "System.Windows.Forms.Panel", "System.Windows.Forms.PictureBox")
        End If
        If n.TypeName.StartsWith("Aqua.", StringComparison.OrdinalIgnoreCase) AndAlso ResolveType(n.TypeName) IsNot Nothing Then Return n.TypeName
        If n.TypeName.Equals("MSComDlg.CommonDialog", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Return "System.Windows.Forms.Panel"   ' placeholder
    End Function

    ''' <summary>VB6 Label / Line / Shape have no window: they are always drawn under the windowed
    ''' controls of the same container, whatever their ZOrder. As WinForms Labels they are real windows,
    ''' so they are added last (bottom of the z-order) -- otherwise a label covers the picture or text
    ''' box it overlaps (a transparent Label only shows its parent through, not its siblings).</summary>
    Private Shared Function IsWindowless(ByVal n As VbNode) As Boolean
        Return n.TypeName.Equals("VB.Label", StringComparison.OrdinalIgnoreCase) OrElse
               n.TypeName.Equals("VB.Line", StringComparison.OrdinalIgnoreCase) OrElse
               n.TypeName.Equals("VB.Shape", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsComponent(ByVal n As VbNode) As Boolean
        Return ComponentTypes.Contains(n.TypeName)
    End Function

    Private Function IsPlaceholder(ByVal n As VbNode) As Boolean
        Return Not TypeMap.ContainsKey(n.TypeName) AndAlso
               Not n.TypeName.Equals("VB.PictureBox", StringComparison.OrdinalIgnoreCase) AndAlso
               Not (n.TypeName.StartsWith("Aqua.", StringComparison.OrdinalIgnoreCase) AndAlso ResolveType(n.TypeName) IsNot Nothing) AndAlso
               Not n.TypeName.Equals("MSComDlg.CommonDialog", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Emits declaration, creation and property section for a node (and its subtree).
    ''' Returns False when the node produces no control (CommonDialog).</summary>
    Private Function EmitNode(ByVal n As VbNode, ByVal container As VbNode) As Boolean
        Dim typeName As String = TargetTypeName(n)
        Dim name As String = _names(n)
        If typeName Is Nothing Then
            Todos.Add($"{n}: MSComDlg.CommonDialog has no control -- use OpenFileDialog / SaveFileDialog / PrintDialog / ColorDialog in code")
            Return False
        End If
        Dim t As Type = ResolveType(typeName)
        _types(n) = typeName
        If n.Index >= 0 Then _arrayTypes(BaseArrayName(name, n.Index)) = typeName
        ControlCount += 1

        Dim ctl As String = "Me." & name
        _decl.Add($"Friend WithEvents {name} As {typeName}")
        If IsComponent(n) Then
            _needComponents = True
            _create.Add($"{ctl} = New {typeName}(Me.components)")
        Else
            _create.Add($"{ctl} = New {typeName}()")
        End If
        If n.Children.Count > 0 Then _suspend.Add(ctl)

        Dim lines As New List(Of String)
        If IsPlaceholder(n) Then
            PlaceholderCount += 1
            Todos.Add($"{n}: no .NET equivalent -- placeholder Panel ({name}); VB6 properties: {String.Join(", ", n.Props.Where(Function(p) Not GlobalSkip.Contains(p.Key)).Select(Function(p) p.Key & "=" & p.Value))}")
            lines.Add($"{ctl}.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle")
        End If

        ' geometry
        If Not IsComponent(n) Then
            If n.TypeName.Equals("VB.Line", StringComparison.OrdinalIgnoreCase) Then
                EmitLine(n, ctl, container, lines)
            ElseIf Not IsTabPage(n) Then
                Dim d As Double = Divisor(container)
                Dim r As Rectangle = Rect(n, d)
                If n.TypeName.Equals("Aqua.PageHead", StringComparison.OrdinalIgnoreCase) Then
                    ' the tab strip where VB6's PageHead was, within the pages' width
                    Dim head As Rectangle = r
                    r = PageUnion(n, d)
                    Dim padL As Integer = head.Left - r.Left, padR As Integer = r.Right - head.Right
                    If padL > 0 OrElse padR > 0 Then lines.Add($"{ctl}.TabStripPadding = New System.Windows.Forms.Padding({Math.Max(0, padL)}, 0, {Math.Max(0, padR)}, 0)")
                End If
                lines.Add($"{ctl}.Location = New System.Drawing.Point({r.X}, {r.Y})")
                lines.Add($"{ctl}.Size = New System.Drawing.Size({r.Width}, {r.Height})")
            End If
            lines.Add($"{ctl}.Name = ""{name}""")
            Dim ti As String = n.Prop("TabIndex")
            If ti IsNot Nothing Then lines.Add($"{ctl}.TabIndex = {CInt(Num(ti))}")
            Dim tip As String = n.Prop("ToolTipText")
            If tip IsNot Nothing AndAlso Str(tip).Length > 0 Then _toolTips.Add($"Me.vb6ToolTip.SetToolTip({ctl}, {Lit(Str(tip))})")
        End If

        If Not IsPlaceholder(n) Then
            EmitSpecial(n, t, ctl, name, lines)
            EmitProps(n, n.TypeName, t, ctl, name, lines)
            EmitFontBlocks(n, t, ctl, lines)
        End If

        ' children (z-order: VB6 lists back-to-front, WinForms Controls(0) is the front)
        For Each c In n.Children.OrderBy(Function(x) IsWindowless(x)).ThenByDescending(Function(x) x.Order)
            If Not EmitNode(c, n) Then Continue For
            If IsComponent(c) Then Continue For
            If IsTabPage(c) Then
                ' added in index order below
            Else
                lines.Add($"{ctl}.Controls.Add(Me.{_names(c)})")
            End If
        Next
        If n.TypeName.Equals("Aqua.PageHead", StringComparison.OrdinalIgnoreCase) Then
            For Each s In n.Children.Where(Function(x) IsTabPage(x)).OrderBy(Function(x) x.Index)
                lines.Add($"{ctl}.TabPages.Add(Me.{_names(s)})")
            Next
        End If

        _sections.AppendLine("        '")
        _sections.AppendLine("        '" & name)
        _sections.AppendLine("        '")
        For Each l In lines
            _sections.AppendLine("        " & l)
        Next
        Return True
    End Function

    Private Shared Function IsTabPage(ByVal n As VbNode) As Boolean
        Return n.TypeName.Equals("Aqua.PageSheet", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function BaseArrayName(ByVal name As String, ByVal index As Integer) As String
        Return name.Substring(0, name.Length - ("_" & index).Length)
    End Function

    '======================================================================================
    ' Type-specific translation
    '======================================================================================
    Private Sub EmitSpecial(ByVal n As VbNode, ByVal t As Type, ByVal ctl As String, ByVal name As String, ByVal lines As List(Of String))
        Select Case n.TypeName.ToLowerInvariant()
            Case "vb.label"
                Dim a As String = n.Prop("Alignment")
                If a IsNot Nothing Then lines.Add($"{ctl}.TextAlign = System.Drawing.ContentAlignment.{Choose3(CInt(Num(a)), "TopLeft", "TopRight", "TopCenter")}")
                Dim bs As String = n.Prop("BackStyle")
                If bs IsNot Nothing AndAlso Num(bs) = 0 Then
                    ' transparent: VB6 ignores BackColor then, so it must not override Transparent below
                    lines.Add($"{ctl}.BackColor = System.Drawing.Color.Transparent")
                    RemoveProp(n, "BackColor")
                End If

            Case "vb.textbox"
                Dim b As String = n.Prop("BorderStyle")
                If b IsNot Nothing AndAlso Num(b) = 0 Then lines.Add($"{ctl}.BorderStyle = System.Windows.Forms.BorderStyle.None")

            Case "vb.image"
                lines.Add($"{ctl}.BackColor = System.Drawing.Color.Transparent")
                If Bool(n.Prop("Stretch")) Then lines.Add($"{ctl}.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage")

            Case "vb.picturebox"
                Dim b As String = n.Prop("BorderStyle")
                lines.Add($"{ctl}.BorderStyle = System.Windows.Forms.BorderStyle.{If(b IsNot Nothing AndAlso Num(b) = 0, "None", "Fixed3D")}")
                Dim pic As String = n.Prop("Picture")
                If pic IsNot Nothing Then
                    Dim prop As String = If(n.Children.Count > 0, "BackgroundImage", "Image")
                    Dim code As String = ImageCode(pic, name & "." & prop, "Picture")
                    If code IsNot Nothing Then
                        lines.Add($"{ctl}.{prop} = {code}")
                        If n.Children.Count > 0 Then lines.Add($"{ctl}.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None")
                    End If
                End If
                If n.Children.Count = 0 AndAlso Bool(n.Prop("AutoSize")) Then lines.Add($"{ctl}.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize")

            Case "vb.shape"
                Dim shape As Integer = CInt(Num(n.Prop("Shape")))
                If shape <> 0 Then Todos.Add($"{n}: Shape={shape} (oval/rounded) drawn as a rectangle Label -- paint it if the shape matters")
                Dim bstyle As String = n.Prop("BorderStyle")
                If bstyle Is Nothing OrElse Num(bstyle) <> 0 Then lines.Add($"{ctl}.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle")
                Dim fill As String = n.Prop("FillStyle")
                If fill IsNot Nothing AndAlso Num(fill) = 0 Then
                    lines.Add($"{ctl}.BackColor = {ColorCode(Num(If(n.Prop("FillColor"), "0")))}")
                ElseIf n.Prop("BackStyle") IsNot Nothing AndAlso Num(n.Prop("BackStyle")) = 1 Then
                    lines.Add($"{ctl}.BackColor = {ColorCode(Num(If(n.Prop("BackColor"), "-2147483643")))}")
                Else
                    lines.Add($"{ctl}.BackColor = System.Drawing.Color.Transparent")
                End If
                If n.Prop("BorderColor") IsNot Nothing Then Todos.Add($"{n}: BorderColor={n.Prop("BorderColor")} -- a Label border is always the system colour")

            Case "aqua.multilinetextbox"
                lines.Add($"{ctl}.Multiline = True")

            Case "aqua.medialist", "aqua.mediaitem"
                ' VB6's MediaList/MediaItem had no check box, and DragItem/DropItem defaulted to False
                ' (the .NET control defaults them to True for its other users)
                lines.Add($"{ctl}.ShowCheckBox = False")
                If n.Prop("DragItem") Is Nothing Then lines.Add($"{ctl}.DragItem = False")
                If n.Prop("DropItem") Is Nothing Then lines.Add($"{ctl}.DropItem = False")

            Case "aqua.desktop"
                lines.Add($"{ctl}.ShowSpecialFolders = True")

            Case "aqua.buttons"
                For i = 0 To 63
                    Dim txt As String = n.Prop("Text" & i)
                    If txt Is Nothing Then Continue For
                    RemoveProp(n, "Text" & i)
                    _extra.Add($"Me.{name}.AdditionButton({Lit(Str(txt))}, Nothing)")
                Next

            Case "aqua.toolbar"
                For i = 0 To 63
                    Dim txt As String = n.Prop("Text" & i)
                    Dim ic As String = n.Prop("Icon" & i)
                    If txt Is Nothing AndAlso ic Is Nothing Then Continue For
                    RemoveProp(n, "Text" & i) : RemoveProp(n, "Icon" & i)
                    Dim icCode As String = If(ic Is Nothing, "Nothing", If(ImageCode(ic, name & ".Icon" & i, "Icon" & i), "Nothing"))
                    _extra.Add($"Me.{name}.AdditionButton({Lit(Str(If(txt, """""")))}, {icCode})")
                Next

            Case "aqua.grid"
                Dim any As Boolean = False
                For i = 0 To 63
                    Dim txt As String = n.Prop("Text" & i)
                    If txt Is Nothing AndAlso n.Prop("Width" & i) Is Nothing AndAlso n.Prop("Alignment" & i) Is Nothing Then Continue For
                    Dim w As Integer = CInt(Math.Round(Num(If(n.Prop("Width" & i), "0")) / 15))
                    Dim al As Integer = CInt(Num(If(n.Prop("Alignment" & i), "0")))
                    Dim srt As Boolean = Bool(n.Prop("Sort" & i))
                    For Each k In {"Text", "Width", "Alignment", "Sort"}
                        RemoveProp(n, k & i)
                    Next
                    _extra.Add($"Me.{name}.Header.AdditionHeader(Nothing, {Lit(Str(If(txt, """""")))}, {EnumCode(GetType(Aqua.AlignmentConstants), al)}, {w}, {If(srt, "True", "False")}, Aqua.SortOrder.None)")
                    any = True
                Next
                If any Then _extra.Add($"Me.{name}.SetHeaderProperty()")

            Case "aqua.pagehead"
                ' page titles go onto the TabPages (see "aqua.pagesheet")
                For i = 0 To 63
                    RemoveProp(n, "Text" & i)
                Next

            Case "aqua.pagesheet"
                Dim head As VbNode = n.Parent
                ' the head's TextN were taken off its property list when it was emitted (before its children)
                Dim title As String = If(head IsNot Nothing, OriginalProp(head, "Text" & n.Index), Nothing)
                lines.Add($"{ctl}.Title = {Lit(Str(If(title, """""")))}")
                Todos.Add($"{n}: became TabPage {n.Index} of {_names(head)} -- check the page layout (VB6 drew tab strip and sheet separately)")

            Case "aqua.aquamenu"
                EmitMenu(n, name)

            Case "mscomctllib.treeview"
                Dim le As String = n.Prop("LabelEdit")
                lines.Add($"{ctl}.LabelEdit = {If(le IsNot Nothing AndAlso Num(le) = 1, "False", "True")}")

            Case "mscomctllib.listview"
                Dim le As String = n.Prop("LabelEdit")
                lines.Add($"{ctl}.LabelEdit = {If(le IsNot Nothing AndAlso Num(le) = 1, "False", "True")}")
                Dim v As Integer = CInt(Num(If(n.Prop("View"), "0")))
                lines.Add($"{ctl}.View = System.Windows.Forms.View.{Choose4(v, "LargeIcon", "SmallIcon", "List", "Details")}")
                If Bool(n.Prop("HideColumnHeaders")) Then lines.Add($"{ctl}.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None")
                Dim cols As VbBlock = n.Block("ColumnHeaders")
                If cols IsNot Nothing Then
                    For Each ch In cols.Blocks
                        Dim w As Integer = CInt(Math.Round(Num(If(ch.Prop("Object.Width"), "1440")) / 15))
                        _extra.Add($"Me.{name}.Columns.Add({Lit(Str(If(ch.Prop("Text"), """""")))}, {w})")
                    Next
                End If

            Case "mscomctllib.imagelist"
                Dim w As Integer = CInt(Num(If(n.Prop("ImageWidth"), "16")))
                Dim h As Integer = CInt(Num(If(n.Prop("ImageHeight"), "16")))
                lines.Add($"{ctl}.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit")
                lines.Add($"{ctl}.ImageSize = New System.Drawing.Size({w}, {h})")
                Dim mask As String = n.Prop("MaskColor")
                If mask IsNot Nothing AndAlso (n.Prop("UseMaskColor") Is Nothing OrElse Bool(n.Prop("UseMaskColor"))) Then lines.Add($"{ctl}.TransparentColor = {ColorCode(Num(mask))}")
                Dim imgs As VbBlock = n.Block("Images")
                If imgs IsNot Nothing Then
                    Dim k As Integer = 0
                    For Each li In imgs.Blocks
                        k += 1
                        Dim pic As String = li.Prop("Picture")
                        If pic Is Nothing Then Continue For
                        Dim code As String = ImageCode(pic, name & "." & li.Name, li.Name)
                        If code Is Nothing Then Continue For
                        Dim key As String = Str(If(li.Prop("Key"), """"""))
                        _extra.Add(If(key.Length > 0, $"Me.{name}.Images.Add({Lit(key)}, {code})", $"Me.{name}.Images.Add({code})"))
                    Next
                End If

            Case "vb.timer"
                ' a VB6 Timer is enabled unless the .frm says Enabled = 0; a WinForms Timer starts disabled
                If n.Prop("Enabled") Is Nothing Then lines.Add($"{ctl}.Enabled = True")

            Case "mediaplayerctl.mediaplayer"
                ' only geometry/Visible matter; the WMP settings have no counterpart
                For Each p In n.Props.ToList()
                    If Not p.Key.Equals("Visible", StringComparison.OrdinalIgnoreCase) AndAlso Not p.Key.Equals("Enabled", StringComparison.OrdinalIgnoreCase) Then RemoveProp(n, p.Key)
                Next
                Todos.Add($"{n}: Windows Media Player -> Aqua.MediaViewerControl (set FileName in code)")
        End Select
    End Sub

    Private Sub EmitMenu(ByVal n As VbNode, ByVal name As String)
        Dim count As Integer = CInt(Num(If(n.Prop("MenuCount"), "0")))
        For i = 0 To count - 1
            Dim nm As String = Str(If(n.Prop("MenuName_" & i), """"""))
            Dim tx As String = Str(If(n.Prop("MenuText_" & i), """"""))
            Dim lv As Integer = CInt(Num(If(n.Prop("MenuLevel_" & i), "1")))
            Dim ic As String = n.Prop("MenuIcon_" & i)
            Dim sic As String = n.Prop("MenuSelIcon_" & i)
            Dim icCode As String = If(ic Is Nothing, "Nothing", If(ImageCode(ic, name & ".MenuIcon_" & i, "MenuIcon_" & i), "Nothing"))
            Dim sicCode As String = If(sic Is Nothing, "Nothing", If(ImageCode(sic, name & ".MenuSelIcon_" & i, "MenuSelIcon_" & i), "Nothing"))
            Dim extra As New List(Of String)
            If n.Prop("MenuEnabled_" & i) IsNot Nothing AndAlso Not Bool(n.Prop("MenuEnabled_" & i)) Then extra.Add(".Enabled = False")
            If n.Prop("MenuVisible_" & i) IsNot Nothing AndAlso Not Bool(n.Prop("MenuVisible_" & i)) Then extra.Add(".Visible = False")
            If n.Prop("MenuChecked_" & i) IsNot Nothing AndAlso Bool(n.Prop("MenuChecked_" & i)) Then extra.Add(".Checked = True")
            If n.Prop("MenuCheckStyle_" & i) IsNot Nothing Then extra.Add(".CheckStyle = " & EnumCode(GetType(Aqua.ItemCheckStyle), CInt(Num(n.Prop("MenuCheckStyle_" & i)))))
            If n.Prop("MenuTag_" & i) IsNot Nothing Then extra.Add(".Tag = " & Lit(Str(n.Prop("MenuTag_" & i))))
            Dim call_ As String = $"Me.{name}.AddItem({Lit(nm)}, {Lit(tx)}, {lv}, {icCode}, {sicCode})"
            If extra.Count = 0 Then
                _extra.Add(call_)
            Else
                _extra.Add($"With {call_}")
                For Each e In extra
                    _extra.Add("    " & e)
                Next
                _extra.Add("End With")
            End If
        Next
        For Each p In n.Props.ToList()
            If p.Key.StartsWith("Menu", StringComparison.OrdinalIgnoreCase) Then RemoveProp(n, p.Key)
        Next
    End Sub

    ''' <summary>A horizontal/vertical VB.Line becomes a 1-pixel Label filled with the line colour.</summary>
    Private Sub EmitLine(ByVal n As VbNode, ByVal ctl As String, ByVal container As VbNode, ByVal lines As List(Of String))
        Dim d As Double = Divisor(container)
        Dim x1 As Integer = Px(n.Prop("X1"), d), x2 As Integer = Px(n.Prop("X2"), d)
        Dim y1 As Integer = Px(n.Prop("Y1"), d), y2 As Integer = Px(n.Prop("Y2"), d)
        Dim w As Integer = Math.Max(1, CInt(Num(If(n.Prop("BorderWidth"), "1"))))
        Dim r As Rectangle
        If y1 = y2 Then
            r = New Rectangle(Math.Min(x1, x2), y1, Math.Max(1, Math.Abs(x2 - x1)), w)
        ElseIf x1 = x2 Then
            r = New Rectangle(x1, Math.Min(y1, y2), w, Math.Max(1, Math.Abs(y2 - y1)))
        Else
            r = New Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(1, Math.Abs(x2 - x1)), Math.Max(1, Math.Abs(y2 - y1)))
            Todos.Add($"{n}: diagonal line ({x1},{y1})-({x2},{y2}) -- draw it in Paint; the placeholder Label only marks the area")
        End If
        lines.Add($"{ctl}.AutoSize = False")
        lines.Add($"{ctl}.Location = New System.Drawing.Point({r.X}, {r.Y})")
        lines.Add($"{ctl}.Size = New System.Drawing.Size({r.Width}, {r.Height})")
        lines.Add($"{ctl}.BackColor = {ColorCode(Num(If(n.Prop("BorderColor"), "-2147483640")))}")
    End Sub

    '======================================================================================
    ' Generic properties (reflection on the target type)
    '======================================================================================
    Private Sub EmitProps(ByVal n As VbNode, ByVal vbType As String, ByVal t As Type, ByVal ctl As String, ByVal resPrefix As String, ByVal lines As List(Of String))
        For Each kv In n.Props.ToList()
            Dim key As String = kv.Key
            If GlobalSkip.Contains(key) Then Continue For
            If key.StartsWith("Object.", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim netName As String = Rename(vbType, key)
            If netName = "" Then Continue For
            Dim pi As PropertyInfo = FindProp(t, netName)
            If pi Is Nothing Then
                Todos.Add($"{n}: {key} = {kv.Value} -- {ShortName(t)} has no {netName}")
                Continue For
            End If
            Dim code As String = ValueCode(pi.PropertyType, kv.Value, resPrefix & "." & netName, key)
            If code Is Nothing Then
                Todos.Add($"{n}: {key} = {kv.Value} -- can't convert to {pi.PropertyType.Name}")
                Continue For
            End If
            lines.Add($"{ctl}.{netName} = {code}")
        Next
    End Sub

    Private Sub EmitFontBlocks(ByVal n As VbNode, ByVal t As Type, ByVal ctl As String, ByVal lines As List(Of String))
        For Each b In n.Blocks
            If Not b.Name.EndsWith("Font", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim pi As PropertyInfo = FindProp(t, b.Name)
            If pi Is Nothing OrElse Not GetType(Font).IsAssignableFrom(pi.PropertyType) Then
                Todos.Add($"{n}: {b.Name} = {FontCode(b)} -- {ShortName(t)} has no {b.Name}")
                Continue For
            End If
            lines.Add($"{ctl}.{b.Name} = {FontCode(b)}")
        Next
    End Sub

    Private Sub EmitForm(ByVal lines As List(Of String))
        lines.Add("Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None")
        ' VB6's AquaForm/iForm took over its host window and sized it to itself, so with a shell its
        ' Width/Height is the real window size (frmPrint / frmPhotoInfo differ from ClientWidth/Height).
        Dim sizeSrc As VbNode = If(_shell IsNot Nothing AndAlso _shell.Prop("Width") IsNot Nothing, _shell, Nothing)
        Dim cw As Integer = CInt(Math.Round(Num(If(If(sizeSrc?.Prop("Width"), _root.Prop("ClientWidth")), "4800")) / 15))
        Dim ch As Integer = CInt(Math.Round(Num(If(If(sizeSrc?.Prop("Height"), _root.Prop("ClientHeight")), "3600")) / 15))
        lines.Add($"Me.ClientSize = New System.Drawing.Size({cw}, {ch})")
        If _shell Is Nothing Then
            Dim bs As String = _root.Prop("BorderStyle")
            If bs IsNot Nothing Then lines.Add("Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle." & ChooseN(CInt(Num(bs)), "None", "FixedSingle", "Sizable", "FixedDialog", "FixedToolWindow", "SizableToolWindow"))
        End If
        Dim sp As String = _root.Prop("StartUpPosition")
        If sp IsNot Nothing Then lines.Add("Me.StartPosition = System.Windows.Forms.FormStartPosition." & ChooseN(CInt(Num(sp)), "Manual", "CenterParent", "CenterScreen", "WindowsDefaultLocation"))
        Dim formType As Type = _baseType
        EmitProps(_root, "VB.Form", formType, "Me", "$this", lines)
        EmitFontBlocks(_root, formType, "Me", lines)
        If _shell IsNot Nothing Then
            EmitProps(_shell, _shell.TypeName, formType, "Me", "$this", lines)
            EmitFontBlocks(_shell, formType, "Me", lines)
        End If
        lines.Add($"Me.Name = ""{_formName}""")
    End Sub

    Private Shared Function Rename(ByVal vbType As String, ByVal key As String) As String
        Dim m As Dictionary(Of String, String) = Nothing
        Dim r As String = Nothing
        If Renames.TryGetValue(vbType, m) AndAlso m.TryGetValue(key, r) Then Return r
        If Renames("*").TryGetValue(key, r) Then Return r
        Return key
    End Function

    Private Shared Function FindProp(ByVal t As Type, ByVal name As String) As PropertyInfo
        If t Is Nothing Then Return Nothing
        ' most-derived writable, non-indexed property of that name (Shadows declarations included)
        Dim cur As Type = t
        While cur IsNot Nothing
            For Each p In cur.GetProperties(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.DeclaredOnly)
                If p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) AndAlso p.CanWrite AndAlso p.GetIndexParameters().Length = 0 AndAlso
                   p.GetSetMethod() IsNot Nothing Then Return p
            Next
            cur = cur.BaseType
        End While
        Return Nothing
    End Function

    '======================================================================================
    ' Values
    '======================================================================================
    Private Function ValueCode(ByVal pt As Type, ByVal raw As String, ByVal resKey As String, ByVal vbProp As String) As String
        If IsFrx(raw) Then
            If GetType(Icon).IsAssignableFrom(pt) Then Return IconCode(raw, resKey)
            If GetType(Image).IsAssignableFrom(pt) Then Return ImageCode(raw, resKey, vbProp)
            If pt Is GetType(String) Then
                Dim f As String = Nothing, o As Integer
                Frx(raw, f, o)
                Dim s As String = _frx.Text(Path.Combine(_frmDir, f), o)
                Return If(s Is Nothing, Nothing, Lit(s))
            End If
            Return Nothing
        End If
        If pt Is GetType(Boolean) Then Return If(Bool(raw), "True", "False")
        If pt.IsEnum Then Return EnumCode(pt, CLng(Num(raw)))
        If pt Is GetType(Color) Then Return ColorCode(Num(raw))
        If pt Is GetType(String) Then Return Lit(Str(raw))
        If pt Is GetType(Char) Then
            Dim s As String = Str(raw)
            Return If(s.Length = 0, "Global.Microsoft.VisualBasic.ChrW(0)", Lit(s.Substring(0, 1)) & "c")
        End If
        If pt Is GetType(Integer) OrElse pt Is GetType(Short) OrElse pt Is GetType(Long) OrElse pt Is GetType(Byte) Then
            Return CLng(Num(raw)).ToString(CultureInfo.InvariantCulture)
        End If
        If pt Is GetType(Single) Then Return Num(raw).ToString("R", CultureInfo.InvariantCulture) & "!"
        If pt Is GetType(Double) Then Return Num(raw).ToString("R", CultureInfo.InvariantCulture) & "R"
        If pt Is GetType(Decimal) Then Return Num(raw).ToString(CultureInfo.InvariantCulture) & "D"
        Return Nothing
    End Function

    Private Function ImageCode(ByVal raw As String, ByVal resKey As String, ByVal vbProp As String) As String
        Dim f As String = Nothing, o As Integer
        If Not Frx(raw, f, o) Then Return Nothing
        Dim bytes As Byte() = _frx.PictureBytes(Path.Combine(_frmDir, f), o)
        If bytes Is Nothing Then Return "Nothing"   ' empty picture in VB6
        Dim bmp As Bitmap = FrxReader.ToBitmap(bytes)
        If bmp Is Nothing Then
            Todos.Add($"{resKey}: picture format not supported (metafile?) -- load it in code")
            Return Nothing
        End If
        If WantsMagentaTransparent(vbProp) Then bmp.MakeTransparent(Color.FromArgb(255, 0, 255))
        _resources(resKey) = bmp
        Return $"CType(resources.GetObject(""{resKey}""), System.Drawing.Image)"
    End Function

    Private Function IconCode(ByVal raw As String, ByVal resKey As String) As String
        Dim f As String = Nothing, o As Integer
        If Not Frx(raw, f, o) Then Return Nothing
        Dim bytes As Byte() = _frx.PictureBytes(Path.Combine(_frmDir, f), o)
        If bytes Is Nothing Then Return "Nothing"
        Dim ic As Icon = FrxReader.ToIcon(bytes)
        If ic Is Nothing Then Return Nothing
        _resources(resKey) = ic
        Return $"CType(resources.GetObject(""{resKey}""), System.Drawing.Icon)"
    End Function

    Private Shared Function EnumCode(ByVal t As Type, ByVal value As Long) As String
        Dim tn As String = t.FullName.Replace("+", ".")
        Dim boxed As Object = [Enum].ToObject(t, value)
        Dim name As String = [Enum].GetName(t, boxed)
        If name IsNot Nothing Then Return tn & "." & name
        Return $"CType({value}, {tn})"
    End Function

    Private Shared Function ColorCode(ByVal ole As Double) As String
        Dim v As Long = CLng(ole)
        If v < 0 Then
            Dim idx As Integer = CInt(v And &HFF)
            If idx >= 0 AndAlso idx < SysColors.Length Then Return "System.Drawing.SystemColors." & SysColors(idx)
            Return "System.Drawing.SystemColors.Control"
        End If
        Dim r As Integer = CInt(v And &HFF), g As Integer = CInt((v >> 8) And &HFF), b As Integer = CInt((v >> 16) And &HFF)
        Return $"System.Drawing.Color.FromArgb(CType(CType({r}, Byte), Integer), CType(CType({g}, Byte), Integer), CType(CType({b}, Byte), Integer))"
    End Function

    Private Shared Function FontCode(ByVal b As VbBlock) As String
        Dim name As String = Str(If(b.Prop("Name"), """新細明體"""))
        Dim size As Double = Num(If(b.Prop("Size"), "9"))
        Dim styles As New List(Of String)
        If Num(If(b.Prop("Weight"), "400")) >= 600 Then styles.Add("System.Drawing.FontStyle.Bold")
        If Bool(b.Prop("Italic")) Then styles.Add("System.Drawing.FontStyle.Italic")
        If Bool(b.Prop("Underline")) Then styles.Add("System.Drawing.FontStyle.Underline")
        If Bool(b.Prop("Strikethrough")) Then styles.Add("System.Drawing.FontStyle.Strikeout")
        Dim style As String = If(styles.Count = 0, "System.Drawing.FontStyle.Regular",
                                 If(styles.Count = 1, styles(0), "CType((" & String.Join(" Or ", styles) & "), System.Drawing.FontStyle)"))
        Dim charset As Integer = CInt(Num(If(b.Prop("Charset"), "1")))
        Return $"New System.Drawing.Font({Lit(name)}, {size.ToString("R", CultureInfo.InvariantCulture)}!, {style}, System.Drawing.GraphicsUnit.Point, CType({charset}, Byte))"
    End Function

    Private Shared Function Lit(ByVal s As String) As String
        If s Is Nothing Then Return """"""
        If s.IndexOfAny({ChrW(13), ChrW(10)}) >= 0 Then
            Dim parts = s.Replace(vbCrLf, vbLf).Split(ChrW(10)).Select(Function(p) """" & p.Replace("""", """""") & """")
            Return String.Join(" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & ", parts)
        End If
        Return """" & s.Replace("""", """""") & """"
    End Function

    '======================================================================================
    ' Geometry
    '======================================================================================
    ''' <summary>The .frm always persists control positions in twips -- the container's ScaleMode only sets
    ''' the units code sees at run time (verified on iPhoto: children of ScaleMode=3 containers, VB.Form and
    ''' VB.PictureBox included, still extend to the container's twip width).</summary>
    Private Function Divisor(ByVal container As VbNode) As Double
        Return 15.0
    End Function

    Private Shared Function Px(ByVal raw As String, ByVal d As Double) As Integer
        Return CInt(Math.Round(Num(If(raw, "0")) / d))
    End Function

    Private Shared Function Rect(ByVal n As VbNode, ByVal d As Double) As Rectangle
        Return New Rectangle(Px(n.Prop("Left"), d), Px(n.Prop("Top"), d), Math.Max(1, Px(n.Prop("Width"), d)), Math.Max(1, Px(n.Prop("Height"), d)))
    End Function

    ''' <summary>The TabControl covers the old tab strip and all its sheets.</summary>
    Private Function PageUnion(ByVal head As VbNode, ByVal d As Double) As Rectangle
        Dim r As Rectangle = Rect(head, d)
        For Each s In head.Children.Where(Function(x) IsTabPage(x))
            r = Rectangle.Union(r, Rect(s, d))
        Next
        Return r
    End Function

    '======================================================================================
    ' Output
    '======================================================================================
    Private Sub WriteDesigner(ByVal path As String, ByVal formLines As List(Of String))
        Dim sb As New StringBuilder()
        sb.AppendLine("<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>")
        sb.AppendLine("Partial Class " & _formName)
        sb.AppendLine("    Inherits " & BaseTypeName)
        sb.AppendLine()
        sb.AppendLine("    'Form overrides dispose to clean up the component list.")
        sb.AppendLine("    <System.Diagnostics.DebuggerNonUserCode()>")
        sb.AppendLine("    Protected Overrides Sub Dispose(ByVal disposing As Boolean)")
        sb.AppendLine("        Try")
        sb.AppendLine("            If disposing AndAlso components IsNot Nothing Then")
        sb.AppendLine("                components.Dispose()")
        sb.AppendLine("            End If")
        sb.AppendLine("        Finally")
        sb.AppendLine("            MyBase.Dispose(disposing)")
        sb.AppendLine("        End Try")
        sb.AppendLine("    End Sub")
        sb.AppendLine()
        sb.AppendLine("    'Required by the Windows Form Designer")
        sb.AppendLine("    Private components As System.ComponentModel.IContainer")
        sb.AppendLine()
        sb.AppendLine("    'NOTE: The following procedure is required by the Windows Form Designer")
        sb.AppendLine("    'It can be modified using the Windows Form Designer.")
        sb.AppendLine("    'Do not modify it using the code editor.")
        sb.AppendLine("    <System.Diagnostics.DebuggerStepThrough()>")
        sb.AppendLine("    Private Sub InitializeComponent()")
        Dim needTip As Boolean = _toolTips.Count > 0
        If _needComponents OrElse needTip Then sb.AppendLine("        Me.components = New System.ComponentModel.Container()")
        If _sections.ToString().Contains("resources.GetObject") OrElse formLines.Any(Function(l) l.Contains("resources.GetObject")) Then
            sb.AppendLine($"        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType({_formName}))")
        End If
        For Each l In _create
            sb.AppendLine("        " & l)
        Next
        If needTip Then sb.AppendLine("        Me.vb6ToolTip = New System.Windows.Forms.ToolTip(Me.components)")
        For Each s In _suspend
            sb.AppendLine("        " & s & ".SuspendLayout()")
        Next
        sb.AppendLine("        Me.SuspendLayout()")
        sb.Append(_sections)
        sb.AppendLine("        '")
        sb.AppendLine("        '" & _formName)
        sb.AppendLine("        '")
        For Each l In formLines
            sb.AppendLine("        " & l)
        Next
        For Each t In _toolTips
            sb.AppendLine("        " & t)
        Next
        For Each s In Enumerable.Reverse(_suspend)
            sb.AppendLine("        " & s & ".ResumeLayout(False)")
        Next
        sb.AppendLine("        Me.ResumeLayout(False)")
        sb.AppendLine()
        sb.AppendLine("    End Sub")
        sb.AppendLine()
        For Each d In _decl
            sb.AppendLine("    " & d)
        Next
        If needTip Then sb.AppendLine("    Friend WithEvents vb6ToolTip As System.Windows.Forms.ToolTip")
        sb.AppendLine("End Class")
        File.WriteAllText(path, sb.ToString(), New UTF8Encoding(True))
    End Sub

    Private Sub WriteResx(ByVal path As String)
        If _resources.Count = 0 Then
            If File.Exists(path) Then File.Delete(path)
            Return
        End If
        Using w As New ResXResourceWriter(path)
            For Each kv In _resources
                w.AddResource(kv.Key, kv.Value)
            Next
        End Using
    End Sub

    Private Sub WriteVb6(ByVal path As String, ByVal relSource As String)
        Dim sb As New StringBuilder()
        sb.AppendLine($"' <auto-generated> by Tools\FrmConverter from {relSource}.")
        sb.AppendLine("' Regenerated on every run -- put hand-written code in the form's own .vb file, not here.")
        sb.AppendLine("'")
        If Todos.Count = 0 Then
            sb.AppendLine("' Converter notes: none.")
        Else
            sb.AppendLine($"' TODO(port) -- {Todos.Count} item(s) the converter could not translate:")
            For Each t In Todos
                sb.AppendLine("'   - " & t.Replace(vbCr, " ").Replace(vbLf, " "))
            Next
        End If
        sb.AppendLine("Partial Class " & _formName)
        sb.AppendLine()
        If Not _hasOwnCtor Then
            sb.AppendLine("    Public Sub New()")
            sb.AppendLine("        InitializeComponent()")
            sb.AppendLine("        InitializeVb6()")
            sb.AppendLine("    End Sub")
            sb.AppendLine()
        Else
            sb.AppendLine("    ' The form's .vb defines Sub New: call InitializeVb6() there, right after InitializeComponent().")
            sb.AppendLine()
        End If
        sb.AppendLine("    ''' <summary>VB6 designer content the WinForms designer can't hold (list-style properties:")
        sb.AppendLine("    ''' Buttons/ToolBar items, Grid headers, AquaMenu items, ImageList images, ListView columns).</summary>")
        sb.AppendLine("    Private Sub InitializeVb6()")
        If _extra.Any(Function(l) l.Contains("resources.GetObject")) Then
            sb.AppendLine($"        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType({_formName}))")
        End If
        For Each l In _extra
            sb.AppendLine("        " & l)
        Next
        sb.AppendLine("    End Sub")
        For Each arr In _arrays
            Dim t As String = Nothing
            If Not _arrayTypes.TryGetValue(arr.Key, t) Then Continue For   ' e.g. an array of CommonDialogs
            Dim maxIdx As Integer = arr.Value.Keys.Max()
            Dim items As New List(Of String)
            For i = 0 To maxIdx
                Dim nm As String = Nothing
                items.Add(If(arr.Value.TryGetValue(i, nm), "Me." & nm, "Nothing"))
            Next
            sb.AppendLine()
            sb.AppendLine($"    Private _{arr.Key} As {t}()")
            sb.AppendLine($"    ''' <summary>VB6 control array {arr.Key}(0..{maxIdx}); indexes VB6 didn't use are Nothing.</summary>")
            sb.AppendLine($"    Friend ReadOnly Property {arr.Key} As {t}()")
            sb.AppendLine("        Get")
            sb.AppendLine($"            If _{arr.Key} Is Nothing Then _{arr.Key} = New {t}() {{{String.Join(", ", items)}}}")
            sb.AppendLine($"            Return _{arr.Key}")
            sb.AppendLine("        End Get")
            sb.AppendLine("    End Property")
        Next
        sb.AppendLine()
        sb.AppendLine("End Class")
        File.WriteAllText(path, sb.ToString(), New UTF8Encoding(True))
    End Sub

    '======================================================================================
    ' Small helpers
    '======================================================================================
    Private ReadOnly _removed As New Dictionary(Of VbNode, List(Of KeyValuePair(Of String, String)))

    Private Sub RemoveProp(ByVal n As VbNode, ByVal key As String)
        For i = n.Props.Count - 1 To 0 Step -1
            If n.Props(i).Key.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                If Not _removed.ContainsKey(n) Then _removed(n) = New List(Of KeyValuePair(Of String, String))
                _removed(n).Add(n.Props(i))
                n.Props.RemoveAt(i)
            End If
        Next
    End Sub

    Private Function OriginalProp(ByVal n As VbNode, ByVal key As String) As String
        Dim v As String = n.Prop(key)
        If v IsNot Nothing Then Return v
        Dim lst As List(Of KeyValuePair(Of String, String)) = Nothing
        If _removed.TryGetValue(n, lst) Then
            For Each kv In lst
                If kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase) Then Return kv.Value
            Next
        End If
        Return Nothing
    End Function

    Public Shared Function ResolveType(ByVal fullName As String) As Type
        Dim t As Type = GetType(Form).Assembly.GetType(fullName)
        If t Is Nothing Then t = GetType(Aqua.TextBox).Assembly.GetType(fullName)
        Return t
    End Function

    Private Shared Function ShortName(ByVal t As Type) As String
        Return If(t Is Nothing, "?", t.Name)
    End Function

    Private Shared Function Choose3(ByVal i As Integer, ByVal a As String, ByVal b As String, ByVal c As String) As String
        Return ChooseN(i, a, b, c)
    End Function

    Private Shared Function Choose4(ByVal i As Integer, ByVal a As String, ByVal b As String, ByVal c As String, ByVal d As String) As String
        Return ChooseN(i, a, b, c, d)
    End Function

    Private Shared Function ChooseN(ByVal i As Integer, ParamArray names As String()) As String
        Return names(Math.Max(0, Math.Min(names.Length - 1, i)))
    End Function

End Class
