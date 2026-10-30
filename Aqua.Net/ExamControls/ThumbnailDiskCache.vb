Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

Namespace Global.Aqua

    ''' <summary>
    ''' MediaList thumbnails kept on disk, so a list opened again (also after a restart) shows them at once
    ''' instead of decoding every photo anew (ThumbnailLoader reads and writes it on its worker threads).
    '''   - one file per thumbnail: Folder\ab\abcdef….thm = a header (what it is the thumbnail of: the media
    '''     file's name as hashed, its last-write time and length, the size asked for, photo or video, and
    '''     the original's size) + a JPEG; files of the first version have only the original's size;
    '''   - the name is a hash of the media file's path, its last-write time and length, and the size asked
    '''     for: a changed file or another thumbnail size is another entry, a stale one is never read;
    '''   - a path on the cache folder's own drive is hashed relative to the cache folder, so the cache still
    '''     fits when the library and the cache move together (another drive letter, another folder);
    '''   - RemoveInvalid drops the entries whose file is gone or changed (by the header); Fill makes the ones
    '''     missing for a list of files; Trim keeps it under a size (the least recently used go first); Clear
    '''     empties it.
    ''' Every failure is a miss: the thumbnail is made from the file as before.
    ''' </summary>
    Public NotInheritable Class ThumbnailDiskCache

        Private Const FileExtension As String = ".thm"
        Private Shared ReadOnly Magic As Byte() = Encoding.ASCII.GetBytes("ITC2")
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

        ''' <summary>What an entry is the thumbnail of (its file name is the hash of this).</summary>
        Private Structure EntryKey
            Public File As String      ' Folder\ab\abcdef….thm
            Public Name As String      ' the media file as hashed: relative to the cache folder, or full
            Public Ticks As Long       ' its last-write time (UTC)
            Public Length As Long
            Public Box As Size
        End Structure

        ''' <summary>The header of a version 2 entry.</summary>
        Private Structure EntryHeader
            Public Name As String
            Public Ticks As Long
            Public Length As Long
            Public Box As Size
            Public Video As Boolean
            Public RealSize As Size
            Public ImageOffset As Integer
        End Structure

        ''' <summary>The cached thumbnail of <paramref name="path"/> at <paramref name="box"/>, or Nothing.</summary>
        Friend Shared Function TryRead(ByVal path As String, ByVal box As Size, ByRef realSize As Size) As Bitmap
            Dim key As EntryKey
            If Not TryKey(_folder, path, box, key) Then Return Nothing
            Try
                If Not IO.File.Exists(key.File) Then Return Nothing
                Dim data As Byte() = IO.File.ReadAllBytes(key.File)
                Dim h As EntryHeader
                If Not TryHeader(data, h) Then Return Nothing
                Dim bmp As Bitmap
                Using ms As New MemoryStream(data, h.ImageOffset, data.Length - h.ImageOffset)
                    Using img As Image = Image.FromStream(ms)
                        bmp = New Bitmap(img)
                    End Using
                End Using
                realSize = h.RealSize
                IO.File.SetLastAccessTimeUtc(key.File, DateTime.UtcNow)   ' Trim drops the least recently used first
                Return bmp
            Catch ex As Exception When IsCacheError(ex)
                Return Nothing
            End Try
        End Function

        ''' <summary>Keeps <paramref name="bmp"/> as the thumbnail of <paramref name="path"/> at <paramref name="box"/>
        ''' (a temporary file first, then moved over: a half-written entry is never read).</summary>
        Friend Shared Sub Write(ByVal path As String, ByVal box As Size, ByVal bmp As Bitmap, ByVal realSize As Size, ByVal video As Boolean)
            Write(_folder, path, box, bmp, realSize, video)
        End Sub

        Private Shared Sub Write(ByVal root As String, ByVal path As String, ByVal box As Size, ByVal bmp As Bitmap, ByVal realSize As Size, ByVal video As Boolean)
            Dim key As EntryKey
            If Not TryKey(root, path, box, key) Then Return
            Dim temp As String = key.File & "." & Guid.NewGuid().ToString("N") & ".tmp"
            Try
                Directory.CreateDirectory(IO.Path.GetDirectoryName(key.File))
                Using fs As New FileStream(temp, FileMode.CreateNew, FileAccess.Write)
                    Using w As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=True)
                        Dim name As Byte() = Encoding.UTF8.GetBytes(key.Name)
                        w.Write(Magic)
                        w.Write(realSize.Width) : w.Write(realSize.Height)
                        w.Write(box.Width) : w.Write(box.Height)
                        w.Write(key.Ticks) : w.Write(key.Length)
                        w.Write(video)
                        w.Write(name.Length) : w.Write(name)
                    End Using
                    bmp.Save(fs, JpegCodec(), JpegQuality())
                End Using
                IO.File.Move(temp, key.File, overwrite:=True)
            Catch ex As Exception When IsCacheError(ex)
                Try
                    If IO.File.Exists(temp) Then IO.File.Delete(temp)
                Catch ex2 As Exception When IsCacheError(ex2)
                End Try
            End Try
        End Sub

        ''' <summary>The header of an entry's bytes; a first-version entry (the original's size, then the JPEG)
        ''' has only RealSize and ImageOffset. False when it is neither.</summary>
        Private Shared Function TryHeader(ByVal data As Byte(), ByRef h As EntryHeader) As Boolean
            h = Nothing
            Try
                If data.Length > Magic.Length AndAlso data.Take(Magic.Length).SequenceEqual(Magic) Then
                    Using r As New BinaryReader(New MemoryStream(data, Magic.Length, data.Length - Magic.Length), Encoding.UTF8)
                        h.RealSize = New Size(r.ReadInt32(), r.ReadInt32())
                        h.Box = New Size(r.ReadInt32(), r.ReadInt32())
                        h.Ticks = r.ReadInt64()
                        h.Length = r.ReadInt64()
                        h.Video = r.ReadBoolean()
                        Dim n As Integer = r.ReadInt32()
                        If n < 0 OrElse n > 4096 Then Return False
                        h.Name = Encoding.UTF8.GetString(r.ReadBytes(n))
                        h.ImageOffset = Magic.Length + CInt(r.BaseStream.Position)
                    End Using
                    Return h.ImageOffset < data.Length
                End If
                If data.Length <= 8 Then Return False
                h.RealSize = New Size(BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4))
                h.ImageOffset = 8
                Return True
            Catch ex As EndOfStreamException
                Return False
            End Try
        End Function

        '==================================================================================================
        ' Upkeep (設定: 重建 / 清除無效)
        '==================================================================================================
        ''' <summary>What RemoveInvalid found: the entries kept and removed, and the sizes the kept ones are for.</summary>
        Public Class CheckResult
            Public Valid As Integer
            Public Removed As Integer
            Public RemovedBytes As Long
            ''' <summary>The thumbnail sizes in use, for photos and for videos (Fill makes those).</summary>
            Public ImageBoxes As New HashSet(Of Size)
            Public VideoBoxes As New HashSet(Of Size)
        End Class

        ''' <summary>Removes from <paramref name="cacheFolder"/> every entry that no longer fits its file: the file
        ''' gone or changed (time, length), a header that doesn't match the entry's name, an entry of the first
        ''' version (no header to tell), and temporary files left over. <paramref name="progress"/> gets
        ''' (done, total). Slow on a big cache: call it on a worker thread.</summary>
        Public Shared Function RemoveInvalid(ByVal cacheFolder As String, Optional ByVal progress As Action(Of Integer, Integer) = Nothing,
                                             Optional ByVal cancel As CancellationToken = Nothing) As CheckResult
            Dim result As New CheckResult
            Dim files As List(Of FileInfo) = Entries(cacheFolder)
            Dim root As String = If(String.IsNullOrEmpty(cacheFolder), "", IO.Path.GetFullPath(cacheFolder))
            For i = 0 To files.Count - 1
                If cancel.IsCancellationRequested Then Exit For
                Dim f As FileInfo = files(i)
                Dim keep As Boolean = False
                Dim h As EntryHeader
                Try
                    Dim head(4095 + 64) As Byte
                    Dim n As Integer
                    Using fs As New FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        n = fs.Read(head, 0, head.Length)
                    End Using
                    ReDim Preserve head(n - 1)
                    If TryHeader(head, h) AndAlso h.Name IsNot Nothing Then
                        Dim media As String = If(IO.Path.IsPathRooted(h.Name), h.Name, IO.Path.GetFullPath(IO.Path.Combine(root, h.Name)))
                        Dim fi As New FileInfo(media)
                        keep = fi.Exists AndAlso fi.LastWriteTimeUtc.Ticks = h.Ticks AndAlso fi.Length = h.Length AndAlso
                               String.Equals(IO.Path.GetFileNameWithoutExtension(f.Name), HashOf(h.Name, h.Ticks, h.Length, h.Box), StringComparison.OrdinalIgnoreCase)
                    End If
                Catch ex As Exception When IsCacheError(ex)
                    keep = False
                End Try
                If keep Then
                    result.Valid += 1
                    If h.Video Then result.VideoBoxes.Add(h.Box) Else result.ImageBoxes.Add(h.Box)
                Else
                    Try
                        Dim len As Long = f.Length
                        f.Delete()
                        result.Removed += 1
                        result.RemovedBytes += len
                    Catch ex As Exception When IsCacheError(ex)
                    End Try
                End If
                If progress IsNot Nothing AndAlso (i Mod 200 = 0 OrElse i = files.Count - 1) Then progress(i + 1, files.Count)
            Next
            Return result
        End Function

        ''' <summary>What Fill did.</summary>
        Public Class FillResult
            Public Made As Integer
            ''' <summary>Already in the cache.</summary>
            Public Present As Integer
            ''' <summary>Photos whose own (EXIF) thumbnail is big enough: read as quickly as the cache, never kept.</summary>
            Public NotNeeded As Integer
            Public Failed As Integer
        End Class

        ''' <summary>Makes the entries missing in <paramref name="cacheFolder"/> for <paramref name="files"/>: each photo at
        ''' each of <paramref name="imageBoxes"/>, each video at each of <paramref name="videoBoxes"/>, as the list
        ''' would (ThumbnailLoader). <paramref name="workers"/> threads (STA: video thumbnails come from the shell),
        ''' below normal priority. <paramref name="progress"/> gets (done, total) from those threads.</summary>
        Public Shared Function Fill(ByVal cacheFolder As String, ByVal files As IEnumerable(Of String),
                                    ByVal imageBoxes As IEnumerable(Of Size), ByVal videoBoxes As IEnumerable(Of Size),
                                    Optional ByVal progress As Action(Of Integer, Integer) = Nothing,
                                    Optional ByVal cancel As CancellationToken = Nothing,
                                    Optional ByVal workers As Integer = 2) As FillResult
            Dim jobs As New List(Of (File As String, Box As Size, Video As Boolean))
            Dim imgs As List(Of Size) = imageBoxes.Where(Function(b) b.Width > 0 AndAlso b.Height > 0).Distinct().ToList()
            Dim vids As List(Of Size) = videoBoxes.Where(Function(b) b.Width > 0 AndAlso b.Height > 0).Distinct().ToList()
            For Each f In files
                Dim ext As String = IO.Path.GetExtension(If(f, "")).ToLowerInvariant()
                If Array.IndexOf(MediaViewerControl.ImageExts, ext) >= 0 Then
                    jobs.AddRange(imgs.Select(Function(b) (f, b, False)))
                ElseIf Array.IndexOf(MediaViewerControl.VideoExts, ext) >= 0 Then
                    jobs.AddRange(vids.Select(Function(b) (f, b, True)))
                End If
            Next
            Dim result As New FillResult
            Dim nextJob As Integer = -1, done As Integer = 0
            Dim work As ThreadStart =
                Sub()
                    Do
                        If cancel.IsCancellationRequested Then Return
                        Dim i As Integer = Interlocked.Increment(nextJob)
                        If i >= jobs.Count Then Return
                        Dim j = jobs(i)
                        Select Case MakeOne(cacheFolder, j.File, j.Box, j.Video)
                            Case FillOutcome.Made : Interlocked.Increment(result.Made)
                            Case FillOutcome.Present : Interlocked.Increment(result.Present)
                            Case FillOutcome.NotNeeded : Interlocked.Increment(result.NotNeeded)
                            Case Else : Interlocked.Increment(result.Failed)
                        End Select
                        Dim d As Integer = Interlocked.Increment(done)
                        If progress IsNot Nothing AndAlso (d Mod 25 = 0 OrElse d = jobs.Count) Then progress(d, jobs.Count)
                    Loop
                End Sub
            Dim threads As New List(Of Thread)
            For k = 1 To Math.Max(1, workers)
                Dim t As New Thread(work) With {.IsBackground = True, .Priority = ThreadPriority.BelowNormal, .Name = "Aqua thumbnail cache fill " & k}
                t.SetApartmentState(ApartmentState.STA)
                t.Start()
                threads.Add(t)
            Next
            For Each t In threads
                t.Join()
            Next
            progress?.Invoke(done, jobs.Count)
            Return result
        End Function

        Private Enum FillOutcome
            Made
            Present
            NotNeeded
            Failed
        End Enum

        Private Shared Function MakeOne(ByVal root As String, ByVal file As String, ByVal box As Size, ByVal video As Boolean) As FillOutcome
            Dim key As EntryKey
            If Not TryKey(root, file, box, key) Then Return FillOutcome.Failed
            If IO.File.Exists(key.File) Then Return FillOutcome.Present
            Dim realSize As Size
            Dim bmp As Bitmap = Nothing
            Try
                If video Then
                    bmp = ShellThumbnail.GetThumbnail(file, box.Width, box.Height)
                    If bmp IsNot Nothing Then realSize = bmp.Size
                Else
                    Dim sharp As Boolean
                    Using quick As Image = MediaViewerControl.LoadEmbeddedThumbnail(file, box, realSize, sharp)
                        If quick IsNot Nothing AndAlso sharp Then Return FillOutcome.NotNeeded
                    End Using
                    bmp = TryCast(MediaViewerControl.LoadDisplayImage(file, box, realSize), Bitmap)
                End If
                If bmp Is Nothing Then Return FillOutcome.Failed
                Write(root, file, box, bmp, realSize, video)
                Return If(IO.File.Exists(key.File), FillOutcome.Made, FillOutcome.Failed)
            Catch ex As Exception When IsCacheError(ex)
                Return FillOutcome.Failed
            Finally
                bmp?.Dispose()
            End Try
        End Function

        '==================================================================================================
        ' Size and emptying
        '==================================================================================================
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

        '==================================================================================================
        ' Names
        '==================================================================================================
        ''' <summary>The entry of this file at this size in <paramref name="root"/>; False when there is no cache
        ''' or the file is gone.</summary>
        Private Shared Function TryKey(ByVal root As String, ByVal path As String, ByVal box As Size, ByRef key As EntryKey) As Boolean
            key = Nothing
            If String.IsNullOrEmpty(root) OrElse String.IsNullOrEmpty(path) Then Return False
            Try
                Dim fi As New FileInfo(path)
                If Not fi.Exists Then Return False
                Dim name As String = fi.FullName
                Dim cacheRoot As String = IO.Path.GetFullPath(root)
                If String.Equals(IO.Path.GetPathRoot(name), IO.Path.GetPathRoot(cacheRoot), StringComparison.OrdinalIgnoreCase) Then
                    name = IO.Path.GetRelativePath(cacheRoot, name)   ' the same drive as the cache: from the cache, no drive letter
                End If
                Dim hash As String = HashOf(name, fi.LastWriteTimeUtc.Ticks, fi.Length, box)
                key = New EntryKey With {.File = IO.Path.Combine(root, hash.Substring(0, 2), hash & FileExtension),
                                         .Name = name, .Ticks = fi.LastWriteTimeUtc.Ticks, .Length = fi.Length, .Box = box}
                Return True
            Catch ex As Exception When IsCacheError(ex)
                Return False
            End Try
        End Function

        Private Shared Function HashOf(ByVal name As String, ByVal ticks As Long, ByVal length As Long, ByVal box As Size) As String
            Dim key As String = name.ToLowerInvariant() & "|" & ticks & "|" & length & "|" & box.Width & "x" & box.Height
            Using sha As SHA1 = SHA1.Create()
                Return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant()
            End Using
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
