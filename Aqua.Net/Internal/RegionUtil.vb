Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

' Managed replacement for the VB6 Module\LibRegion.bas rounded-corner window regions.
' The VB6 code used gdi32 CreateRoundRectRgn(0,0,w+1,h+1,border,border) + SetWindowRgn.
' Here we build an equivalent GraphicsPath-based Region and assign it to Control.Region,
' which works identically on net35 and net8 without any P/Invoke.
Namespace Global.Aqua

    Friend Module RegionUtil

        Private Const DefaultBorder As Integer = 4

        ''' <summary>
        ''' Build a region matching VB6 DrawObtusenessControlRegion. Size is the control's
        ''' pixel client size; the VB6 code added +1 to width/height before making the region.
        ''' </summary>
        Public Function CreateObtusenessRegion(ByVal mode As ObtusenessMode, _
                                               ByVal widthPx As Integer, _
                                               ByVal heightPx As Integer, _
                                               Optional ByVal border As Integer = DefaultBorder) As Region
            Dim w As Integer = widthPx + 1
            Dim h As Integer = heightPx + 1
            If w <= 0 OrElse h <= 0 Then Return New Region(New Rectangle(0, 0, Math.Max(w, 0), Math.Max(h, 0)))

            If mode = ObtusenessMode.None OrElse border <= 0 Then
                Return New Region(New Rectangle(0, 0, w, h))
            End If

            Dim tl, tr, bl, br As Boolean
            Select Case mode
                Case ObtusenessMode.All : tl = True : tr = True : bl = True : br = True
                Case ObtusenessMode.LeftTop : tl = True
                Case ObtusenessMode.RightTop : tr = True
                Case ObtusenessMode.LeftBottom : bl = True
                Case ObtusenessMode.RightBottom : br = True
                Case ObtusenessMode.Top : tl = True : tr = True
                Case ObtusenessMode.Bottom : bl = True : br = True
                Case ObtusenessMode.Left : tl = True : bl = True
                Case ObtusenessMode.Right : tr = True : br = True
            End Select

            Using path As GraphicsPath = BuildRoundedPath(w, h, border, tl, tr, br, bl)
                Return New Region(path)
            End Using
        End Function

        ''' <summary>Same shape as CreateObtusenessRegion, as a path instead of a Region -- lets a
        ''' caller (AquaForm/iForm) stroke the exact outline of its own Region with a Pen, so the
        ''' border line is guaranteed to follow the true rounded-corner clip pixel-for-pixel instead
        ''' of needing separate straight border lines plus a hand-placed corner overlay image to
        ''' (imperfectly) fake the curve. Caller must Dispose the returned path.</summary>
        Public Function CreateObtusenessPath(ByVal mode As ObtusenessMode, _
                                             ByVal widthPx As Integer, _
                                             ByVal heightPx As Integer, _
                                             Optional ByVal border As Integer = DefaultBorder) As GraphicsPath
            Dim w As Integer = widthPx + 1
            Dim h As Integer = heightPx + 1
            If w <= 0 OrElse h <= 0 Then
                Dim empty As New GraphicsPath()
                empty.AddRectangle(New Rectangle(0, 0, Math.Max(w, 0), Math.Max(h, 0)))
                Return empty
            End If

            Dim tl, tr, bl, br As Boolean
            Select Case mode
                Case ObtusenessMode.All : tl = True : tr = True : bl = True : br = True
                Case ObtusenessMode.LeftTop : tl = True
                Case ObtusenessMode.RightTop : tr = True
                Case ObtusenessMode.LeftBottom : bl = True
                Case ObtusenessMode.RightBottom : br = True
                Case ObtusenessMode.Top : tl = True : tr = True
                Case ObtusenessMode.Bottom : bl = True : br = True
                Case ObtusenessMode.Left : tl = True : bl = True
                Case ObtusenessMode.Right : tr = True : br = True
            End Select

            If mode = ObtusenessMode.None OrElse border <= 0 Then
                Dim rectPath As New GraphicsPath()
                rectPath.AddRectangle(New Rectangle(0, 0, w - 1, h - 1))
                Return rectPath
            End If

            Return BuildRoundedPath(w, h, border, tl, tr, br, bl)
        End Function

        ''' <summary>Builds a Region from a bitmap, excluding pixels that match transparentColor
        ''' exactly: one rectangle per contiguous run of non-matching pixels in each row (every run --
        ''' taking only the first-to-last span of a row filled in the key-coloured holes inside a shape,
        ''' e.g. between a gear's teeth, which then showed as magenta). Port of VB6's
        ''' Quartz.Region.CreateFromPicture(picture, TransparencyColor:=key) (used by Icon.ctl's
        ''' colour-key Transparency) -- FlashButton.vb has its own private, black-only copy of this
        ''' same row-run-scan idea for its fixed mask art; this is the general form, keyed to an
        ''' arbitrary caller-supplied colour, for controls (like Icon) whose transparency key is a
        ''' runtime property instead of a fixed bundled mask.</summary>
        Public Function CreateRegionFromBitmap(ByVal bitmap As Bitmap, ByVal transparentColor As Color) As Region
            Dim w As Integer = bitmap.Width, h As Integer = bitmap.Height
            ' all pixels at once (GetPixel per pixel made every resize of a masked control cost ms)
            Dim px(w * h - 1) As Integer
            Dim data As BitmapData = bitmap.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Try
                For y As Integer = 0 To h - 1
                    Marshal.Copy(New IntPtr(data.Scan0.ToInt64() + CLng(y) * data.Stride), px, y * w, w)   ' (no IntPtr.Add on net35)
                Next
            Finally
                bitmap.UnlockBits(data)
            End Try
            Dim key As Integer = transparentColor.ToArgb() And &HFFFFFF

            ' Runs of each row; a row whose runs equal the previous row's just makes those rectangles
            ' taller -- a mask is mostly identical rows between its rounded ends, so a handful of
            ' rectangles instead of one per row.
            Dim path As New GraphicsPath(FillMode.Winding)
            Dim prev As New List(Of Integer)(), cur As New List(Of Integer)()   ' flattened (left, right) pairs
            Dim blockTop As Integer = 0
            For y As Integer = 0 To h
                cur.Clear()
                If y < h Then
                    Dim row As Integer = y * w, left As Integer = -1
                    For x As Integer = 0 To w
                        Dim isKey As Boolean = x = w OrElse (px(row + x) And &HFFFFFF) = key
                        If Not isKey Then
                            If left = -1 Then left = x
                        ElseIf left >= 0 Then
                            cur.Add(left) : cur.Add(x)
                            left = -1
                        End If
                    Next
                End If
                If y = h OrElse Not SameRuns(prev, cur) Then
                    For i As Integer = 0 To prev.Count - 1 Step 2
                        path.AddRectangle(New Rectangle(prev(i), blockTop, prev(i + 1) - prev(i), y - blockTop))
                    Next
                    Dim t As List(Of Integer) = prev : prev = cur : cur = t
                    blockTop = y
                End If
            Next
            Dim result As New Region(path)
            path.Dispose()
            Return result
        End Function

        Private Function SameRuns(ByVal a As List(Of Integer), ByVal b As List(Of Integer)) As Boolean
            If a.Count <> b.Count Then Return False
            For i As Integer = 0 To a.Count - 1
                If a(i) <> b(i) Then Return False
            Next
            Return True
        End Function

        ''' <summary>Port of VB6's frmPaint.Region + Quartz.Region(TransparencyColor:=vbBlack): the
        ''' control's mask art (white = keep, black = cut) three-slice stretched to the control's size
        ''' -- caps at native size, so the cut follows the art's own rounded ends exactly -- and every
        ''' black pixel left out. Nothing (no clip) without a mask.</summary>
        Public Function CreateStretchedMaskRegion(ByVal mask As Image, ByVal width As Integer, ByVal height As Integer,
                                                  ByVal horizontal As Boolean) As Region
            If mask Is Nothing OrElse width <= 0 OrElse height <= 0 Then Return Nothing
            Using stretched As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(stretched)
                    g.Clear(Color.Black)
                    g.InterpolationMode = InterpolationMode.NearestNeighbor
                    g.PixelOffsetMode = PixelOffsetMode.Half
                    Skin.DrawStretch(g, mask, New Rectangle(0, 0, width, height), horizontal)
                End Using
                Return CreateRegionFromBitmap(stretched, Color.Black)
            End Using
        End Function

        Private Function BuildRoundedPath(ByVal w As Integer, ByVal h As Integer, ByVal border As Integer, _
                                          ByVal tl As Boolean, ByVal tr As Boolean, _
                                          ByVal br As Boolean, ByVal bl As Boolean) As GraphicsPath
            Dim d As Integer = border * 2
            If d > w Then d = w
            If d > h Then d = h

            Dim p As New GraphicsPath()

            ' top edge / top-left corner
            If tl Then p.AddArc(0, 0, d, d, 180, 90) Else p.AddLine(0, 0, 0, 0)
            ' top-right corner
            If tr Then p.AddArc(w - d - 1, 0, d, d, 270, 90) Else p.AddLine(w - 1, 0, w - 1, 0)
            ' bottom-right corner
            If br Then p.AddArc(w - d - 1, h - d - 1, d, d, 0, 90) Else p.AddLine(w - 1, h - 1, w - 1, h - 1)
            ' bottom-left corner
            If bl Then p.AddArc(0, h - d - 1, d, d, 90, 90) Else p.AddLine(0, h - 1, 0, h - 1)

            p.CloseFigure()
            Return p
        End Function

    End Module

End Namespace
