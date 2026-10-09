Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Text

' 使用者資料存放:文件\PainterTool\
'   Palettes\*.gpl     使用者色票(GIMP Palette 格式,可直接匯入 GIMP / Krita / Inkscape 匯出的色票)
'   CustomColors.txt   自訂色(常用色區被改過的顏色),一行一個 #RRGGBB
'   RecentColors.txt   最近使用色,一行一個 #RRGGBB
Namespace Global.Aqua

    Friend Module PaletteStore

        Public ReadOnly Property RootFolder As String
            Get
                If Not String.IsNullOrEmpty(ColorPicker.UserDataFolder) Then Return ColorPicker.UserDataFolder
                Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PainterTool")
            End Get
        End Property

        Public ReadOnly Property PalettesFolder As String
            Get
                Return Path.Combine(RootFolder, "Palettes")
            End Get
        End Property

        ''' <summary>常用色區(3 列)使用者改過的顏色;檔案不存在 = 用內建預設。</summary>
        Public ReadOnly Property CustomColorsFile As String
            Get
                Return Path.Combine(RootFolder, "CustomColors.txt")
            End Get
        End Property

        ''' <summary>最近使用色(新到舊)。</summary>
        Public ReadOnly Property RecentColorsFile As String
            Get
                Return Path.Combine(RootFolder, "RecentColors.txt")
            End Get
        End Property

