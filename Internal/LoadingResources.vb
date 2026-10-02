Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetLoadingSurface and the frmResLoading resource form:
' one repeating striped bar texture per accent colour.
Namespace Global.Aqua

    Friend Module LoadingResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetBar(ByVal color As ColorConstants) As Image
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return Load("ld_imgBar_" & i.ToString("00"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.LoadingRes." & key & ".bmp")
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
