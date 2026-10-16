Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging

' 深色配色（選用）：Dark = True 時，Aqua 控制項的面板貼圖（標題列、選單、分頁、滑桿軌道…）
' 自動轉成深色——灰階的像素依亮度反轉到深色範圍，有彩度的部分（藍色選取、紅綠燈按鈕、滑桿鈕）保留原色。
' 預設 False，不設定的程式（例如 iPhoto）外觀完全不變。
Namespace Global.Aqua

    Public NotInheritable Class Theme

        Private Sub New()
        End Sub

        Private Shared _dark As Boolean
        Private Shared ReadOnly _skins As New Dictionary(Of Image, Image)()
        Private Shared ReadOnly _sync As New Object()

        ''' <summary>配色改變時（Dark 換值）。</summary>
        Public Shared Event Changed As EventHandler

        ''' <summary>是否使用深色配色。</summary>
        Public Shared Property Dark() As Boolean
            Get
                Return _dark
            End Get
            Set(ByVal value As Boolean)
                If _dark = value Then Return
                _dark = value
                RaiseEvent Changed(Nothing, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>
        ''' 淺色配色的顏色換成目前配色：淺色時原樣傳回；深色時灰階的亮色（背景）變暗、暗色（文字）變亮，
        ''' 很淡的彩色（淺藍選取底）變成同色相的深色，鮮豔的中間色（強調色）不變。透明度保留。
        ''' </summary>
        Public Shared Function Map(ByVal c As Color) As Color
            If Not _dark OrElse c.A = 0 Then Return c
            Dim h As Single = c.GetHue(), s As Single = c.GetSaturation(), l As Single = c.GetBrightness()
            Dim nl As Single
            If l >= 0.5F Then
                ' 背景：白 → 0.12、淺灰 0.9 → 0.16、中灰 0.6 → 0.31
                If s > 0.35F AndAlso l < 0.78F Then Return c ' 鮮豔的強調色
                nl = 0.09F + (1 - l) * 0.55F
                s = Math.Min(s, 0.45F)
            Else
                ' 文字與線條：近黑 → 0.9，深灰 0.4 → 0.68
                If s > 0.35F AndAlso l > 0.22F Then Return c
                nl = 0.92F - l * 0.6F
                s = Math.Min(s, 0.25F)
            End If
            Return FromHsl(c.A, h, s, nl)
        End Function

        ''' <summary>深色時：主要文字色；淺色時 SystemColors.ControlText。</summary>
        Public Shared ReadOnly Property TextColor() As Color
            Get
                Return If(_dark, Color.FromArgb(226, 229, 234), SystemColors.ControlText)
            End Get
        End Property

        ''' <summary>
        ''' 面板貼圖換成目前配色（深色時轉暗，結果快取；淺色時原樣傳回）。
        ''' 灰階像素依亮度反轉：0.95 → 0.12、0.6 → 0.31；有彩度（飽和度 0.3 以上）的像素保留。
        ''' </summary>
        Public Shared Function Skin(ByVal img As Image) As Image
            If Not _dark OrElse img Is Nothing Then Return img
            SyncLock _sync
                Dim dark As Image = Nothing
                If _skins.TryGetValue(img, dark) Then Return dark
                Dim src As New Bitmap(img)
                Dim bmp As New Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb)
                For y As Integer = 0 To src.Height - 1
                    For x As Integer = 0 To src.Width - 1
                        Dim c As Color = src.GetPixel(x, y)
                        Dim s As Single = c.GetSaturation(), l As Single = c.GetBrightness()
                        If c.A = 0 Then
                            bmp.SetPixel(x, y, c)
                        ElseIf s < 0.3F Then
                            ' 灰階：亮度反轉到深色，幾乎不留彩度（避免淺灰紋理變成偏紫偏紅）
                            bmp.SetPixel(x, y, FromHsl(c.A, c.GetHue(), Math.Min(s * 0.5F, 0.06F), 0.07F + (1 - l) * 0.55F))
                        ElseIf l > 0.7F Then
                            ' 很淡的彩色（分頁邊緣的淡藍）：同色相轉深
                            bmp.SetPixel(x, y, FromHsl(c.A, c.GetHue(), s * 0.8F, 0.1F + (1 - l) * 0.8F))
                        Else
                            bmp.SetPixel(x, y, c) ' 鮮豔的部分（藍色選取、紅綠燈、滑桿鈕）保留
                        End If
                    Next
                Next
                src.Dispose()
                _skins(img) = bmp
                Return bmp
            End SyncLock
        End Function

        Private Shared Function FromHsl(ByVal a As Integer, ByVal h As Single, ByVal s As Single, ByVal l As Single) As Color
            l = Math.Max(0, Math.Min(1, l))
            If s <= 0 Then
                Dim v As Integer = CInt(Math.Round(l * 255))
                Return Color.FromArgb(a, v, v, v)
            End If
            Dim q As Single = If(l < 0.5F, l * (1 + s), l + s - l * s)
            Dim p As Single = 2 * l - q
            Dim hk As Single = h / 360.0F
            Return Color.FromArgb(a, Channel(p, q, hk + 1 / 3.0F), Channel(p, q, hk), Channel(p, q, hk - 1 / 3.0F))
        End Function

        Private Shared Function Channel(ByVal p As Single, ByVal q As Single, ByVal t As Single) As Integer
            If t < 0 Then t += 1
            If t > 1 Then t -= 1
            Dim v As Single
            If t < 1 / 6.0F Then
                v = p + (q - p) * 6 * t
            ElseIf t < 0.5F Then
                v = q
            ElseIf t < 2 / 3.0F Then
                v = p + (q - p) * (2 / 3.0F - t) * 6
            Else
                v = p
            End If
            Return CInt(Math.Round(Math.Max(0, Math.Min(1, v)) * 255))
        End Function
    End Class

End Namespace
