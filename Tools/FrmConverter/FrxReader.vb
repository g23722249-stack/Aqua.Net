Imports System.Drawing
Imports System.IO
Imports System.Text

''' <summary>
''' Reads binary property values out of VB6 .frx files. A picture blob is
'''   DWORD blobLength, [16-byte StdPicture CLSID], "lt\0\0", DWORD dataLength, data (BMP/ICO/GIF/JPEG/...)
''' (same format as Tools\Export-FrxImages.ps1). Other blobs (multi-line Text, List ...) are read as
''' length-prefixed ANSI strings where possible.
''' </summary>
Public NotInheritable Class FrxReader

    Private Shared ReadOnly PicGuid As Byte() = {&H4, &H52, &HE3, &HB, &H91, &H8F, &HCE, &H11, &H9D, &HE3, &H0, &HAA, &H0, &H4B, &HB8, &H51}
    Private ReadOnly _cache As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _enc As Encoding

    Public Sub New(ByVal enc As Encoding)
        _enc = enc
    End Sub

    Private Function Bytes(ByVal path As String) As Byte()
        Dim b As Byte() = Nothing
        If Not _cache.TryGetValue(path, b) Then
            b = If(File.Exists(path), File.ReadAllBytes(path), Nothing)
            _cache(path) = b
        End If
        Return b
    End Function

    ''' <summary>The embedded image file bytes, or Nothing if the blob isn't a (non-empty) picture.</summary>
    Public Function PictureBytes(ByVal frxPath As String, ByVal offset As Integer) As Byte()
        Dim frx As Byte() = Bytes(frxPath)
        If frx Is Nothing OrElse offset + 12 > frx.Length Then Return Nothing
        Dim p As Integer = offset + 4
        If Matches(frx, p, PicGuid) Then p += 16
        If Not Matches(frx, p, New Byte() {&H6C, &H74, 0, 0}) Then Return Nothing
        Dim len As Integer = BitConverter.ToInt32(frx, p + 4)
        p += 8
        If len <= 0 OrElse p + len > frx.Length Then Return Nothing
        Dim out(len - 1) As Byte
        Array.Copy(frx, p, out, 0, len)
        Return out
    End Function

    ''' <summary>A non-picture blob as text (DWORD or WORD length prefix), or Nothing.</summary>
    Public Function Text(ByVal frxPath As String, ByVal offset As Integer) As String
        Dim frx As Byte() = Bytes(frxPath)
        If frx Is Nothing OrElse offset + 4 > frx.Length Then Return Nothing
        Dim len As Integer = BitConverter.ToInt32(frx, offset)
        If len >= 0 AndAlso offset + 4 + len <= frx.Length AndAlso len < 1_000_000 Then Return _enc.GetString(frx, offset + 4, len)
        Dim len2 As Integer = BitConverter.ToUInt16(frx, offset)
        If offset + 2 + len2 <= frx.Length Then Return _enc.GetString(frx, offset + 2, len2)
        Return Nothing
    End Function

    Private Shared Function Matches(ByVal buf As Byte(), ByVal at As Integer, ByVal pattern As Byte()) As Boolean
        If at + pattern.Length > buf.Length Then Return False
        For i = 0 To pattern.Length - 1
            If buf(at + i) <> pattern(i) Then Return False
        Next
        Return True
    End Function

    Public Shared Function IsIcon(ByVal d As Byte()) As Boolean
        Return d.Length >= 4 AndAlso d(0) = 0 AndAlso d(1) = 0 AndAlso (d(2) = 1 OrElse d(2) = 2) AndAlso d(3) = 0
    End Function

    Public Shared Function IsMetafile(ByVal d As Byte()) As Boolean
        If d.Length >= 4 AndAlso d(0) = &HD7 AndAlso d(1) = &HCD AndAlso d(2) = &HC6 AndAlso d(3) = &H9A Then Return True
        Return d.Length >= 44 AndAlso d(40) = &H20 AndAlso d(41) = &H45 AndAlso d(42) = &H4D AndAlso d(43) = &H46
    End Function

    ''' <summary>Decodes to a standalone Bitmap (icons via their largest frame). Nothing for metafiles/garbage.</summary>
    Public Shared Function ToBitmap(ByVal d As Byte()) As Bitmap
        Try
            If IsMetafile(d) Then Return Nothing
            Using ms As New MemoryStream(d)
                If IsIcon(d) Then
                    Using ic As New Icon(ms)
                        Return ic.ToBitmap()
                    End Using
                End If
                Using img As Image = Image.FromStream(ms)
                    Return New Bitmap(img)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Public Shared Function ToIcon(ByVal d As Byte()) As Icon
        Try
            If Not IsIcon(d) Then Return Nothing
            Using ms As New MemoryStream(d)
                Return New Icon(ms)
            End Using
        Catch
            Return Nothing
        End Try
    End Function

End Class
