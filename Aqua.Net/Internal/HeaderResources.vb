Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetHeadersSurface / GetHeaderSortOrderSurface
' and the frmResHeaders resource form. The header button surfaces are the original
' 40x20 template bitmaps (imgNormal / imgSelect(color) / imgClick(color)); they are
' stretched to each button's rectangle, reproducing the VB6 frmPaint HorizontalStretch.
Namespace Global.Aqua

    ''' <summary>Header button visual state, mirroring the VB6 enumControlState use in DrawHeaders.</summary>
    Friend Enum HeaderState
        Normal = 0
        Selected = 1
        Click = 2
    End Enum

    Friend Module HeaderResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        ''' <summary>Button surface for a state/colour (GetHeadersSurface). Normal ignores colour.</summary>
        Public Function GetSurface(ByVal state As HeaderState, ByVal color As ColorConstants) As Image
            Select Case state
                Case HeaderState.Selected
                    Return LoadBmp("hdr_select_" & ColorSuffix(color))
                Case HeaderState.Click
                    Return LoadBmp("hdr_click_" & ColorSuffix(color))
                Case Else
                    Return LoadBmp("hdr_normal")
            End Select
        End Function

        ''' <summary>Sort arrow for a direction (GetHeaderSortOrderSurface); Nothing for None.</summary>
        Public Function GetSortArrow(ByVal order As SortOrder) As Image
            Select Case order
                Case SortOrder.Ascending
                    Return LoadIco("sort_asc")
                Case SortOrder.Descending
                    Return LoadIco("sort_desc")
                Case Else
                    Return Nothing
            End Select
        End Function

        Private Function ColorSuffix(ByVal color As ColorConstants) As String
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return i.ToString("00")
        End Function

        Private Function LoadBmp(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.HeaderRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

        Private Function LoadIco(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.HeaderRes." & key & ".ico")
                    If s Is Nothing Then Return Nothing
                    Using ico As New Icon(s)
                        img = ico.ToBitmap()
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
