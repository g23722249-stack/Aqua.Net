Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetiTextBoxSurface and the frmResiTextBox resource form.
' Just two states (active/deactive); unlike Label/Header there's no per-colour variant.
Namespace Global.Aqua

    Friend Module ITextBoxResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetSurface(ByVal active As Boolean) As Image
            Return Load(If(active, "itb_active", "itb_deactive"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.ITextBoxRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
