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

        ''' <summary>
        ''' The VB6 title-bar bitmaps have rounded top corners painted over a magenta key colour
        ''' (and anti-aliased into it, so the arc pixels are purple-tinted). Rounded windows used to
        ''' hide them behind their Region, but square windows (RoundCorners = False) showed the
        ''' magenta. Fill each corner block with the same row's colour just inside it: the bar
        ''' becomes square, square windows draw their own frame line, rounded windows are still
        ''' clipped round by their Region -- no magenta or purple fringe either way.
        ''' </summary>
        Private Sub SquareTitleCorners(ByVal bmp As Bitmap)
            Dim isKey = Function(c As Color) c.R > 240 AndAlso c.G < 20 AndAlso c.B > 240
            Dim k As Integer = 0
            While k < bmp.Width \ 2 AndAlso isKey(bmp.GetPixel(k, 0))
                k += 1
            End While
            If k = 0 Then Return
            k = Math.Min(bmp.Width \ 2 - 1, k + 2) ' the arc's anti-aliased pixels sit just past the key run
            For y As Integer = 0 To Math.Min(bmp.Height - 1, k - 1)
                Dim leftC = bmp.GetPixel(k, y), rightC = bmp.GetPixel(bmp.Width - 1 - k, y)
                For x As Integer = 0 To k - 1
                    bmp.SetPixel(x, y, leftC)
                    bmp.SetPixel(bmp.Width - 1 - x, y, rightC)
                Next
            Next
        End Sub

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.FormChromeRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)
                        img = New Bitmap(tmp)
                    End Using
                    If key.StartsWith("frm_imgTitle") Then SquareTitleCorners(DirectCast(img, Bitmap))
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
