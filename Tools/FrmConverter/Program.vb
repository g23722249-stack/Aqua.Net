Imports System.Globalization
Imports System.IO
Imports System.Text

' FrmConverter -- converts the forms of a VB6 project into WinForms designer files.
'
'   FrmConverter <project.vbp> <net-root> [--only frmA,frmB] [--skip frmC] [--codepage 950]
'
' For every Form= in the .vbp it looks under <net-root> for "<VB_Name>.vb" (the stub the skeleton
' created, which also decides the target folder/project) and writes next to it:
'   <VB_Name>.Designer.vb   controls + properties (opens in the VS designer)
'   <VB_Name>.resx          the .frx pictures used by the designer
'   <VB_Name>.Vb6.vb        Sub New, list-style content (menus, headers, toolbar items ...), control
'                           arrays and the TODO list
' The form's own <VB_Name>.vb (hand-written code) is never touched. A summary goes to
' <net-root>\ConversionReport.md.
'
' Forms listed in <net-root>\FrmConverter.keep (one VB_Name per line, "#" starts a comment) are
' maintained by hand in the VS designer from then on and are never regenerated -- the converter is
' only the starting point of a form's .Designer.vb.
Module Program

    Function Main(ByVal args As String()) As Integer
        If args.Length < 2 Then
            Console.WriteLine("usage: FrmConverter <project.vbp> <net-root> [--only a,b] [--skip c] [--codepage 950]")
            Return 2
        End If
        Dim vbp As String = Path.GetFullPath(args(0))
        Dim netRoot As String = Path.GetFullPath(args(1))
        Dim only As HashSet(Of String) = Nothing
        Dim skip As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim codepage As Integer = CultureInfo.CurrentCulture.TextInfo.ANSICodePage
        For i = 2 To args.Length - 1
            Select Case args(i).ToLowerInvariant()
                Case "--only" : i += 1 : only = New HashSet(Of String)(args(i).Split(","c), StringComparer.OrdinalIgnoreCase)
                Case "--skip" : i += 1 : For Each s In args(i).Split(","c) : skip.Add(s) : Next
                Case "--codepage" : i += 1 : codepage = Integer.Parse(args(i))
            End Select
        Next
        Dim keepFile As String = Path.Combine(netRoot, "FrmConverter.keep")
        Dim keep As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If File.Exists(keepFile) Then
            For Each k In File.ReadAllLines(keepFile)
                Dim name As String = k.Split("#"c)(0).Trim()
                If name <> "" Then keep.Add(name)
            Next
        End If
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Dim enc As Encoding = Encoding.GetEncoding(codepage)
        Dim frx As New FrxReader(enc)
        Dim vbpDir As String = Path.GetDirectoryName(vbp)

        Dim stubs = Directory.EnumerateFiles(netRoot, "*.vb", SearchOption.AllDirectories).
            Where(Function(f) Not f.Contains("\bin\") AndAlso Not f.Contains("\obj\") AndAlso
                              Not f.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase) AndAlso
                              Not f.EndsWith(".Vb6.vb", StringComparison.OrdinalIgnoreCase)).
            ToLookup(Function(f) Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)

        Dim report As New StringBuilder()
        report.AppendLine("# 表單轉換報告")
        report.AppendLine()
        report.AppendLine($"由 `Tools\FrmConverter` 從 `{vbp}` 產生（{DateTime.Now:yyyy-MM-dd HH:mm}）。每個表單的 TODO 也寫在它的 `.Vb6.vb` 開頭。")
        report.AppendLine()
        report.AppendLine("| 表單 | 基底 | 控制項 | 佔位 | 待辦 | 位置 |")
        report.AppendLine("|---|---|---:|---:|---:|---|")
        Dim details As New StringBuilder()
        Dim total As Integer = 0, totalTodo As Integer = 0, failed As Integer = 0

        For Each line In File.ReadAllLines(vbp, enc)
            If Not line.StartsWith("Form=", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim frmPath As String = Path.GetFullPath(Path.Combine(vbpDir, line.Substring(5).Trim()))
            Dim vbName As String = Nothing
            Dim root As VbNode
            Try
                root = FrmParser.Parse(frmPath, enc, vbName)
            Catch ex As Exception
                Console.WriteLine($"  !! {frmPath}: {ex.Message}")
                failed += 1
                Continue For
            End Try
            If only IsNot Nothing AndAlso Not only.Contains(vbName) Then Continue For
            If skip.Contains(vbName) Then Continue For
            If keep.Contains(vbName) Then
                Console.WriteLine($"  == {vbName}: in FrmConverter.keep (designer maintained by hand), not regenerated")
                Continue For
            End If
            Dim targets = stubs(vbName).ToList()
            If targets.Count <> 1 Then
                Console.WriteLine($"  -- {vbName}: {If(targets.Count = 0, "no", "several")} {vbName}.vb under {netRoot}, skipped")
                Continue For
            End If
            Dim stub As String = targets(0)
            Dim dir As String = Path.GetDirectoryName(stub)
            Dim hasCtor As Boolean = File.ReadAllText(stub).IndexOf("Sub New(", StringComparison.OrdinalIgnoreCase) >= 0
            Dim conv As New FormConverter(root, vbName, frmPath, frx, hasCtor)
            Try
                conv.Convert(Path.Combine(dir, vbName & ".Designer.vb"), Path.Combine(dir, vbName & ".resx"),
                             Path.Combine(dir, vbName & ".Vb6.vb"), Path.GetRelativePath(Path.GetDirectoryName(vbpDir), frmPath))
            Catch ex As Exception
                Console.WriteLine($"  !! {vbName}: {ex}")
                failed += 1
                Continue For
            End Try
            total += 1
            totalTodo += conv.Todos.Count
            Dim relDir As String = dir.Substring(netRoot.Length).TrimStart("\"c)
            Console.WriteLine($"  {vbName,-24} {conv.BaseTypeName,-16} controls={conv.ControlCount,4} placeholders={conv.PlaceholderCount,2} todo={conv.Todos.Count,3}")
            report.AppendLine($"| `{vbName}` | {conv.BaseTypeName} | {conv.ControlCount} | {conv.PlaceholderCount} | {conv.Todos.Count} | {relDir} |")
            If conv.Todos.Count > 0 Then
                details.AppendLine($"### {vbName}")
                details.AppendLine()
                For Each t In conv.Todos
                    details.AppendLine("- " & t.Replace("|", "\|"))
                Next
                details.AppendLine()
            End If
        Next

        report.AppendLine()
        report.AppendLine($"共 {total} 個表單，{totalTodo} 項待辦。")
        report.AppendLine()
        report.AppendLine("## 待辦明細")
        report.AppendLine()
        report.Append(details)
        If only Is Nothing Then File.WriteAllText(Path.Combine(netRoot, "ConversionReport.md"), report.ToString(), New UTF8Encoding(True))
        Console.WriteLine($"converted {total} form(s), {totalTodo} TODO item(s), {failed} failure(s)")
        Return If(failed = 0, 0, 1)
    End Function

End Module
