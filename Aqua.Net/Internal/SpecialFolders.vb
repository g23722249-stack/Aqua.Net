Option Strict On
Option Explicit On

Imports System.IO
Imports System.Runtime.InteropServices

' Windows Explorer's "quick access" style special folders (Desktop/Downloads/Documents/Pictures/
' Music/Videos). VB6's DeskTop.ctl (Control\DeskTop.ctl) hard-coded just Desktop + My Document
' under its own dedicated TreeView control; that control was never ported as its own class --
' instead its one useful idea (letting the user jump straight to a well-known shell folder instead
' of drilling down through drives) was folded directly into DriveListBox.vb (Desktop) and
' DirListBox.vb (the full quick-access set), which already had the tree/lazy-expand machinery this
' needed.
Namespace Global.Aqua

    Friend Module SpecialFolders

        Public Enum QuickAccessFolder
            Desktop
            Downloads
            Documents
            Pictures
            Music
            Videos
        End Enum

        Public Function GetDisplayName(ByVal folder As QuickAccessFolder) As String
            Select Case folder
                Case QuickAccessFolder.Desktop : Return "桌面"
                Case QuickAccessFolder.Downloads : Return "下載"
                Case QuickAccessFolder.Documents : Return "文件"
                Case QuickAccessFolder.Pictures : Return "圖片"
                Case QuickAccessFolder.Music : Return "音樂"
                Case QuickAccessFolder.Videos : Return "影片"
                Case Else : Return folder.ToString()
            End Select
        End Function

        ''' <summary>Real on-disk path for a quick-access folder. Desktop/Documents/Pictures/Music/
        ''' Videos all have a matching Environment.SpecialFolder entry; Downloads does not (it was
        ''' never given one), so that one goes through SHGetKnownFolderPath instead, the same API
        ''' Explorer itself uses to resolve it.</summary>
        Public Function GetPath(ByVal folder As QuickAccessFolder) As String
            Select Case folder
                Case QuickAccessFolder.Desktop : Return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                Case QuickAccessFolder.Documents : Return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                Case QuickAccessFolder.Pictures : Return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                Case QuickAccessFolder.Music : Return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                Case QuickAccessFolder.Videos : Return GetShellFolderPath("My Video", "Videos")
                Case QuickAccessFolder.Downloads : Return GetDownloadsPath()
                Case Else : Return ""
            End Select
        End Function

        ''' <summary>All six quick-access folders, in the same order Windows Explorer's own
        ''' navigation pane lists them by default.</summary>
        Public Function AllQuickAccessFolders() As QuickAccessFolder()
            Return New QuickAccessFolder() {
                QuickAccessFolder.Desktop, QuickAccessFolder.Downloads, QuickAccessFolder.Documents,
                QuickAccessFolder.Pictures, QuickAccessFolder.Music, QuickAccessFolder.Videos}
        End Function

        Private ReadOnly FolderIdDownloads As New Guid("374DE290-123F-4565-9164-39C4925E467B")

        <DllImport("shell32.dll", CharSet:=CharSet.Unicode, PreserveSig:=True)>
        Private Function SHGetKnownFolderPath(ByRef rfid As Guid, ByVal dwFlags As UInteger, ByVal hToken As IntPtr, ByRef ppszPath As IntPtr) As Integer
        End Function

        Private Function GetDownloadsPath() As String
            Dim ptr As IntPtr = IntPtr.Zero
            Try
                Dim guid As Guid = FolderIdDownloads
                If SHGetKnownFolderPath(guid, 0, IntPtr.Zero, ptr) = 0 AndAlso ptr <> IntPtr.Zero Then
                    Return Marshal.PtrToStringUni(ptr)
                End If
            Catch
                ' Fall through to the profile-relative guess below.
            Finally
                If ptr <> IntPtr.Zero Then Marshal.FreeCoTaskMem(ptr)
            End Try
            Return Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Downloads")
        End Function

        ''' <summary>net35's Environment.SpecialFolder here doesn't carry every modern member (e.g.
        ''' MyVideos/UserProfile) -- the pre-Vista "Shell Folders" registry key both target
        ''' frameworks (and VB6's own Registry-based DeskTop.ctl) can read directly sidesteps that,
        ''' with an env-var-relative guess if the value is missing entirely.</summary>
        Private Function GetShellFolderPath(ByVal valueName As String, ByVal fallbackRelativeToProfile As String) As String
            Try
                Using key As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders")
                    If key IsNot Nothing Then
                        Dim value As String = TryCast(key.GetValue(valueName), String)
                        If Not String.IsNullOrEmpty(value) Then Return value
                    End If
                End Using
            Catch
            End Try
            Return Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), fallbackRelativeToProfile)
        End Function

    End Module

End Namespace
