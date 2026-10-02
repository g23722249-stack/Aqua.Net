Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetProgressbarBackground / GetProgressbarSurface.
Namespace Global.Aqua

    Friend Module ProgressBarResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetBackground(ByVal orientation As OrientationMode) As Image
            Return Load(If(orientation = OrientationMode.Horizontal, "pb_imgHBackground", "pb_imgVBackground"))
        End Function

        Public Function GetFill(ByVal color As ColorConstants, ByVal orientation As OrientationMode) As Image
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Dim prefix As String = If(orientation = OrientationMode.Horizontal, "pb_imgHColor_", "pb_imgVColor_")
            Return Load(prefix & i.ToString("00"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.ProgressBarRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
