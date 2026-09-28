Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetPanelSurface and the frmResPanel resource form.
' Only the five "texture" styles have a surface bitmap; Flat/Container/Simulation are drawn
' procedurally instead (see Panel.vb), matching the VB6 GetPanelSurface Select Case exactly
' (those three cases fall through and return Nothing there too).
Namespace Global.Aqua

    Friend Module PanelResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetSurface(ByVal style As PanelStyleMode) As Image
            Select Case style
                Case PanelStyleMode.DarkSinking : Return Load("pnl_imgPanelStyle_00")
                Case PanelStyleMode.LightSinking : Return Load("pnl_imgPanelStyle_01")
                Case PanelStyleMode.SmoothSinking : Return Load("pnl_imgPanelStyle_02")
                Case PanelStyleMode.DarkGrid : Return Load("pnl_imgPanelStyle_03")
                Case PanelStyleMode.LightGrid : Return Load("pnl_imgPanelStyle_04")
                Case Else : Return Nothing
            End Select
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.PanelRes." & key & ".bmp")
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
