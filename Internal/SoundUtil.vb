Option Strict On
Option Explicit On

Imports System.Runtime.InteropServices

' Managed replacement for Module\LibSound.bas's PlaySound (winmm.dll sndPlaySound, WAV/MID,
' asynchronous). PlaySoundW takes the same flags and needs no short-path workaround since it's
' Unicode from the start.
Namespace Global.Aqua

    Friend Module SoundUtil

        Private Const SND_ASYNC As Integer = &H1
        Private Const SND_FILENAME As Integer = &H20000
        Private Const SND_NODEFAULT As Integer = &H2

        <DllImport("winmm.dll", EntryPoint:="PlaySoundW", CharSet:=CharSet.Unicode)>
        Private Function PlaySoundW(ByVal pszSound As String, ByVal hmod As IntPtr, ByVal fdwSound As Integer) As Boolean
        End Function

        ''' <summary>Plays a .wav (or .mid) file asynchronously; silently does nothing for blank/missing/unsupported files.</summary>
        Public Sub PlaySound(ByVal fileName As String)
            If String.IsNullOrEmpty(fileName) OrElse fileName.Trim().Length = 0 Then Return   ' IsNullOrWhiteSpace needs .NET 4.0+; this project also targets net35
            Dim ext As String = IO.Path.GetExtension(fileName).TrimStart("."c).ToUpperInvariant()
            If ext <> "WAV" AndAlso ext <> "MID" Then Return
            If Not IO.File.Exists(fileName) Then Return
            Try
                PlaySoundW(fileName, IntPtr.Zero, SND_ASYNC Or SND_FILENAME Or SND_NODEFAULT)
            Catch
                ' matches the VB6 caller's On Error Resume Next around PlaySound
            End Try
        End Sub

    End Module

End Namespace
