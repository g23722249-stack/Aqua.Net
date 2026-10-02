Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Runtime.InteropServices

' Real Windows shell icons (drive/folder/file-type) via SHGetFileInfo -- what DriveListBox/
' DirListBox/FileListBox use for their tree/list icons, replacing the VB6 originals' hand-built
' per-drive-type / per-extension ImageList (drvRemovable/drvFixed/... bitmaps baked into
' frmResDriveListBox-style resource forms). This gets the user's actual current shell icon set
' instead of a fixed bitmap, which is both simpler to port and more visually correct.
Namespace Global.Aqua

    Friend Module ShellIcon

        Private Const SHGFI_ICON As Integer = &H100
        Private Const SHGFI_SMALLICON As Integer = &H1
        Private Const SHGFI_USEFILEATTRIBUTES As Integer = &H10
        Private Const FILE_ATTRIBUTE_DIRECTORY As Integer = &H10
        Private Const FILE_ATTRIBUTE_NORMAL As Integer = &H80

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
        Private Structure SHFILEINFO
            Public hIcon As IntPtr
            Public iIcon As Integer
            Public dwAttributes As Integer
            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
            Public szDisplayName As String
            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=80)>
            Public szTypeName As String
        End Structure

        <DllImport("shell32.dll", CharSet:=CharSet.Auto)>
        Private Function SHGetFileInfo(ByVal pszPath As String, ByVal dwFileAttributes As Integer,
                                       ByRef psfi As SHFILEINFO, ByVal cbSizeFileInfo As Integer,
                                       ByVal uFlags As Integer) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Function DestroyIcon(ByVal hIcon As IntPtr) As Boolean
        End Function

        ''' <summary>Small shell icon for a real, existing path (drive/folder/file).</summary>
        Public Function GetIcon(ByVal path As String) As Image
            Return GetIconCore(path, 0, useAttributes:=False)
        End Function

        ''' <summary>Small shell icon for a folder that may not exist yet (or to avoid hitting the
        ''' filesystem), keyed purely by the directory attribute.</summary>
        Public Function GetFolderIcon() As Image
            Return GetIconCore("folder", FILE_ATTRIBUTE_DIRECTORY, useAttributes:=True)
        End Function

        ''' <summary>Small shell icon for a file extension (e.g. ".txt"), without touching disk.</summary>
        Public Function GetExtensionIcon(ByVal extension As String) As Image
            Dim ext As String = If(String.IsNullOrEmpty(extension), ".", extension)
            If Not ext.StartsWith(".") Then ext = "." & ext
            Return GetIconCore("x" & ext, FILE_ATTRIBUTE_NORMAL, useAttributes:=True)
        End Function

        Private Function GetIconCore(ByVal path As String, ByVal attrs As Integer, ByVal useAttributes As Boolean) As Image
            Dim info As New SHFILEINFO()
            Dim flags As Integer = SHGFI_ICON Or SHGFI_SMALLICON
            If useAttributes Then flags = flags Or SHGFI_USEFILEATTRIBUTES
            Dim result = SHGetFileInfo(path, attrs, info, Marshal.SizeOf(GetType(SHFILEINFO)), flags)
            If result = IntPtr.Zero OrElse info.hIcon = IntPtr.Zero Then Return Nothing
            Try
                Using ico As Icon = Icon.FromHandle(info.hIcon)
                    Return DirectCast(ico.Clone(), Icon).ToBitmap()
                End Using
            Finally
                DestroyIcon(info.hIcon)
            End Try
        End Function

    End Module

End Namespace
