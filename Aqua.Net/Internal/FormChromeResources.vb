Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for frmResForm (LibResource.GetFormTitleBarSurface / GetControlButtonSurface)
' plus AquaForm/iForm's own embedded apple/size-grip/corner-grip art.
Namespace Global.Aqua

    Public Enum FormControlBox
        Close
        Minimize
        Maximize
    End Enum

    Friend Enum FormCorner
        TopLeft
        TopRight
        BottomLeft
        BottomRight
    End Enum

    Friend Module FormChromeResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetTitleBar(ByVal active As Boolean) As Image
            Return Load(If(active, "frm_imgTitleActivate", "frm_imgTitleDeactivate"))
        End Function

        Public Function GetBackground() As Image
            Return Load("frm_imgBackground")
        End Function

        ''' <summary>Mirrors LibResource.GetControlButtonSurface(active, mode, hover): when the
        ''' window is inactive, ALL THREE buttons share the one grey "deactivated" dot regardless
        ''' of which button it is -- a real VB6 behaviour, not a shortcut taken here.</summary>
        Public Function GetControlButton(ByVal active As Boolean, ByVal box As FormControlBox, ByVal hover As Boolean) As Image
            If Not active Then Return Load("frm_imgCtlDeactivate")
            Select Case box
                Case FormControlBox.Close : Return Load(If(hover, "frm_imgCtlCloseMouseIn", "frm_imgCtlCloseMouseOut"))
                Case FormControlBox.Minimize : Return Load(If(hover, "frm_imgCtlMinMouseIn", "frm_imgCtlMinMouseOut"))
                Case Else : Return Load(If(hover, "frm_imgCtlMaxMouseIn", "frm_imgCtlMaxMouseOut"))
            End Select
        End Function

        Public Function GetSizeGrip() As Image
            Return Load("af_imgSize")
        End Function

        ''' <summary>iForm's four resize-corner glyphs (if_imgUL/UR/DL/DR).</summary>
        Public Function GetCornerGrip(ByVal corner As FormCorner) As Image
            Select Case corner
                Case FormCorner.TopLeft : Return Load("if_imgUL")
                Case FormCorner.TopRight : Return Load("if_imgUR")
                Case FormCorner.BottomLeft : Return Load("if_imgDL")
                Case Else : Return Load("if_imgDR")
            End Select
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.FormChromeRes." & key & ".bmp")
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
