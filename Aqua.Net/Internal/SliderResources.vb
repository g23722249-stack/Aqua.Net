Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetSliderBackgroundSurface / GetSliderTickSurface and the
' frmResSlider resource form. Thumb art is six image sets (one per orientation/tick-style combo),
' 13 colour variants each; the two mask bitmaps aren't used: instead of the VB6 CreateFromPicture/
' SetWindowRgn(vbWhite) trick, the white surround is made transparent when an image is loaded.
Namespace Global.Aqua

    Friend Module SliderResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        ''' <summary>Track/groove background (port of GetSliderBackgroundSurface).</summary>
        Public Function GetBackground(ByVal orientation As OrientationMode) As Image
            Return Load(If(orientation = OrientationMode.Horizontal, "sld_imgHBar", "sld_imgVBar"))
        End Function

        ''' <summary>Thumb art (port of GetSliderTickSurface): H/V = NoTicks, U/D = horizontal Top/Bottom, L/R = vertical Top/Bottom.</summary>
        Public Function GetThumb(ByVal orientation As OrientationMode, ByVal color As ColorConstants, ByVal tick As SliderTickMode) As Image
            Dim prefix As String
            If orientation = OrientationMode.Horizontal Then
                Select Case tick
                    Case SliderTickMode.TopLeft : prefix = "sld_imgU_"
                    Case SliderTickMode.BottomRight : prefix = "sld_imgD_"
                    Case Else : prefix = "sld_imgH_"
                End Select
            Else
                Select Case tick
                    Case SliderTickMode.TopLeft : prefix = "sld_imgL_"
                    Case SliderTickMode.BottomRight : prefix = "sld_imgR_"
                    Case Else : prefix = "sld_imgV_"
                End Select
            End If
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return Load(prefix & i.ToString("00"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.SliderRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = ClearOuterWhite(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

        ''' <summary>A 32-bit copy with the white surround made transparent: VB6 keyed vbWhite out of the
        ''' control's window region. Only white connected to the image edge is cleared (flood fill), so
        ''' a white highlight inside the art survives.</summary>
        Private Function ClearOuterWhite(ByVal src As Bitmap) As Bitmap
            Dim bmp As New Bitmap(src.Width, src.Height, Imaging.PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.DrawImage(src, New Rectangle(0, 0, src.Width, src.Height))
            End Using
            Dim w As Integer = bmp.Width, h As Integer = bmp.Height
            Dim white As Integer = Color.White.ToArgb()
            Dim seen(w * h - 1) As Boolean
            Dim todo As New Stack(Of Point)()
            For x As Integer = 0 To w - 1
                todo.Push(New Point(x, 0)) : todo.Push(New Point(x, h - 1))
            Next
            For y As Integer = 0 To h - 1
                todo.Push(New Point(0, y)) : todo.Push(New Point(w - 1, y))
            Next
            While todo.Count > 0
                Dim p As Point = todo.Pop()
                If p.X < 0 OrElse p.Y < 0 OrElse p.X >= w OrElse p.Y >= h Then Continue While
                Dim i As Integer = p.Y * w + p.X
                If seen(i) Then Continue While
                seen(i) = True
                If bmp.GetPixel(p.X, p.Y).ToArgb() <> white Then Continue While
                bmp.SetPixel(p.X, p.Y, Color.Transparent)
                todo.Push(New Point(p.X + 1, p.Y)) : todo.Push(New Point(p.X - 1, p.Y))
                todo.Push(New Point(p.X, p.Y + 1)) : todo.Push(New Point(p.X, p.Y - 1))
            End While
            Return bmp
        End Function

    End Module

End Namespace
