Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

Namespace Global.Aqua

    ''' <summary>
    ''' MediaList thumbnails kept on disk, so a list opened again (also after a restart) shows them at once
    ''' instead of decoding every photo anew (ThumbnailLoader reads and writes it on its worker threads).
    '''   - one file per thumbnail: Folder\ab\abcdef….thm = the original's size (two Int32) + a JPEG;
    '''   - the name is a hash of the media file's path, its last-write time and length, and the size asked
    '''     for: a changed file or another thumbnail size is another entry, a stale one is never read;
    '''   - a path on the cache folder's own drive is hashed relative to the cache folder, so the cache still
    '''     fits when the library and the cache move together (another drive letter, another folder);
    '''   - Trim keeps it under a size (the least recently used go first); Clear empties it.
    ''' Every failure is a miss: the thumbnail is made from the file as before.
    ''' </summary>
    Public NotInheritable Class ThumbnailDiskCache

        Private Const FileExtension As String = ".thm"
        Private Shared _folder As String

        Private Sub New()
        End Sub

        ''' <summary>The cache folder (made when the first thumbnail is written); Nothing or "" = no disk cache.</summary>
        Public Shared Property Folder As String
            Get
                Return _folder
            End Get
            Set(value As String)
                _folder = If(String.IsNullOrEmpty(value), Nothing, value)
            End Set
        End Property

        ''' <summary>The cached thumbnail of <paramref name="path"/> at <paramref name="box"/>, or Nothing.</summary>
        Friend Shared Function TryRead(ByVal path As String, ByVal box As Size, ByRef realSize As Size) As Bitmap
            Dim file As String = EntryFile(path, box)
            If file Is Nothing Then Return Nothing
            Try
                If Not IO.File.Exists(file) Then Return Nothing
                Dim data As Byte() = IO.File.ReadAllBytes(file)
                If data.Length <= 8 Then Return Nothing
                Dim w As Integer = BitConverter.ToInt32(data, 0), h As Integer = BitConverter.ToInt32(data, 4)
                Dim bmp As Bitmap
                Using ms As New MemoryStream(data, 8, data.Length - 8)
                    Using img As Image = Image.FromStream(ms)
                        bmp = New Bitmap(img)
                    End Using
                End Using
                realSize = New Size(w, h)
                IO.File.SetLastAccessTimeUtc(file, DateTime.UtcNow)   ' Trim drops the least recently used first
                Return bmp
            Catch ex As Exception When IsCacheError(ex)
                Return Nothing
            End Try
        End Function

        ''' <summary>Keeps <paramref name="bmp"/> as the thumbnail of <paramref name="path"/> at <paramref name="box"/>
        ''' (a temporary file first, then moved over: a half-written entry is never read).</summary>
        Friend Shared Sub Write(ByVal path As String, ByVal box As Size, ByVal bmp As Bitmap, ByVal realSize As Size)
            Dim file As String = EntryFile(path, box)
            If file Is Nothing Then Return
            Dim temp As String = file & "." & Guid.NewGuid().ToString("N") & ".tmp"
            Try
                Directory.CreateDirectory(IO.Path.GetDirectoryName(file))
                Using fs As New FileStream(temp, FileMode.CreateNew, FileAccess.Write)
                    fs.Write(BitConverter.GetBytes(realSize.Width), 0, 4)
                    fs.Write(BitConverter.GetBytes(realSize.Height), 0, 4)
                    bmp.Save(fs, JpegCodec(), JpegQuality())
                End Using
                IO.File.Move(temp, file, overwrite:=True)
            Catch ex As Exception When IsCacheError(ex)
                Try
                    If IO.File.Exists(temp) Then IO.File.Delete(temp)
                Catch ex2 As Exception When IsCacheError(ex2)
                End Try
            End Try
        End Sub

        ''' <summary>The bytes the cache holds now (0 when there is none).</summary>
        Public Shared Function Bytes(ByVal cacheFolder As String) As Long
            Dim total As Long = 0
            For Each f As FileInfo In Entries(cacheFolder)
                total += f.Length
            Next
            Return total
        End Function

        ''' <summary>The cache in <paramref name="cacheFolder"/> over <paramref name="limitBytes"/>: the least
        ''' recently used thumbnails go until it is at 80% of it (and stray temporary files from a write cut
        ''' short). Slow on a big cache: call it on a worker thread.</summary>
        Public Shared Sub Trim(ByVal cacheFolder As String, ByVal limitBytes As Long)
            Dim files As List(Of FileInfo) = Entries(cacheFolder)
            Dim total As Long = 0
            For Each f As FileInfo In files
                total += f.Length
            Next
            If total <= limitBytes Then Return
            files.Sort(Function(a, b) a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc))
            For Each f As FileInfo In files
                If total <= limitBytes * 8 \ 10 Then Exit For
                Try
                    Dim len As Long = f.Length
                    f.Delete()
                    total -= len
                Catch ex As Exception When IsCacheError(ex)
                End Try
            Next
        End Sub

        ''' <summary>Every thumbnail kept in <paramref name="cacheFolder"/> goes (the folder itself too).</summary>
        Public Shared Sub Clear(ByVal cacheFolder As String)
            If String.IsNullOrEmpty(cacheFolder) Then Return
            Try
                If Directory.Exists(cacheFolder) Then Directory.Delete(cacheFolder, recursive:=True)
            Catch ex As Exception When IsCacheError(ex)
            End Try
        End Sub

        Private Shared Function Entries(ByVal root As String) As List(Of FileInfo)
            Dim list As New List(Of FileInfo)
            If String.IsNullOrEmpty(root) Then Return list
            Try
                Dim d As New DirectoryInfo(root)
                If Not d.Exists Then Return list
                For Each f As FileInfo In d.EnumerateFiles("*", SearchOption.AllDirectories)
                    If f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) AndAlso f.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1) Then
                        Try
                            f.Delete()
                        Catch ex As Exception When IsCacheError(ex)
                        End Try
                    ElseIf f.Name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) Then
                        list.Add(f)
                    End If
                Next
            Catch ex As Exception When IsCacheError(ex)
            End Try
            Return list
        End Function

        ''' <summary>Folder\ab\abcdef….thm for this file at this size; Nothing when there is no cache or the
        ''' file is gone.</summary>
        Private Shared Function EntryFile(ByVal path As String, ByVal box As Size) As String
            Dim root As String = _folder
            If root Is Nothing OrElse String.IsNullOrEmpty(path) Then Return Nothing
            Try
                Dim fi As New FileInfo(path)
                If Not fi.Exists Then Return Nothing
                Dim name As String = fi.FullName
                Dim cacheRoot As String = IO.Path.GetFullPath(root)
                If String.Equals(IO.Path.GetPathRoot(name), IO.Path.GetPathRoot(cacheRoot), StringComparison.OrdinalIgnoreCase) Then
                    name = IO.Path.GetRelativePath(cacheRoot, name)   ' the same drive as the cache: from the cache, no drive letter
                End If
                Dim key As String = name.ToLowerInvariant() & "|" & fi.LastWriteTimeUtc.Ticks & "|" & fi.Length & "|" & box.Width & "x" & box.Height
                Dim hash As String
                Using sha As SHA1 = SHA1.Create()
                    hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant()
                End Using
                Return IO.Path.Combine(root, hash.Substring(0, 2), hash & FileExtension)
            Catch ex As Exception When IsCacheError(ex)
                Return Nothing
            End Try
        End Function

        Private Shared _jpeg As ImageCodecInfo

        Private Shared Function JpegCodec() As ImageCodecInfo
            If _jpeg Is Nothing Then
                For Each c As ImageCodecInfo In ImageCodecInfo.GetImageEncoders()
                    If c.FormatID = ImageFormat.Jpeg.Guid Then _jpeg = c
                Next
            End If
            Return _jpeg
        End Function

        Private Shared Function JpegQuality() As EncoderParameters
            Dim p As New EncoderParameters(1)
            p.Param(0) = New EncoderParameter(Imaging.Encoder.Quality, 90L)
            Return p
        End Function

        Private Shared Function IsCacheError(ByVal ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                   TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException OrElse
                   TypeOf ex Is OutOfMemoryException OrElse TypeOf ex Is Runtime.InteropServices.ExternalException OrElse
                   TypeOf ex Is Security.SecurityException
        End Function

    End Class

End Namespace
