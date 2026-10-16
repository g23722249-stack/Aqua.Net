Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Window chrome styles shared by AquaForm and iForm:
'   MacOS     -- the original look: traffic-light close/minimize/maximize on the left, blue Apple in
'                AquaForm's menu bar.
'   KdePlasma -- KDE Plasma (Breeze) style: minimize/maximize/close line glyphs on the right, a round
'                hover background (red for close), and the bird logo instead of the Apple.
' Each form has its own ChromeStyle; Default follows FormChrome.DefaultStyle, so an application can
' switch every open window at once (PhotoEdit's Settings does this). The default is MacOS, so
' applications that never touch this (iPhoto) look exactly as before.
Namespace Global.Aqua

    Public Enum ChromeStyle
        ''' <summary>Follow FormChrome.DefaultStyle.</summary>
        [Default] = 0
        MacOS = 1
        KdePlasma = 2
    End Enum

    Public NotInheritable Class FormChrome
        Private Sub New()
        End Sub

        Private Shared _defaultStyle As ChromeStyle = ChromeStyle.MacOS

        ''' <summary>Raised when DefaultStyle changes; open AquaForm/iForm windows repaint themselves.</summary>
        Public Shared Event DefaultStyleChanged As EventHandler

        ''' <summary>The style of every form whose ChromeStyle is Default (MacOS unless the application changes it).</summary>
        Public Shared Property DefaultStyle As ChromeStyle
            Get
                Return _defaultStyle
            End Get
            Set(value As ChromeStyle)
                If value = ChromeStyle.Default Then value = ChromeStyle.MacOS
                If value = _defaultStyle Then Return
                _defaultStyle = value
                RaiseEvent DefaultStyleChanged(Nothing, EventArgs.Empty)
            End Set
        End Property

        Friend Shared Function Resolve(style As ChromeStyle) As ChromeStyle
            Return If(style = ChromeStyle.Default, _defaultStyle, style)
        End Function

        '=====================================================================
        ' KDE Plasma (Breeze) title-bar buttons
        '=====================================================================

        ''' <summary>Button size, gap and right margin (pixels) in a 23-pixel title bar.</summary>
        Friend Const KdeButtonSize As Integer = 18
        Friend Const KdeButtonGap As Integer = 4
        Friend Const KdeRightMargin As Integer = 6

        ''' <summary>
        ''' Where a KDE button goes: from the right edge, close first, then maximize, then minimize;
        ''' buttons that are turned off take no space.
        ''' </summary>
        Friend Shared Function KdeButtonRect(box As FormControlBox, formWidth As Integer, titleBarHeight As Integer,
                                             hasClose As Boolean, hasMax As Boolean) As Rectangle
            Dim x As Integer = formWidth - KdeRightMargin - KdeButtonSize
            Dim y As Integer = (titleBarHeight - KdeButtonSize) \ 2
            If box = FormControlBox.Close Then Return New Rectangle(x, y, KdeButtonSize, KdeButtonSize)
            If hasClose Then x -= KdeButtonSize + KdeButtonGap
            If box = FormControlBox.Maximize Then Return New Rectangle(x, y, KdeButtonSize, KdeButtonSize)
            If hasMax Then x -= KdeButtonSize + KdeButtonGap
            Return New Rectangle(x, y, KdeButtonSize, KdeButtonSize)   ' Minimize
        End Function

        ''' <summary>
        ''' One Breeze button: a thin line glyph (minimize ⌄, maximize ⌃ / restore ◇, close ✕). Hover adds a
        ''' soft round background (red for close, with a white glyph); pressed is a little darker.
        ''' </summary>
        Friend Shared Sub DrawKdeButton(g As Graphics, box As FormControlBox, r As Rectangle, active As Boolean,
                                        hover As Boolean, pressed As Boolean, maximized As Boolean)
            Dim state = g.Save()
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            Dim fore As Color
            If Theme.Dark Then
                fore = If(active, Color.FromArgb(222, 225, 230), Color.FromArgb(140, 145, 152))
            Else
                fore = If(active, Color.FromArgb(60, 64, 72), Color.FromArgb(150, 154, 160))
            End If
            If hover OrElse pressed Then
                Dim bg As Color
                If box = FormControlBox.Close Then
                    bg = If(pressed, Color.FromArgb(200, 40, 50), Color.FromArgb(232, 72, 85))
                    fore = Color.White
                ElseIf Theme.Dark Then
                    bg = Color.FromArgb(If(pressed, 110, 70), 255, 255, 255)
                Else
                    bg = Color.FromArgb(If(pressed, 90, 55), 60, 64, 72)
                End If
                Using b As New SolidBrush(bg)
                    g.FillEllipse(b, New RectangleF(r.X, r.Y, r.Width - 0.5F, r.Height - 0.5F))
                End Using
            End If
            Dim cx = r.X + r.Width / 2.0F, cy = r.Y + r.Height / 2.0F, s = r.Width * 0.22F
            Using p As New Pen(fore, Math.Max(1.3F, r.Width * 0.09F))
                p.StartCap = LineCap.Round : p.EndCap = LineCap.Round : p.LineJoin = LineJoin.Round
                Select Case box
                    Case FormControlBox.Minimize
                        g.DrawLines(p, {New PointF(cx - s, cy - s * 0.45F), New PointF(cx, cy + s * 0.55F), New PointF(cx + s, cy - s * 0.45F)})
                    Case FormControlBox.Maximize
                        If maximized Then
                            ' restore: a small diamond, like Breeze
                            g.DrawPolygon(p, {New PointF(cx, cy - s), New PointF(cx + s, cy), New PointF(cx, cy + s), New PointF(cx - s, cy)})
                        Else
                            g.DrawLines(p, {New PointF(cx - s, cy + s * 0.45F), New PointF(cx, cy - s * 0.55F), New PointF(cx + s, cy + s * 0.45F)})
                        End If
                    Case Else
                        g.DrawLine(p, cx - s, cy - s, cx + s, cy + s)
                        g.DrawLine(p, cx - s, cy + s, cx + s, cy - s)
                End Select
            End Using
            g.Restore(state)
        End Sub

        '=====================================================================
        ' Maximize like a native window
        '=====================================================================

        ''' <summary>
        ''' Where a maximized form goes. A borderless form maximized as-is covers the taskbar; a native
        ''' window stops at the screen's working area, so the form sets MaximizedBounds (Protected) to
        ''' this first -- relative to the monitor the form is on. Restore returns to the previous
        ''' position and size by itself.
        ''' </summary>
        Friend Shared Function MaximizedArea(f As Form) As Rectangle
            Dim scr = Screen.FromHandle(f.Handle)
            Dim wa = scr.WorkingArea
            Return New Rectangle(wa.X - scr.Bounds.X, wa.Y - scr.Bounds.Y, wa.Width, wa.Height)
        End Function
    End Class

End Namespace
