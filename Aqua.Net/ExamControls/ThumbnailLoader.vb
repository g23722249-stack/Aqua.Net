Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' Background thumbnail loading for MediaList (MediaViewerControl.LoadAsynchronously):
    '''   - decoding runs on two background threads, never on the UI thread, so scrolling doesn't stall;
    '''     the result is handed back with BeginInvoke;
    '''   - newest request first (what just scrolled into view), and a request whose control has moved
    '''     on (scrolled away, another file) is skipped when its turn comes;
    '''   - the decoded thumbnails are kept in an LRU cache with a byte budget, so scrolling back shows
    '''     them at once while memory stays bounded (the controls themselves drop theirs when they
    '''     scroll out of view). The key holds the file's last-write time: a saved edit is decoded anew.
    ''' The cache owns its bitmaps; every control gets its own copy.
    ''' Workers are STA: video thumbnails come from the shell (COM).
    ''' </summary>
    Friend NotInheritable Class ThumbnailLoader

        Private Const WorkerCount As Integer = 2
        Private Const CacheBudgetBytes As Long = 48L * 1024 * 1024

        Private Class Request
            Public Owner As MediaViewerControl
            Public Path As String
            Public Box As Size
            Public Video As Boolean
            Public Generation As Integer
        End Class

        Private Class Entry
            Public Bitmap As Bitmap
            Public RealSize As Size
            Public Bytes As Long
        End Class

        Private Shared ReadOnly _sync As New Object()
        Private Shared ReadOnly _queue As New LinkedList(Of Request)()          ' newest first
        Private Shared ReadOnly _cache As New Dictionary(Of String, LinkedListNode(Of KeyValuePair(Of String, Entry)))()
        Private Shared ReadOnly _lru As New LinkedList(Of KeyValuePair(Of String, Entry))()   ' most recent first
        Private Shared _cacheBytes As Long
        Private Shared _started As Boolean

        Private Sub New()
        End Sub

        ''' <summary>A copy of the cached thumbnail for this file at this size, or Nothing.</summary>
        Public Shared Function TryGetCached(ByVal path As String, ByVal box As Size, ByRef realSize As Size) As Bitmap
            Dim key As String = CacheKey(path, box)
            If key Is Nothing Then Return Nothing
            SyncLock _sync
                Dim node As LinkedListNode(Of KeyValuePair(Of String, Entry)) = Nothing
                If Not _cache.TryGetValue(key, node) Then Return Nothing
                _lru.Remove(node)
                _lru.AddFirst(node)
                realSize = node.Value.Value.RealSize
                Return New Bitmap(node.Value.Value.Bitmap)   ' under the lock: a GDI+ bitmap can't be read by two threads at once
            End SyncLock
        End Function

        ''' <summary>Queues a thumbnail for <paramref name="owner"/>; it arrives through
        ''' MediaViewerControl.DeliverThumbnail on the UI thread, if the request is still current then.</summary>
        Public Shared Sub Enqueue(ByVal owner As MediaViewerControl, ByVal path As String, ByVal box As Size,
                                  ByVal video As Boolean, ByVal generation As Integer)
            SyncLock _sync
                If Not _started Then StartWorkers()
                _queue.AddFirst(New Request With {.Owner = owner, .Path = path, .Box = box, .Video = video, .Generation = generation})
                Monitor.Pulse(_sync)
            End SyncLock
        End Sub

        Private Shared Sub StartWorkers()
            For i As Integer = 1 To WorkerCount
                Dim t As New Thread(AddressOf Work) With {.IsBackground = True, .Priority = ThreadPriority.BelowNormal, .Name = "Aqua thumbnails " & i}
                t.SetApartmentState(ApartmentState.STA)
                t.Start()
            Next
            _started = True
        End Sub

        Private Shared Sub Work()
            Do
                Dim r As Request
                SyncLock _sync
                    While _queue.Count = 0
                        Monitor.Wait(_sync)
                    End While
                    r = _queue.First.Value
                    _queue.RemoveFirst()
                End SyncLock
                Try
                    Serve(r)
                Catch
                    ' a thumbnail that can't be made just stays empty
                End Try
            Loop
        End Sub

        Private Shared Sub Serve(ByVal r As Request)
            If Not r.Owner.IsCurrentLoad(r.Generation) Then Return   ' scrolled away / another file since
            Dim realSize As Size
            Dim copy As Bitmap = TryGetCached(r.Path, r.Box, realSize)   ' an identical request may have filled it
            If copy Is Nothing Then
                Dim bmp As Bitmap
                If r.Video Then
                    bmp = ShellThumbnail.GetThumbnail(r.Path, r.Box.Width, r.Box.Height)
                    If bmp IsNot Nothing Then realSize = bmp.Size
                Else
                    bmp = TryCast(MediaViewerControl.LoadDisplayImage(r.Path, r.Box, realSize), Bitmap)
                End If
                If bmp Is Nothing Then Return
                SyncLock _sync
                    AddToCache(CacheKey(r.Path, r.Box), bmp, realSize)
                    copy = New Bitmap(bmp)
                End SyncLock
            End If
            If Not r.Owner.IsCurrentLoad(r.Generation) OrElse Not r.Owner.IsHandleCreated Then
                copy.Dispose()
                Return
            End If
            Try
                Dim owner As MediaViewerControl = r.Owner, gen As Integer = r.Generation, rs As Size = realSize
                owner.BeginInvoke(New MethodInvoker(Sub() owner.DeliverThumbnail(gen, copy, rs)))
            Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ObjectDisposedException
                copy.Dispose()   ' its window went away in the meantime
            End Try
        End Sub

        ' caller holds _sync
        Private Shared Sub AddToCache(ByVal key As String, ByVal bmp As Bitmap, ByVal realSize As Size)
            If key Is Nothing Then Return
            Dim old As LinkedListNode(Of KeyValuePair(Of String, Entry)) = Nothing
            If _cache.TryGetValue(key, old) Then
                _lru.Remove(old)
                _cache.Remove(key)
                _cacheBytes -= old.Value.Value.Bytes
                old.Value.Value.Bitmap.Dispose()
            End If
            Dim e As New Entry With {.Bitmap = bmp, .RealSize = realSize, .Bytes = CLng(bmp.Width) * bmp.Height * 4}
            _cache(key) = _lru.AddFirst(New KeyValuePair(Of String, Entry)(key, e))
            _cacheBytes += e.Bytes
            While _cacheBytes > CacheBudgetBytes AndAlso _lru.Count > 1
                Dim last As LinkedListNode(Of KeyValuePair(Of String, Entry)) = _lru.Last
                _lru.RemoveLast()
                _cache.Remove(last.Value.Key)
                _cacheBytes -= last.Value.Value.Bytes
                last.Value.Value.Bitmap.Dispose()
            End While
        End Sub

        ''' <summary>path + size + last-write time (a changed file is a different entry); Nothing if the file is gone.</summary>
        Private Shared Function CacheKey(ByVal path As String, ByVal box As Size) As String
            Try
                Dim fi As New FileInfo(path)
                If Not fi.Exists Then Return Nothing
                Return fi.FullName.ToLowerInvariant() & "|" & box.Width & "x" & box.Height & "|" & fi.LastWriteTimeUtc.Ticks
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                                   TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException
                Return Nothing
            End Try
        End Function

    End Class

End Namespace
