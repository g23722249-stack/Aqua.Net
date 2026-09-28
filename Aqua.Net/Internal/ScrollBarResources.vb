Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for LibResource.GetScrollBarSurface / GetScrollBarTrack /
' GetScrollBarBackGround and the frmResScrollBar resource form. The thumb / track /
' background templates are the original bitmaps, stretched to fit, reproducing the
' VB6 frmPaint HorizontalStretch / VerticalStretch skinning of the scrollbar.
Namespace Global.Aqua

    ''' <summary>Thumb visual state (subset of VB6 enumControlState actually used by the scrollbar).</summary>
    Friend Enum ScrollState
        ExitFocus = 0   ' also used for Normal and Disable
        Hover = 1       ' also used for EnterFocus
        Click = 2
        Deactivate = 3
    End Enum

    Friend Enum ScrollBarSize
        Large = 0
        Small = 1
    End Enum

    Friend Module ScrollBarResources

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        ''' <summary>Thumb surface (port of GetScrollBarSurface). Orientation + state + colour + size.</summary>
        Public Function GetSurface(ByVal orientation As OrientationMode, ByVal state As ScrollState,
                                   ByVal color As ColorConstants, ByVal size As ScrollBarSize) As Image
            ' name pattern: img{S|L}{H|V}Scroll{ExitFocus|Hover|Click|Deactivate}[_cc]
            Dim sz As String = If(size = ScrollBarSize.Small, "S", "L")
            Dim orient As String = If(orientation = OrientationMode.Horizontal, "H", "V")
            Select Case state
                Case ScrollState.Deactivate
                    Return Load("img" & sz & orient & "ScrollDeactivate")
                Case ScrollState.Hover
                    Return Load("img" & sz & orient & "ScrollHover_" & Cc(color))
                Case ScrollState.Click
                    Return Load("img" & sz & orient & "ScrollClick_" & Cc(color))
                Case Else ' ExitFocus / Normal / Disable
                    Return Load("img" & sz & orient & "ScrollExitFocus_" & Cc(color))
            End Select
        End Function

        ''' <summary>Track surface behind the thumb (port of GetScrollBarTrack).</summary>
        Public Function GetTrack(ByVal orientation As OrientationMode) As Image
            Return Load(If(orientation = OrientationMode.Horizontal, "imgHScrollTrack", "imgVScrollTrack"))
        End Function

        ''' <summary>Whole-control background (port of GetScrollBarBackGround).</summary>
        Public Function GetBackground(ByVal orientation As OrientationMode) As Image
            Return Load(If(orientation = OrientationMode.Horizontal, "imgHScrollDisable", "imgVScrollDisable"))
        End Function

        Private Function Cc(ByVal color As ColorConstants) As String
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return i.ToString("00")
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.ScrollRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

        '--- spinner (frmResSpin): the up/down buttons always shown at the bar's end ---

        ''' <summary>Spin button surface (port of GetSpinSurface). Disable/Enabled/Click(colour) per direction.</summary>
        Public Function GetSpinSurface(ByVal state As ScrollState, ByVal color As ColorConstants, ByVal dir As Integer) As Image
            Dim d As String = DirName(dir)
            Select Case state
                Case ScrollState.Deactivate
                    Return LoadSpin("img" & d & "Disable")
                Case ScrollState.Hover, ScrollState.Click
                    Return LoadSpin("img" & d & "Click_" & Cc(color))
                Case Else ' ExitFocus / Normal
                    Return LoadSpin("img" & d & "Enabled")
            End Select
        End Function

        ''' <summary>Spin arrow glyph (port of GetSpinArrowSurface). imgArrow(dir) / imgArrowDisabled(dir).</summary>
        Public Function GetSpinArrow(ByVal dir As Integer, ByVal enabled As Boolean) As Image
            Dim name As String = If(enabled, "imgArrow", "imgArrowDisabled") & "_" & dir.ToString("00")
            Return LoadSpinIco(name)
        End Function

        ''' <summary>Outline mask of a whole Independence-style spinner (port of GetSpinMask):
        ''' white = inside, black = cut.</summary>
        Public Function GetSpinMask(ByVal orientation As OrientationMode) As Image
            Return LoadSpin(If(orientation = OrientationMode.Horizontal, "imgHMask", "imgVMask"))
        End Function

        Private Function DirName(ByVal dir As Integer) As String
            Select Case dir
                Case 0 : Return "Up"
                Case 1 : Return "Down"
                Case 2 : Return "Left"
                Case Else : Return "Right"
            End Select
        End Function

        Private Function LoadSpin(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue("spin:" & key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.SpinRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)   ' clone: stream-backed bitmaps fault once the stream closes
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache("spin:" & key) = img
                Return img
            End SyncLock
        End Function

        Private Function LoadSpinIco(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue("spin:" & key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.SpinRes." & key & ".ico")
                    If s Is Nothing Then Return Nothing
                    Using ico As New Icon(s)
                        img = ico.ToBitmap()
                    End Using
                End Using
                _cache("spin:" & key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
