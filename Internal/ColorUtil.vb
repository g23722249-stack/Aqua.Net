Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

' Managed replacement for the external Quartz.Color COM object (iQuartz.dll).
' Only the members the grid family actually used are reimplemented:
'   ColorToRGB  -> OleToColor          (OLE_COLOR Long  -> GDI+ Color)
'   RGBToColor  -> ColorToOle          (GDI+ Color      -> OLE_COLOR Long)
'   ColorToHSL  -> ColorToHsl          (returns Hsl: H 0..360, S/L 0..100)
' plus small RGB channel tweaks used by Grid's border shading.
Namespace Global.Aqua

    ''' <summary>HSL triple on the same scale the VB6 code compared against (S/L are 0..100).</summary>
    Friend Structure Hsl
        Public Hue As Double          ' 0..360
        Public Saturation As Double   ' 0..100
        Public Luminosity As Double   ' 0..100
    End Structure

    Friend Module ColorUtil

        ''' <summary>OLE_COLOR (incl. system colours &amp;H8000xxxx) to GDI+ Color. Mirrors Quartz.ColorToRGB.</summary>
        Public Function OleToColor(ByVal oleColor As Integer) As Color
            Return ColorTranslator.FromOle(oleColor)
        End Function

        ''' <summary>GDI+ Color back to an OLE_COLOR Long. Mirrors Quartz.RGBToColor.</summary>
        Public Function ColorToOle(ByVal c As Color) As Integer
            Return ColorTranslator.ToOle(c)
        End Function

        ''' <summary>
        ''' Convert to HSL with Saturation/Luminosity expressed as 0..100 percentages,
        ''' matching the thresholds the VB6 Cell.SetTextColor used
        ''' (Luminosity &lt;= 90 And Saturation &gt; 50 =&gt; white text on a selected cell).
        ''' </summary>
        Public Function ColorToHsl(ByVal oleColor As Integer) As Hsl
            Dim c As Color = OleToColor(oleColor)
            Dim r As Double = c.R / 255.0
            Dim g As Double = c.G / 255.0
            Dim b As Double = c.B / 255.0

            Dim max As Double = Math.Max(r, Math.Max(g, b))
            Dim min As Double = Math.Min(r, Math.Min(g, b))
            Dim delta As Double = max - min

            Dim h As Double = 0.0
            Dim s As Double = 0.0
            Dim l As Double = (max + min) / 2.0

            If delta <> 0.0 Then
                If l < 0.5 Then
                    s = delta / (max + min)
                Else
                    s = delta / (2.0 - max - min)
                End If

                If max = r Then
                    h = (g - b) / delta + (If(g < b, 6.0, 0.0))
                ElseIf max = g Then
                    h = (b - r) / delta + 2.0
                Else
                    h = (r - g) / delta + 4.0
                End If
                h *= 60.0
            End If

            Dim result As Hsl
            result.Hue = h
            result.Saturation = s * 100.0
            result.Luminosity = l * 100.0
            Return result
        End Function

        ''' <summary>Offset each RGB channel by a signed delta, clamped to 0..255. Used by Grid border shading.</summary>
        Public Function ShiftChannels(ByVal c As Color, ByVal delta As Integer) As Color
            Return Color.FromArgb(Clamp(c.R + delta), Clamp(c.G + delta), Clamp(c.B + delta))
        End Function

        ''' <summary>Offset each RGB channel by independent signed deltas, clamped to 0..255.</summary>
        Public Function ShiftChannels(ByVal c As Color, ByVal dR As Integer, ByVal dG As Integer, ByVal dB As Integer) As Color
            Return Color.FromArgb(Clamp(c.R + dR), Clamp(c.G + dG), Clamp(c.B + dB))
        End Function

        Private Function Clamp(ByVal v As Integer) As Integer
            If v < 0 Then Return 0
            If v > 255 Then Return 255
            Return v
        End Function

        ''' <summary>
        ''' Per-pixel HSL fine-tune, offsetting each pixel's own hue/saturation/luminosity rather than
        ''' replacing it (unlike ImageTintHelper.Tint, which paints a whole new hue). Approximates the
        ''' external Quartz.Image.HSL call used by Label.ColorOfHue/Saturation/Luminosity; alpha is preserved.
        ''' hueOffset in degrees, saturation/luminosity offsets in percentage points (VB6 stored them as raw Longs).
        ''' </summary>
        Public Function ApplyHslShift(ByVal src As Image, ByVal hueOffset As Integer, ByVal saturationOffset As Integer, ByVal luminosityOffset As Integer) As Bitmap
            Dim bmp As New Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.DrawImage(src, New Rectangle(0, 0, bmp.Width, bmp.Height))
            End Using

            Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
            Dim data As BitmapData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim n As Integer = Math.Abs(data.Stride) * bmp.Height
                Dim buf(n - 1) As Byte
                Marshal.Copy(data.Scan0, buf, 0, n)

                Dim i As Integer = 0
                While i < n
                    Dim a0 As Integer = buf(i + 3)
                    If a0 <> 0 Then
                        Dim h As Double, s As Double, l As Double
                        RgbToHsl1(buf(i + 2), buf(i + 1), buf(i), h, s, l)

                        h = ((h + hueOffset) Mod 360.0 + 360.0) Mod 360.0
                        s = Math.Max(0.0, Math.Min(1.0, s + saturationOffset / 100.0))
                        l = Math.Max(0.0, Math.Min(1.0, l + luminosityOffset / 100.0))

                        Dim r As Integer, g0 As Integer, b As Integer
                        HslToRgb1(h, s, l, r, g0, b)
                        buf(i) = CByte(b)
                        buf(i + 1) = CByte(g0)
                        buf(i + 2) = CByte(r)
                    End If
                    i += 4
                End While

                Marshal.Copy(buf, 0, data.Scan0, n)
            Finally
                bmp.UnlockBits(data)
            End Try

            Return bmp
        End Function

        ' RGB 0-255 -> H:0-360, S/L:0-1
        Private Sub RgbToHsl1(ByVal r As Integer, ByVal g As Integer, ByVal b As Integer, ByRef h As Double, ByRef s As Double, ByRef l As Double)
            Dim rd As Double = r / 255.0, gd As Double = g / 255.0, bd As Double = b / 255.0
            Dim maxV As Double = Math.Max(rd, Math.Max(gd, bd))
            Dim minV As Double = Math.Min(rd, Math.Min(gd, bd))
            Dim d As Double = maxV - minV
            l = (maxV + minV) / 2.0

            If d <= 0.0 Then
                h = 0 : s = 0
                Return
            End If

            s = d / (1.0 - Math.Abs(2.0 * l - 1.0))
            If maxV = rd Then
                h = 60.0 * (((gd - bd) / d) Mod 6.0)
            ElseIf maxV = gd Then
                h = 60.0 * (((bd - rd) / d) + 2.0)
            Else
                h = 60.0 * (((rd - gd) / d) + 4.0)
            End If
            If h < 0 Then h += 360
        End Sub

        ' H:0-360, S/L:0-1 -> RGB 0-255
        Private Sub HslToRgb1(ByVal h As Double, ByVal s As Double, ByVal l As Double, ByRef r As Integer, ByRef g As Integer, ByRef b As Integer)
            If s <= 0.0 Then
                Dim v As Integer = Clamp255(l * 255.0)
                r = v : g = v : b = v
                Return
            End If

            Dim c As Double = (1.0 - Math.Abs(2.0 * l - 1.0)) * s
            Dim hp As Double = h / 60.0
            Dim x As Double = c * (1.0 - Math.Abs((hp Mod 2.0) - 1.0))
            Dim r1 As Double = 0, g1 As Double = 0, b1 As Double = 0

            Select Case CInt(Math.Floor(hp)) Mod 6
                Case 0 : r1 = c : g1 = x : b1 = 0
                Case 1 : r1 = x : g1 = c : b1 = 0
                Case 2 : r1 = 0 : g1 = c : b1 = x
                Case 3 : r1 = 0 : g1 = x : b1 = c
                Case 4 : r1 = x : g1 = 0 : b1 = c
                Case Else : r1 = c : g1 = 0 : b1 = x
            End Select

            Dim m As Double = l - c / 2.0
            r = Clamp255((r1 + m) * 255.0)
            g = Clamp255((g1 + m) * 255.0)
            b = Clamp255((b1 + m) * 255.0)
        End Sub

        Private Function Clamp255(ByVal v As Double) As Integer
            If v < 0 Then Return 0
            If v > 255 Then Return 255
            Return CInt(v)
        End Function

    End Module

End Namespace
