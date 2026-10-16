Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' ColorPicker 的色彩模型與共用工具。
' 內部一律以 HSV(double) 為唯一來源:純灰/純黑時 RGB 推不回色相,
' 若用 RGB 存,使用者把明度拉到 0 再拉回來色相會跳回紅色。
Namespace Global.Aqua

    ''' <summary>ColorPicker 的頁籤。</summary>
    Public Enum ColorPickerPage
        Wheel = 0       ' 色環
        Classic = 1     ' 經典
        Harmony = 2     ' 調和
        Palette = 3     ' 色票
        Favorites = 4   ' 常用(常用色 + 最近使用色)
    End Enum

    ''' <summary>調和頁的配色規則。</summary>
    Public Enum ColorHarmonyRule
        Complementary = 0       ' 互補
        Analogous = 1           ' 類似
        Triadic = 2             ' 三角
        SplitComplementary = 3  ' 分割互補
        Square = 4              ' 矩形(四方)
    End Enum

    ''' <summary>一組具名色票。</summary>
    Public Class ColorPalette
        Private ReadOnly _colors As New List(Of Color)()

        Public Sub New()
        End Sub

        Public Sub New(ByVal name As String, ByVal ParamArray colors() As Color)
            Me.Name = name
            _colors.AddRange(colors)
        End Sub

        Public Property Name As String
        Public ReadOnly Property Colors As List(Of Color)
            Get
                Return _colors
            End Get
        End Property

        ''' <summary>使用者色票才有(存於 文件\PainterTool\Palettes);內建色票為 Nothing。</summary>
        Friend Property FilePath As String

        ''' <summary>是否為使用者自建 / 匯入的色票(可編輯、可刪除)。</summary>
        Public ReadOnly Property IsUserPalette As Boolean
            Get
                Return FilePath IsNot Nothing
            End Get
        End Property
    End Class

    Friend Module ColorMath

        ''' <summary>HSV(H 0..360, S/V 0..1) 轉 32bpp ARGB 整數。</summary>
        Public Function HsvToArgb(ByVal h As Double, ByVal s As Double, ByVal v As Double,
                                  Optional ByVal alpha As Integer = 255) As Integer
            h = ((h Mod 360.0) + 360.0) Mod 360.0
            Dim c As Double = v * s
            Dim hp As Double = h / 60.0
            Dim x As Double = c * (1.0 - Math.Abs(hp Mod 2.0 - 1.0))
            Dim m As Double = v - c
            Dim r, g, b As Double
            Select Case CInt(Math.Floor(hp))
                Case 0 : r = c : g = x : b = 0
                Case 1 : r = x : g = c : b = 0
                Case 2 : r = 0 : g = c : b = x
                Case 3 : r = 0 : g = x : b = c
                Case 4 : r = x : g = 0 : b = c
                Case Else : r = c : g = 0 : b = x
            End Select
            Return (alpha << 24) Or (ToByte(r + m) << 16) Or (ToByte(g + m) << 8) Or ToByte(b + m)
        End Function

        Public Function HsvToColor(ByVal h As Double, ByVal s As Double, ByVal v As Double) As Color
            Return Color.FromArgb(HsvToArgb(h, s, v))
        End Function

        ''' <summary>RGB 轉 HSV。回傳 False 表示色相無定義(灰階),此時 h 不應採用。</summary>
        Public Function RgbToHsv(ByVal c As Color, ByRef h As Double, ByRef s As Double, ByRef v As Double) As Boolean
            Dim r As Double = c.R / 255.0
            Dim g As Double = c.G / 255.0
            Dim b As Double = c.B / 255.0
            Dim max As Double = Math.Max(r, Math.Max(g, b))
            Dim min As Double = Math.Min(r, Math.Min(g, b))
            Dim delta As Double = max - min
            v = max
            s = If(max = 0.0, 0.0, delta / max)
            If delta = 0.0 Then Return False
            If max = r Then
                h = 60.0 * (((g - b) / delta) Mod 6.0)
            ElseIf max = g Then
                h = 60.0 * ((b - r) / delta + 2.0)
            Else
                h = 60.0 * ((r - g) / delta + 4.0)
            End If
            If h < 0 Then h += 360.0
            Return True
        End Function

        ''' <summary>RGB 轉 HSL(H 0..360,S/L 0..1)。</summary>
        Public Sub RgbToHsl(ByVal c As Color, ByRef h As Double, ByRef s As Double, ByRef l As Double)
            Dim r As Double = c.R / 255.0, g As Double = c.G / 255.0, b As Double = c.B / 255.0
            Dim max As Double = Math.Max(r, Math.Max(g, b))
            Dim min As Double = Math.Min(r, Math.Min(g, b))
            Dim delta As Double = max - min
            l = (max + min) / 2.0
            s = If(delta = 0.0, 0.0, delta / (1.0 - Math.Abs(2.0 * l - 1.0)))
            ' 色相與 HSV 相同;灰階時維持 0。
            Dim v As Double, s2 As Double
            h = 0
            RgbToHsv(c, h, s2, v)
        End Sub

        ''' <summary>HSL(H 0..360,S/L 0..1)轉 Color。</summary>
        Public Function HslToColor(ByVal h As Double, ByVal s As Double, ByVal l As Double) As Color
            ' 經由 HSV:V = L + S·min(L, 1−L);Sv = 2(1 − L/V)
            l = Clamp01(l)
            s = Clamp01(s)
            Dim v As Double = l + s * Math.Min(l, 1.0 - l)
            Dim sv As Double = If(v = 0.0, 0.0, 2.0 * (1.0 - l / v))
            Return HsvToColor(h, sv, v)
        End Function

        ''' <summary>WCAG 2.x 相對亮度(0 = 黑,1 = 白)。</summary>
        Public Function RelativeLuminance(ByVal c As Color) As Double
            Return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B)
        End Function

        ''' <summary>WCAG 對比值,範圍 1..21。</summary>
        Public Function ContrastRatio(ByVal a As Color, ByVal b As Color) As Double
            Dim la As Double = RelativeLuminance(a), lb As Double = RelativeLuminance(b)
            Return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05)
        End Function

        Private Function Linear(ByVal channel As Byte) As Double
            Dim c As Double = channel / 255.0
            Return If(c <= 0.04045, c / 12.92, Math.Pow((c + 0.055) / 1.055, 2.4))
        End Function

        Public Function ToHex(ByVal c As Color) As String
            Return "#" & c.R.ToString("X2") & c.G.ToString("X2") & c.B.ToString("X2")
        End Function

        ''' <summary>接受 #RRGGBB、RRGGBB、#RGB。</summary>
        Public Function TryParseHex(ByVal text As String, ByRef result As Color) As Boolean
            If text Is Nothing Then Return False
            Dim t As String = text.Trim().TrimStart("#"c)
            If t.Length = 3 Then
                t = New String(New Char() {t(0), t(0), t(1), t(1), t(2), t(2)})
            End If
            If t.Length <> 6 Then Return False
            Dim n As Integer
            If Not Integer.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, n) Then Return False
            result = Color.FromArgb((n >> 16) And &HFF, (n >> 8) And &HFF, n And &HFF)
            Return True
        End Function

        ''' <summary>由 ARGB 像素陣列建立獨立點陣圖(不依附任何 stream)。</summary>
        Public Function MakeBitmap(ByVal width As Integer, ByVal height As Integer, ByVal pixels() As Integer) As Bitmap
            Dim bmp As New Bitmap(width, height, PixelFormat.Format32bppArgb)
            Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, width, height),
                                                  ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Try
                For y As Integer = 0 To height - 1
                    Marshal.Copy(pixels, y * width, New IntPtr(data.Scan0.ToInt64() + CLng(y) * data.Stride), width)
                Next
            Finally
                bmp.UnlockBits(data)
            End Try
            Return bmp
        End Function

        ''' <summary>色相角度 → 螢幕座標(0°=右、逆時針,與參考圖紅色在右一致)。</summary>
        Public Function PointOnCircle(ByVal cx As Double, ByVal cy As Double, ByVal r As Double, ByVal degrees As Double) As PointF
            Dim rad As Double = degrees * Math.PI / 180.0
            Return New PointF(CSng(cx + r * Math.Cos(rad)), CSng(cy - r * Math.Sin(rad)))
        End Function

        ''' <summary>螢幕座標 → 色相角度 0..360。</summary>
        Public Function AngleOf(ByVal dx As Double, ByVal dy As Double) As Double
            Dim a As Double = Math.Atan2(-dy, dx) * 180.0 / Math.PI
            If a < 0 Then a += 360.0
            Return a
        End Function

        Public Function Clamp01(ByVal x As Double) As Double
            If x < 0.0 Then Return 0.0
            If x > 1.0 Then Return 1.0
            Return x
        End Function

        Private Function ToByte(ByVal x As Double) As Integer
            Dim n As Integer = CInt(Math.Round(x * 255.0))
            If n < 0 Then Return 0
            If n > 255 Then Return 255
            Return n
        End Function

    End Module

    ''' <summary>各頁面共用的目前色彩(HSV 為唯一來源)。</summary>
    Friend NotInheritable Class ColorState
        Private _h As Double = 0.0
        Private _s As Double = 0.0
        Private _v As Double = 0.0

        Public Event Changed As EventHandler

        Public ReadOnly Property H As Double
            Get
                Return _h
            End Get
        End Property
        Public ReadOnly Property S As Double
            Get
                Return _s
            End Get
        End Property
        Public ReadOnly Property V As Double
            Get
                Return _v
            End Get
        End Property

        Public Sub SetHsv(ByVal h As Double, ByVal s As Double, ByVal v As Double)
            h = ((h Mod 360.0) + 360.0) Mod 360.0
            s = ColorMath.Clamp01(s)
            v = ColorMath.Clamp01(v)
            If h = _h AndAlso s = _s AndAlso v = _v Then Return
            _h = h : _s = s : _v = v
            RaiseEvent Changed(Me, EventArgs.Empty)
        End Sub

        ''' <summary>設定 RGB 時,灰階保留原色相、純黑保留原飽和度。</summary>
        Public Property Color As Color
            Get
                Return ColorMath.HsvToColor(_h, _s, _v)
            End Get
            Set(ByVal value As Color)
                Dim h As Double = _h, s As Double, v As Double
                ColorMath.RgbToHsv(value, h, s, v)
                If v = 0.0 Then s = _s
                SetHsv(h, s, v)
            End Set
        End Property
    End Class

    ''' <summary>ColorPicker / ColorPickerDialog 的配色。</summary>
    Public Enum ColorPickerTheme
        Dark = 0    ' 深色
        Light = 1   ' 淺色
        Auto = 2    ' 跟著 Aqua.Theme.Dark(預設)
    End Enum

    ''' <summary>持有主題的視窗元件(ColorPicker、ColorPickerDialog、NamePrompt)。</summary>
    Friend Interface IThemeHost
        ReadOnly Property Scheme As PickerTheme
    End Interface

    ''' <summary>一組配色。內部控制項以 <see cref="[Of]"/> 往上找所屬主題取色。</summary>
    Friend NotInheritable Class PickerTheme

        ''' <summary>深色:維持原本版面配色(對應色彩選取器參考圖)。</summary>
        Public Shared ReadOnly Dark As New PickerTheme() With {
            .IsDark = True,
            .Back = Color.FromArgb(43, 43, 46),
            .Panel = Color.FromArgb(30, 30, 32),
            .Segment = Color.FromArgb(86, 86, 90),
            .Border = Color.FromArgb(70, 70, 74),
            .Text = Color.FromArgb(228, 228, 230),
            .SubText = Color.FromArgb(160, 160, 165),
            .Heading = Color.FromArgb(228, 228, 230),
            .EmptySlot = Color.FromArgb(96, 96, 100),
            .ButtonBack = Color.FromArgb(86, 86, 90),
            .ThumbFill = Color.FromArgb(235, 235, 238),
            .ThumbEdge = Color.FromArgb(120, 0, 0, 0),
            .ScrollThumb = Color.FromArgb(86, 86, 90),
            .HotRing = Color.White}

        ''' <summary>淺色:參考 PhotoEdit 調整面板(淺灰底、白色內容區、淡藍選取、藍色滑桿把手)。</summary>
        Public Shared ReadOnly Light As New PickerTheme() With {
            .IsDark = False,
            .Back = Color.FromArgb(237, 237, 237),
            .Panel = Color.White,
            .Segment = Color.FromArgb(207, 227, 248),
            .Border = Color.FromArgb(180, 180, 180),
            .Text = Color.FromArgb(34, 34, 34),
            .SubText = Color.FromArgb(110, 110, 110),
            .Heading = Color.FromArgb(31, 63, 120),
            .EmptySlot = Color.FromArgb(189, 189, 189),
            .ButtonBack = Color.White,
            .ThumbFill = Color.FromArgb(74, 144, 226),
            .ThumbEdge = Color.FromArgb(47, 109, 181),
            .ScrollThumb = Color.FromArgb(200, 200, 200),
            .HotRing = Color.FromArgb(61, 127, 214)}

        Public IsDark As Boolean
        Public Back As Color          ' 控制項底色
        Public Panel As Color         ' 頁籤列、輸入框、色環內圈等內容區
        Public Segment As Color       ' 選取中的頁籤
        Public Border As Color        ' 框線、分隔線
        Public Text As Color
        Public SubText As Color       ' 次要文字(目前 / 歷史、摺疊箭頭)
        Public Heading As Color       ' 色票分組標題
        Public EmptySlot As Color     ' 空色槽外框
        Public ButtonBack As Color
        Public ThumbFill As Color     ' 滑桿把手
        Public ThumbEdge As Color
        Public ScrollThumb As Color
        Public HotRing As Color       ' 色塊滑過的外圈

        ''' <summary>同一組配色、只換底色(嵌在別的面板裡時和面板同色)。</summary>
        Public Function WithBack(ByVal back As Color) As PickerTheme
            Dim c As PickerTheme = DirectCast(MemberwiseClone(), PickerTheme)
            c.Back = back
            c.Panel = back
            Return c
        End Function

        Public Shared Function [For](ByVal theme As ColorPickerTheme) As PickerTheme
            If theme = ColorPickerTheme.Auto Then Return If(Global.Aqua.Theme.Dark, Dark, Light)
            Return If(theme = ColorPickerTheme.Light, Light, Dark)
        End Function

        ''' <summary>往上找第一個主題宿主;找不到(例如還沒加入父控制項)時用深色。</summary>
        Public Shared Function [Of](ByVal c As Control) As PickerTheme
            While c IsNot Nothing
                Dim host As IThemeHost = TryCast(c, IThemeHost)
                If host IsNot Nothing Then Return host.Scheme
                c = c.Parent
            End While
            Return Dark
        End Function

        ''' <summary>套用到標準 WinForms 按鈕(平面樣式)。</summary>
        Public Sub StyleButton(ByVal b As System.Windows.Forms.Button)
            b.FlatStyle = FlatStyle.Flat
            b.BackColor = ButtonBack
            b.ForeColor = Text
            b.FlatAppearance.BorderColor = Border
        End Sub

        ''' <summary>套用到 TextBox / ComboBox 等輸入元件。</summary>
        Public Sub StyleInput(ByVal c As Control)
            c.BackColor = Panel
            c.ForeColor = Text
        End Sub

    End Class

    ''' <summary>
    ''' 視窗標題列配色(DWM)。Win10 1809+ 支援深色標題列;
    ''' Win11 另可指定標題列底色 / 文字色 / 外框色。舊版 Windows 呼叫失敗即忽略,維持系統預設。
    ''' </summary>
    Friend Module TitleBarTheme

        Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
        Private Const DWMWA_BORDER_COLOR As Integer = 34
        Private Const DWMWA_CAPTION_COLOR As Integer = 35
        Private Const DWMWA_TEXT_COLOR As Integer = 36

        <System.Runtime.InteropServices.DllImport("dwmapi.dll")>
        Private Function DwmSetWindowAttribute(ByVal hwnd As IntPtr, ByVal attr As Integer, ByRef value As Integer, ByVal size As Integer) As Integer
        End Function

        Public Sub Apply(ByVal form As Form, ByVal scheme As PickerTheme)
            If Not form.IsHandleCreated Then Return
            Try
                Dim dark As Integer = If(scheme.IsDark, 1, 0)
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, dark, 4)
                Dim caption As Integer = ColorRef(scheme.Back)
                Dim text As Integer = ColorRef(scheme.Text)
                Dim border As Integer = ColorRef(scheme.Border)
                DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, caption, 4)
                DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, text, 4)
                DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, border, 4)
            Catch ex As DllNotFoundException
            Catch ex As EntryPointNotFoundException
            End Try
        End Sub

        ''' <summary>Win32 COLORREF = 0x00BBGGRR。</summary>
        Private Function ColorRef(ByVal c As Color) As Integer
            Return c.R Or (CInt(c.G) << 8) Or (CInt(c.B) << 16)
        End Function

    End Module

End Namespace