#Region "色彩清單(自訂色 / 最近使用色)"

        ''' <summary>一行一個 #RRGGBB;檔案不存在回傳 Nothing(呼叫端自行決定預設)。</summary>
        Public Function LoadColorList(ByVal file As String) As List(Of Color)
            If Not IO.File.Exists(file) Then Return Nothing
            Dim result As New List(Of Color)()
            For Each line As String In IO.File.ReadAllLines(file, Encoding.UTF8)
                Dim c As Color
                If ColorMath.TryParseHex(line, c) Then result.Add(c)
            Next
            Return result
        End Function

        Public Sub SaveColorList(ByVal file As String, ByVal colors As IList(Of Color))
            Directory.CreateDirectory(RootFolder)
            Dim lines(colors.Count - 1) As String
            For i As Integer = 0 To colors.Count - 1
                lines(i) = ColorMath.ToHex(colors(i))
            Next
            IO.File.WriteAllLines(file, lines, Encoding.UTF8)
        End Sub

        Public Sub DeleteFile(ByVal file As String)
            If IO.File.Exists(file) Then IO.File.Delete(file)
        End Sub

#End Region

#Region "色票"

        ''' <summary>讀取 Palettes 資料夾內所有 .gpl;單檔壞掉就略過,不影響其他色票。</summary>
        Public Function LoadUserPalettes() As List(Of ColorPalette)
            Dim result As New List(Of ColorPalette)()
            If Not Directory.Exists(PalettesFolder) Then Return result
            Dim files() As String = Directory.GetFiles(PalettesFolder, "*.gpl")
            Array.Sort(files, StringComparer.OrdinalIgnoreCase)
            For Each f As String In files
                Try
                    Dim p As ColorPalette = ParseFile(f)
                    p.FilePath = f
                    result.Add(p)
                Catch ex As IOException
                Catch ex As UnauthorizedAccessException
                End Try
            Next
            Return result
        End Function

        ''' <summary>寫回 .gpl;新色票依名稱配一個不重複的檔名。</summary>
        Public Sub SavePalette(ByVal p As ColorPalette)
            Directory.CreateDirectory(PalettesFolder)
            If p.FilePath Is Nothing Then p.FilePath = UniqueFilePath(p.Name)
            Dim sb As New StringBuilder()
            sb.AppendLine("GIMP Palette")
            sb.AppendLine("Name: " & p.Name)
            sb.AppendLine("Columns: 12")
            sb.AppendLine("#")
            For Each c As Color In p.Colors
                sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "{0,3} {1,3} {2,3}" & vbTab & "{3}", c.R, c.G, c.B, ColorMath.ToHex(c)))
            Next
            File.WriteAllText(p.FilePath, sb.ToString(), Encoding.UTF8)
        End Sub

        Public Sub DeletePalette(ByVal p As ColorPalette)
            If p.FilePath IsNot Nothing AndAlso File.Exists(p.FilePath) Then File.Delete(p.FilePath)
            p.FilePath = Nothing
        End Sub

        ''' <summary>匯入外部 .gpl 或十六進位色碼清單,另存一份到 Palettes 資料夾。</summary>
        Public Function Import(ByVal sourceFile As String) As ColorPalette
            Dim p As ColorPalette = ParseFile(sourceFile)
            If p.Colors.Count = 0 Then
                Throw New InvalidDataException("檔案中找不到任何顏色。")
            End If
            SavePalette(p)
            Return p
        End Function

        ''' <summary>
        ''' 解析色票檔。接受:
        '''   GIMP Palette 的「R G B 名稱」行與「Name:」行;
        '''   每行一個 #RRGGBB / RRGGBB(.txt / .hex)。
        ''' 名稱優先取 Name:,沒有則用檔名。
        ''' </summary>
        Public Function ParseFile(ByVal file As String) As ColorPalette
            Dim p As New ColorPalette() With {.Name = Path.GetFileNameWithoutExtension(file)}
            For Each raw As String In IO.File.ReadAllLines(file, Encoding.UTF8)
                Dim line As String = raw.Trim()
                If line.Length = 0 OrElse line.StartsWith("GIMP Palette", StringComparison.OrdinalIgnoreCase) Then Continue For
                If line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase) Then
                    Dim n As String = line.Substring(5).Trim()
                    If n.Length > 0 Then p.Name = n
                    Continue For
                End If
                If line.StartsWith("Columns:", StringComparison.OrdinalIgnoreCase) Then Continue For
                If line.StartsWith("#") AndAlso line.Length < 4 Then Continue For   ' gpl 的「#」分隔行

                Dim c As Color
                If TryParseRgbTriple(line, c) OrElse ColorMath.TryParseHex(FirstToken(line), c) Then p.Colors.Add(c)
            Next
            Return p
        End Function

        Private Function TryParseRgbTriple(ByVal line As String, ByRef c As Color) As Boolean
            Dim parts() As String = line.Split(New Char() {" "c, vbTab(0)}, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length < 3 Then Return False
            Dim r, g, b As Integer
            If Not (Integer.TryParse(parts(0), r) AndAlso Integer.TryParse(parts(1), g) AndAlso Integer.TryParse(parts(2), b)) Then Return False
            If r < 0 OrElse r > 255 OrElse g < 0 OrElse g > 255 OrElse b < 0 OrElse b > 255 Then Return False
            c = Color.FromArgb(r, g, b)
            Return True
        End Function

        Private Function FirstToken(ByVal line As String) As String
            Dim parts() As String = line.Split(New Char() {" "c, vbTab(0), ","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)
            Return If(parts.Length > 0, parts(0), "")
        End Function

        Private Function UniqueFilePath(ByVal name As String) As String
            Dim safe As New StringBuilder()
            Dim invalid() As Char = Path.GetInvalidFileNameChars()
            For Each ch As Char In If(String.IsNullOrEmpty(name), "palette", name)
                safe.Append(If(Array.IndexOf(invalid, ch) >= 0, "_"c, ch))
            Next
            Dim baseName As String = safe.ToString().Trim()
            If baseName.Length = 0 Then baseName = "palette"
            Dim candidate As String = Path.Combine(PalettesFolder, baseName & ".gpl")
            Dim n As Integer = 2
            While File.Exists(candidate)
                candidate = Path.Combine(PalettesFolder, baseName & " (" & n & ").gpl")
                n += 1
            End While
            Return candidate
        End Function

#End Region

    End Module

End Namespace
