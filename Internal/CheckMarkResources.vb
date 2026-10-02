Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for VB6 Module\LibResource.GetListItemCheckedSurface and the
' frmResListItem resource form. The 36 check/radio/hook bitmaps were the exact 16x16
' icons embedded in frmResListItem.frx; they are carved out verbatim and embedded here,
' so the rendered check marks are pixel-identical to the original control.
Namespace Global.Aqua

    Friend Module CheckMarkResources

        Private ReadOnly _cache As New Dictionary(Of String, Bitmap)()
        Private ReadOnly _sync As New Object()

        ''' <summary>
        ''' Return the check-mark surface for a cell, mirroring
        ''' LibResource.GetListItemCheckedSurface(MarkStyle, Color, Marked, Enabled).
        ''' Returns Nothing when the style is None (no image), as the VB6 code did.
        ''' </summary>
        Public Function GetCheckSurface(ByVal style As ItemCheckStyle, _
                                        ByVal color As ColorConstants, _
                                        ByVal marked As Boolean, _
                                        ByVal enabled As Boolean) As Bitmap
            Dim key As String
            Select Case style
                Case ItemCheckStyle.None
                    Return Nothing

                Case ItemCheckStyle.Check
                    If enabled Then
                        key = If(marked, "check_" & ColorSuffix(color), "uncheck")
                    Else
                        key = If(marked, "check_disabled", "uncheck_disabled")
                    End If

                Case ItemCheckStyle.Radio
                    If enabled Then
                        key = If(marked, "radio_" & ColorSuffix(color), "unradio")
                    Else
                        key = If(marked, "radio_disabled", "unradio_disabled")
                    End If

                Case ItemCheckStyle.Hook
                    If enabled Then
                        key = If(marked, "hook", "unhook")
                    Else
                        key = If(marked, "hook_disabled", "unhook_disabled")
                    End If

                Case Else
                    Return Nothing
            End Select

            Return LoadIcon(key)
        End Function

        Private Function ColorSuffix(ByVal color As ColorConstants) As String
            Dim i As Integer = CInt(color)
            If i < 0 Then i = 0
            If i > 12 Then i = 12
            Return i.ToString("00")
        End Function

        Private Function LoadIcon(ByVal key As String) As Bitmap
            SyncLock _sync
                Dim bmp As Bitmap = Nothing
                If _cache.TryGetValue(key, bmp) Then Return bmp

                Dim name As String = "Aqua.CheckMarks." & key & ".ico"
                Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                    If stream Is Nothing Then
                        Return Nothing
                    End If
                    Using ico As New Icon(stream)
                        bmp = ico.ToBitmap()
                    End Using
                End Using
                _cache(key) = bmp
                Return bmp
            End SyncLock
        End Function

        ''' <summary>picMark bitmap carved from Grid.ctx (scrollbar corner filler); used by the Grid control.</summary>
        Public Function GetPicMark() As Bitmap
            SyncLock _sync
                Dim bmp As Bitmap = Nothing
                If _cache.TryGetValue("__picMark", bmp) Then Return bmp
                Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.GridRes.picMark.bmp")
                    If stream Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(stream)   ' clone: stream-backed bitmaps fault once the stream closes
                        bmp = New Bitmap(tmp)
                    End Using
                End Using
                _cache("__picMark") = bmp
                Return bmp
            End SyncLock
        End Function

    End Module

End Namespace
