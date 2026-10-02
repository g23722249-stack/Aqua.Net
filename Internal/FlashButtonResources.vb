Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetButtonSurface / GetButtonMask / GetButtonFlashFlame and
' the frmResButton resource form. Normal/Disabled art is a single shared set regardless of Color;
' Hover, Click, and all 12 flash-sweep frames are colour-keyed (13 ColorConstants variants each),
' matching frmResButton.frm (imgButtonHover/imgButtonClick/imgbuttonFlash_0..11 are Image arrays
' indexed 0-12; imgButtonNormal/imgButtonDisble/imgButtonMask are singular controls).
Namespace Global.Aqua

    Friend Module FlashButtonResources

        Public Const FlashFrameCount As Integer = 12

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetMask() As Image
            Return Load("fb_imgButtonMask")
        End Function

        Public Function GetNormal() As Image
            Return Load("fb_imgButtonNormal")
        End Function

        Public Function GetDisabled() As Image
            Return Load("fb_imgButtonDisble")   ' matches the source bitmap's own name (VB6 typo, kept for the resource key)
        End Function

        Public Function GetHover(ByVal color As ColorConstants) As Image
            Return Load("fb_imgButtonHover_" & CInt(color).ToString("D2"))
        End Function

        Public Function GetClick(ByVal color As ColorConstants) As Image
            Return Load("fb_imgButtonClick_" & CInt(color).ToString("D2"))
        End Function

        ''' <summary>Frame 0-11 of the flash sweep; Nothing outside that range (VB6's Select Case
        ''' FlashIndex left FlashIndex outside 0-11 unhandled, i.e. no picture that tick).</summary>
        Public Function GetFlashFrame(ByVal color As ColorConstants, ByVal frameIndex As Integer) As Image
            If frameIndex < 0 OrElse frameIndex >= FlashFrameCount Then Return Nothing
            Return Load("fb_imgbuttonFlash_" & frameIndex & "_" & CInt(color).ToString("D2"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.FlashButtonRes." & key & ".bmp")
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
