Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetButtonsSurface / GetButtonsMask and the frmResButtons
' resource form. Only the selected ("Click") state art is colour-keyed (13 ColorConstants
' variants); the unselected/normal state art is a single shared grey set regardless of Color,
' matching frmResButtons.frm exactly (imgLeftClick/imgMiddleClick/imgRightClick/imgFullClick are
' Image *arrays* indexed 0-12, but imgLeft/imgMiddle/imgRight/imgFull are singular controls).
'
' VB6's GetButtonsSurface also took a Value (Enabled) parameter that its Select Case never
' actually used -- a disabled Buttons control renders identically to an enabled one there too.
' That's a real VB6 behaviour (not something worth "fixing" silently), so it's preserved here:
' GetSurface below has no Enabled parameter at all.
Namespace Global.Aqua

    Public Enum ButtonsSegment
        Full
        Left
        Middle
        Right
    End Enum

    Friend Module ButtonsResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetSurface(ByVal segment As ButtonsSegment, ByVal color As ColorConstants, ByVal selected As Boolean) As Image
            If selected Then
                Dim idx As String = CInt(color).ToString("D2")
                Select Case segment
                    Case ButtonsSegment.Full : Return Load("btn_imgFullClick_" & idx)
                    Case ButtonsSegment.Left : Return Load("btn_imgLeftClick_" & idx)
                    Case ButtonsSegment.Middle : Return Load("btn_imgMiddleClick_" & idx)
                    Case Else : Return Load("btn_imgRightClick_" & idx)
                End Select
            Else
                Select Case segment
                    Case ButtonsSegment.Full : Return Load("btn_imgFull")
                    Case ButtonsSegment.Left : Return Load("btn_imgLeft")
                    Case ButtonsSegment.Middle : Return Load("btn_imgMiddle")
                    Case Else : Return Load("btn_imgRight")
                End Select
            End If
        End Function

        ''' <summary>Outline mask for the whole bar (port of GetButtonsMask): white = inside, black = cut.</summary>
        Public Function GetMask() As Image
            Return Load("btn_imgMask")
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.ButtonsRes." & key & ".bmp")
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
