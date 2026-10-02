Option Strict On
Option Explicit On

Imports System.Drawing

' Three-slice (end-cap preserving) stretch, reproducing the VB6 frmPaint
' HorizontalStretch / VerticalStretch behaviour used for the scrollbar thumb, track and
' background. The rounded end caps stay at native size; only the middle strip is stretched,
' so a tall/wide thumb keeps crisp rounded ends instead of distorting when drawn whole.
Namespace Global.Aqua

    Friend Module Skin

        ''' <summary>Stretch <paramref name="img"/> into <paramref name="dest"/> keeping the two end caps intact.</summary>
        Public Sub DrawStretch(ByVal g As Graphics, ByVal img As Image, ByVal dest As Rectangle, ByVal horizontal As Boolean)
            If img Is Nothing OrElse dest.Width <= 0 OrElse dest.Height <= 0 Then Return

            If horizontal Then
                Dim cap As Integer = Math.Min(img.Height \ 2, img.Width \ 2)
                If cap < 1 OrElse img.Width < cap * 2 + 1 OrElse dest.Width < cap * 2 + 1 Then
                    g.DrawImage(img, dest) : Return
                End If
                ' left cap
                g.DrawImage(img, New Rectangle(dest.X, dest.Y, cap, dest.Height),
                            New Rectangle(0, 0, cap, img.Height), GraphicsUnit.Pixel)
                ' stretched middle
                g.DrawImage(img, New Rectangle(dest.X + cap, dest.Y, dest.Width - cap * 2, dest.Height),
                            New Rectangle(cap, 0, img.Width - cap * 2, img.Height), GraphicsUnit.Pixel)
                ' right cap
                g.DrawImage(img, New Rectangle(dest.Right - cap, dest.Y, cap, dest.Height),
                            New Rectangle(img.Width - cap, 0, cap, img.Height), GraphicsUnit.Pixel)
            Else
                Dim cap As Integer = Math.Min(img.Width \ 2, img.Height \ 2)
                If cap < 1 OrElse img.Height < cap * 2 + 1 OrElse dest.Height < cap * 2 + 1 Then
                    g.DrawImage(img, dest) : Return
                End If
                ' top cap
                g.DrawImage(img, New Rectangle(dest.X, dest.Y, dest.Width, cap),
                            New Rectangle(0, 0, img.Width, cap), GraphicsUnit.Pixel)
                ' stretched middle
                g.DrawImage(img, New Rectangle(dest.X, dest.Y + cap, dest.Width, dest.Height - cap * 2),
                            New Rectangle(0, cap, img.Width, img.Height - cap * 2), GraphicsUnit.Pixel)
                ' bottom cap
                g.DrawImage(img, New Rectangle(dest.X, dest.Bottom - cap, dest.Width, cap),
                            New Rectangle(0, img.Height - cap, img.Width, cap), GraphicsUnit.Pixel)
            End If
        End Sub

        ''' <summary>
        ''' Port of Quartz.Draw's SizeMode dispatch (D:\專案\RunTime\Quartz\CoClass\Draw.cls, Drawing):
        ''' Normal = native size at the top-left (clipped); StretchImage/FastStretchImage = stretch;
        ''' AutoSize = zoom to fit keeping the aspect ratio, centred; CenterImage = native size centred
        ''' (clipped); Horizontal/VerticalStretch = three-slice stretch; Fill = nine-slice with TILED
        ''' edges and centre (PrintFillPicture); Appose = tile the whole image (PrintApposePicture).
        ''' </summary>
        Public Sub DrawSized(ByVal g As Graphics, ByVal img As Image, ByVal dest As Rectangle, ByVal mode As ImageSizeMode)
            If img Is Nothing OrElse dest.Width <= 0 OrElse dest.Height <= 0 Then Return
            Select Case mode
                Case ImageSizeMode.Normal
                    g.DrawImage(img, New Rectangle(dest.X, dest.Y, img.Width, img.Height))
                Case ImageSizeMode.CenterImage
                    g.DrawImage(img, New Rectangle(dest.X + (dest.Width - img.Width) \ 2, dest.Y + (dest.Height - img.Height) \ 2, img.Width, img.Height))
                Case ImageSizeMode.AutoSize
                    Dim scale As Double = Math.Min(dest.Width / CDbl(img.Width), dest.Height / CDbl(img.Height))
                    Dim w As Integer = Math.Max(1, CInt(img.Width * scale)), h As Integer = Math.Max(1, CInt(img.Height * scale))
                    g.DrawImage(img, New Rectangle(dest.X + (dest.Width - w) \ 2, dest.Y + (dest.Height - h) \ 2, w, h))
                Case ImageSizeMode.HorizontalStretch
                    DrawStretch(g, img, dest, horizontal:=True)
                Case ImageSizeMode.VerticalStretch
                    DrawStretch(g, img, dest, horizontal:=False)
                Case ImageSizeMode.Fill
                    DrawFill(g, img, dest)
                Case ImageSizeMode.Appose
                    Using tb As New TextureBrush(img, Drawing2D.WrapMode.Tile)
                        tb.TranslateTransform(dest.X, dest.Y)
                        g.FillRectangle(tb, dest)
                    End Using
                Case Else ' StretchImage / FastStretchImage
                    g.DrawImage(img, dest)
            End Select
        End Sub

        ''' <summary>Quartz PrintFillPicture: the image is cut into a 3x3 grid of cells
        ''' (ceil(W/3) x ceil(H/3)); corners are drawn once at native size, the edge cells are tiled
        ''' along their edge and the centre cell tiles the interior -- so a small rounded "panel" skin
        ''' fills any size without its corners or texture being stretched. Tiling is aligned to the
        ''' destination origin, as in the VB6 loops.</summary>
        Public Sub DrawFill(ByVal g As Graphics, ByVal img As Image, ByVal dest As Rectangle)
            Dim cw As Integer = (img.Width + 2) \ 3, ch As Integer = (img.Height + 2) \ 3
            If cw < 1 OrElse ch < 1 OrElse img.Width < 3 OrElse img.Height < 3 Then
                g.DrawImage(img, dest) : Return
            End If
            Dim rightSrc As Integer = img.Width - cw, bottomSrc As Integer = img.Height - ch
            ' interior, then the four edge bands, each tiled from its own cell
            TileCell(g, img, New Rectangle(cw, ch, cw, ch), dest, dest.Location)
            TileCell(g, img, New Rectangle(cw, 0, cw, ch), New Rectangle(dest.X, dest.Y, dest.Width, ch), dest.Location)
            TileCell(g, img, New Rectangle(cw, bottomSrc, cw, ch), New Rectangle(dest.X, dest.Bottom - ch, dest.Width, ch), New Point(dest.X, dest.Bottom - ch))
            TileCell(g, img, New Rectangle(0, ch, cw, ch), New Rectangle(dest.X, dest.Y, cw, dest.Height), dest.Location)
            TileCell(g, img, New Rectangle(rightSrc, ch, cw, ch), New Rectangle(dest.Right - cw, dest.Y, cw, dest.Height), New Point(dest.Right - cw, dest.Y))
            ' corners at native size
            g.DrawImage(img, New Rectangle(dest.X, dest.Y, cw, ch), New Rectangle(0, 0, cw, ch), GraphicsUnit.Pixel)
            g.DrawImage(img, New Rectangle(dest.Right - cw, dest.Y, cw, ch), New Rectangle(rightSrc, 0, cw, ch), GraphicsUnit.Pixel)
            g.DrawImage(img, New Rectangle(dest.X, dest.Bottom - ch, cw, ch), New Rectangle(0, bottomSrc, cw, ch), GraphicsUnit.Pixel)
            g.DrawImage(img, New Rectangle(dest.Right - cw, dest.Bottom - ch, cw, ch), New Rectangle(rightSrc, bottomSrc, cw, ch), GraphicsUnit.Pixel)
        End Sub

        Private Sub TileCell(ByVal g As Graphics, ByVal img As Image, ByVal src As Rectangle, ByVal area As Rectangle, ByVal origin As Point)
            If area.Width <= 0 OrElse area.Height <= 0 Then Return
            Using tb As New TextureBrush(img, Drawing2D.WrapMode.Tile, src)
                tb.TranslateTransform(origin.X, origin.Y)
                g.FillRectangle(tb, area)
            End Using
        End Sub

    End Module

End Namespace
