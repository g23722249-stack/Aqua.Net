Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetPageHeadSurface / GetPageHeadMask /
' GetPageTabLineSurface / GetPageSheetBackground / GetTabLineColor and the frmResPageHead /
' frmResPageSheet resource forms. Tab surfaces are the original 81x27 bitmaps with their
' shape mask applied (VB6 used the mask as a window Region with black = transparent), so
' the tabs keep their shaped edges.
Namespace Global.Aqua

    Friend Module PageResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        ' solid tab-underline colours (port of GetTabLineColor)
        Private ReadOnly _tabLineColors As Color() = {
            Color.FromArgb(15, 94, 193), Color.FromArgb(167, 140, 71), Color.FromArgb(125, 97, 61),
            Color.FromArgb(38, 163, 199), Color.FromArgb(0, 0, 53), Color.FromArgb(100, 92, 76),
            Color.FromArgb(114, 15, 193), Color.FromArgb(95, 116, 143), Color.FromArgb(129, 167, 71),
            Color.FromArgb(208, 108, 0), Color.FromArgb(199, 38, 163), Color.FromArgb(193, 15, 70),
            Color.FromArgb(173, 15, 193)}

        Public Function TabLineColor(ByVal color As ColorConstants) As Color
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return _tabLineColors(i)
        End Function

        ''' <summary>A tab-head surface with its shape mask applied (GetPageHeadSurface + GetPageHeadMask).</summary>
        Public Function GetTabSurface(ByVal style As PageStyle, ByVal color As ColorConstants, ByVal selected As Boolean) As Image
            Dim o As String = Orient(style)
            Dim key As String = "tab:" & o & ":" & If(selected, Cc(color), "n")
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Dim surfName As String = If(selected, "img" & o & "Click_" & Cc(color), "img" & o)
                Dim surf As Bitmap = LoadPageHead(surfName)
                Dim mask As Bitmap = LoadPageHead("img" & o & "Mask")
                img = Compose(surf, mask)
                _cache(key) = img
                Return img
            End SyncLock
        End Function

        ''' <summary>The tab underline surface (GetPageTabLineSurface). H for top/bottom, V for left/right.</summary>
        Public Function GetTabLine(ByVal style As PageStyle, ByVal color As ColorConstants, ByVal selected As Boolean) As Image
            Dim horizontal As Boolean = (style = PageStyle.Top OrElse style = PageStyle.Bottom)
            Dim prefix As String = If(horizontal, "imgH", "imgV")
            Dim name As String = If(selected, prefix & "Selected_" & Cc(color), prefix & "UnSelected")
            Return LoadPageSheet(name)
        End Function

        ''' <summary>Page-body background texture (GetPageSheetBackground).</summary>
        Public Function GetSheetBackground() As Image
            Return LoadPageSheet("imgBackground")
        End Function

        Private Function Orient(ByVal style As PageStyle) As String
            Select Case style
                Case PageStyle.Top : Return "Up"
                Case PageStyle.Bottom : Return "Down"
                Case PageStyle.Left : Return "Left"
                Case Else : Return "Right"
            End Select
        End Function

        Private Function Cc(ByVal color As ColorConstants) As String
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return i.ToString("00")
        End Function

        ''' <summary>Combine a surface with its mask: where the mask is black the result is transparent.</summary>
        Private Function Compose(ByVal surface As Bitmap, ByVal mask As Bitmap) As Bitmap
            If surface Is Nothing Then Return Nothing
            Dim w As Integer = surface.Width, h As Integer = surface.Height
            Dim result As New Bitmap(w, h, Imaging.PixelFormat.Format32bppArgb)
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    ' Use the mask's grey level as alpha (mask is greyscale: black=outside=0,
                    ' white=inside=255, grey=anti-aliased edge) for smooth shaped edges.
                    Dim a As Integer = 255
                    If mask IsNot Nothing AndAlso x < mask.Width AndAlso y < mask.Height Then
                        a = mask.GetPixel(x, y).R
                    End If
                    Dim sp As Color = surface.GetPixel(x, y)
                    result.SetPixel(x, y, Color.FromArgb(a, sp.R, sp.G, sp.B))
                Next
            Next
            Return result
        End Function

        Private Function LoadPageHead(ByVal key As String) As Bitmap
            Return LoadRaw("Aqua.PageHeadRes." & key & ".bmp", "ph:" & key)
        End Function

        Private Function LoadPageSheet(ByVal key As String) As Image
            Return LoadRaw("Aqua.PageSheetRes." & key & ".bmp", "ps:" & key)
        End Function

        Private Function LoadRaw(ByVal resName As String, ByVal cacheKey As String) As Bitmap
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(cacheKey, img) Then Return DirectCast(img, Bitmap)
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName)
                    If s Is Nothing Then Return Nothing
                    ' Clone to a stream-independent bitmap: a Bitmap built directly from a
                    ' stream faults (GDI+ OutOfMemoryException) once the stream is closed.
                    Dim bmp As Bitmap
                    Using tmp As New Bitmap(s)
                        bmp = New Bitmap(tmp)
                    End Using
                    _cache(cacheKey) = bmp
                    Return bmp
                End Using
            End SyncLock
        End Function

    End Module

End Namespace
