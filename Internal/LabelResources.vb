Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetLabelSurface and the frmResLabel resource form.
' The two mask bitmaps (imgLabel{Obtuseness|Rectangle}Mask) are not ported: in VB6 they only
' fed a CreateFromPicture/SetWindowRgn call to round the control's corners for the Obtuseness
' style, which RegionUtil.CreateObtusenessRegion now reproduces procedurally (see Label.vb).
Namespace Global.Aqua

    ''' <summary>Label surface visual state (VB6 enumControlState subset used by GetLabelSurface).</summary>
    Friend Enum LabelState
        Disable = 0
        Normal = 1
        Click = 2   ' accent-coloured surface
    End Enum

    Friend Module LabelResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        ''' <summary>Background surface for a state/colour/style (port of GetLabelSurface). Disable/Normal ignore colour.</summary>
        Public Function GetSurface(ByVal state As LabelState, ByVal color As ColorConstants, ByVal style As LabelStyle) As Image
            Dim shape As String = If(style = LabelStyle.Obtuseness, "obtuse", "rect")
            Select Case state
                Case LabelState.Disable
                    Return Load("lbl_" & shape & "_disable")
                Case LabelState.Click
                    Return Load("lbl_" & shape & "_click_" & Cc(color))
                Case Else ' Normal
                    Return Load("lbl_" & shape & "_normal")
            End Select
        End Function

        Private Function Cc(ByVal color As ColorConstants) As String
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return i.ToString("00")
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.LabelRes." & key & ".bmp")
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
