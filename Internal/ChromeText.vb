Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Text

Namespace Global.Aqua

    ''' <summary>
    ''' Text on the forms' own chrome (AquaForm / iForm title, AquaForm menu bar). Those are painted into
    ''' the form's back buffer bitmap, and GDI text (TextRenderer) drawn into a bitmap gets no smoothing
    ''' at all: hard stair-stepped glyphs, which in the grey of an inactive window read as a blur.
    ''' VB6 drew them with a Label straight on the screen, where Windows smooths text. GDI+ with
    ''' ClearType does smooth into a bitmap, as long as the background under the text is opaque (it is:
    ''' the title bar / menu bar art is drawn first).
    ''' </summary>
    Friend Module ChromeText

        ''' <summary>Size the text takes when drawn with Draw.</summary>
        Public Function Measure(ByVal g As Graphics, ByVal text As String, ByVal font As Font) As Size
            If String.IsNullOrEmpty(text) Then Return Size.Empty
            Dim old As TextRenderingHint = g.TextRenderingHint
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
            Try
                Using sf As StringFormat = Format()
                    Dim s As SizeF = g.MeasureString(text, font, New PointF(0, 0), sf)
                    Return New Size(CInt(Math.Ceiling(s.Width)), CInt(Math.Ceiling(s.Height)))
                End Using
            Finally
                g.TextRenderingHint = old
            End Try
        End Function

        ''' <summary>Draws <paramref name="text"/> centred in <paramref name="rect"/>, smoothed.</summary>
        Public Sub DrawCentered(ByVal g As Graphics, ByVal text As String, ByVal font As Font, ByVal rect As Rectangle, ByVal color As Color)
            If String.IsNullOrEmpty(text) Then Return
            Dim old As TextRenderingHint = g.TextRenderingHint
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
            Try
                Using sf As StringFormat = Format(), b As New SolidBrush(color)
                    sf.Alignment = StringAlignment.Center
                    sf.LineAlignment = StringAlignment.Center
                    g.DrawString(text, font, b, rect, sf)
                End Using
            Finally
                g.TextRenderingHint = old
            End Try
        End Sub

        ' one line, no clipping of overhanging glyphs, no trailing padding (tight like TextRenderer's)
        Private Function Format() As StringFormat
            Dim sf As New StringFormat(StringFormat.GenericTypographic)
            sf.FormatFlags = sf.FormatFlags Or StringFormatFlags.NoWrap Or StringFormatFlags.NoClip Or StringFormatFlags.MeasureTrailingSpaces
            sf.Trimming = StringTrimming.None
            Return sf
        End Function

    End Module

End Namespace
