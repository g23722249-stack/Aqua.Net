Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 色彩選取器。頁籤:色環 / 經典 / 調和 / 色票 / 常用(常用色與最近使用色);右側共用「目前 → 歷史」與
    ''' 色相、飽和度、明度、紅、綠、藍滑桿及 HEX 輸入;下方橫跨整個寬度的是滴管、複製、加入色票、對比預覽與明暗變化。
    ''' 滑鼠在頁籤列上滾動可循環切換頁籤;ShowCollapseButton 時頁籤列右端有縮小按鈕,縮小後只剩頁籤列,
    ''' 再按一次或點任何頁籤就還原(宿主視窗依 CollapsedChanged 與 PreferredHeight 調整高度)。
    ''' 所有頁面只讀寫同一個 ColorState(HSV),本控制項負責把變動同步到各處。
    ''' </summary>
    <DefaultEvent("ColorChanged"), DefaultProperty("SelectedColor")>
    <DesignerCategory("Code")>   ' 版面為手寫;避免 VS 按兩下開設計檢視而自動產生空 .resx / InitializeComponent
    Public Class ColorPicker
        Inherits UserControl
        Implements IThemeHost

        Private Const TabHeight As Integer = 26
        Private Const Pad As Integer = 8
        Private Const RecentCount As Integer = 7

        ''' <summary>
        ''' 版面尺寸。一般版:右側 6 條滑桿(HSV＋RGB)、下方工具列兩列(按鈕＋對比預覽、明暗變化)。
        ''' 精簡版(Compact,浮動視窗用):右側較窄、一次 3 條滑桿(HSV 與 RGB 切換)、下方工具列一列(按鈕＋明暗變化,不顯示對比預覽)。
        ''' </summary>
        Private Structure Metrics
            Public RightW, Row, CaptionW, ValueW, BoxW, BoxH, LabelH, SliderCount, ToolsH As Integer
        End Structure

        Private Shared Function MetricsFor(ByVal compact As Boolean) As Metrics
            If compact Then
                Return New Metrics With {.RightW = 150, .Row = 20, .CaptionW = 36, .ValueW = 34, .BoxW = 46, .BoxH = 26, .LabelH = 16,
                                         .SliderCount = 3, .ToolsH = PickerTools.CompactHeight}
            End If
            Return New Metrics With {.RightW = 200, .Row = 22, .CaptionW = 44, .ValueW = 40, .BoxW = 56, .BoxH = 36, .LabelH = 20,
                                     .SliderCount = 6, .ToolsH = PickerTools.WideHeight}
        End Function

        ''' <summary>右側一欄(目前/歷史、滑桿、HEX)需要的高度,主區至少要這麼高。</summary>
        Private Shared Function MinMainFor(ByVal compact As Boolean) As Integer
            Dim m As Metrics = MetricsFor(compact)
            Return m.BoxH + m.LabelH + m.SliderCount * m.Row + 8 + 22 + 4
        End Function

        Private _compact As Boolean
        ''' <summary>精簡版:一次 3 條滑桿時顯示 RGB(False = HSV)。</summary>
        Private _rgbSliders As Boolean
        Private _modeRect As Rectangle

        Private Shared ReadOnly PresetColors As Color() = {
            Color.Black, Color.FromArgb(128, 128, 128), Color.FromArgb(128, 0, 0), Color.FromArgb(230, 40, 40),
            Color.FromArgb(245, 130, 30), Color.FromArgb(250, 220, 30), Color.FromArgb(150, 60, 170),
            Color.FromArgb(120, 70, 190), Color.FromArgb(30, 160, 70), Color.FromArgb(20, 150, 230),
            Color.FromArgb(60, 70, 200), Color.FromArgb(170, 200, 40), Color.FromArgb(140, 210, 230),
            Color.FromArgb(200, 180, 230), Color.White, Color.FromArgb(190, 190, 190),
            Color.FromArgb(150, 100, 60), Color.FromArgb(245, 170, 190), Color.FromArgb(250, 235, 190),
            Color.FromArgb(180, 220, 40), Color.FromArgb(215, 190, 240)}

        Private ReadOnly _state As New ColorState()
        Private ReadOnly _palettes As List(Of ColorPalette) = PalettePage.DefaultPalettes()
        Private ReadOnly _recent As New List(Of Color)()
        Private ReadOnly _custom As Color() = CType(PresetColors.Clone(), Color())
        Private _userDataLoaded As Boolean
        Private _original As Color = Color.Black
        Private _syncing As Boolean

        Private ReadOnly _tabs As New SegmentBar()
        Private ReadOnly _wheel As HueRingPage
        Private ReadOnly _classic As ClassicPage
        Private ReadOnly _harmony As HarmonyPage
        Private ReadOnly _book As PalettePage
        Private ReadOnly _favorites As FavoritesPage
        Private ReadOnly _tools As PickerTools
        Private _collapsed As Boolean
        Private ReadOnly _presetGrid As New SwatchGrid()
        Private ReadOnly _recentGrid As New SwatchGrid()
        Private ReadOnly _sliders(5) As ColorSlider
        Private ReadOnly _hex As New System.Windows.Forms.TextBox()

        Private Shared ReadOnly SliderCaptions As String() = {"色相", "飽和度", "明度", "紅", "綠", "藍"}
        Private Shared ReadOnly CompactCaptions As String() = {"色相", "飽和", "明度", "紅", "綠", "藍"}

        ' 由 ArrangeLayout 算出、OnPaint 使用的區域
        Private _currentRect, _historyRect As Rectangle
        Private _rowTop, _rightX, _rightW As Integer

        ''' <summary>目前色彩變動(拖曳時連續觸發)。</summary>
        Public Event ColorChanged As EventHandler

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            _wheel = New HueRingPage(_state)
            _classic = New ClassicPage(_state)
            _harmony = New HarmonyPage(_state)
            _book = New PalettePage(_state, _palettes)
            _favorites = New FavoritesPage(_presetGrid, _recentGrid)
            _tools = New PickerTools(_state, _book) With {.Wide = True}
            BuildLayout()
            LoadUserData()
            AddHandler _state.Changed, AddressOf OnStateChanged
            AddHandler Global.Aqua.Theme.Changed, AddressOf OnAquaThemeChanged
            SyncFromState()
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            ' Aqua.Theme.Changed 是共用事件,不解除會讓關掉的選色器一直留在記憶體裡。
            If disposing Then RemoveHandler Global.Aqua.Theme.Changed, AddressOf OnAquaThemeChanged
            MyBase.Dispose(disposing)
        End Sub

        Private Sub OnAquaThemeChanged(ByVal sender As Object, ByVal e As EventArgs)
            If _theme <> ColorPickerTheme.Auto Then Return
            ApplyTheme()
            RaiseEvent ThemeChanged(Me, EventArgs.Empty)
        End Sub

        ''' <summary>
        ''' 使用者資料(色票、常用色、最近使用色)的資料夾;空白 = 文件\PainterTool(iPhoto、PhotoEdit 共用)。
        ''' 測試時指到暫存資料夾,才不會改到使用者的資料。要在建立選色器之前設定。
        ''' </summary>
        Public Shared Property UserDataFolder As String

        ' 刻意不叫 InitializeComponent:手寫版面若用該名稱,VS 設計工具會試圖解析而無法開啟。
        Private Sub BuildLayout()
            Font = New Font("Microsoft JhengHei UI", 9.0F)
            Size = New Size(470, PreferredHeightFor(470))

            _tabs.Items = New String() {"色環", "經典", "調和", "色票", "常用"}
            AddHandler _tabs.SelectedIndexChanged, Sub(s, e) ShowPage()
            AddHandler _tabs.SegmentClicked, Sub(s, i) Collapsed = False   ' 縮小時點頁籤:還原並切到該頁
            AddHandler _tabs.ToggleClicked, Sub(s, e) Collapsed = Not Collapsed
            Controls.Add(_tabs)

            For Each page As Control In New Control() {_wheel, _classic, _harmony, _book, _favorites}
                Controls.Add(page)
            Next
            AddHandler _book.ColorPicked, AddressOf OnSwatchPicked

            _presetGrid.Colors = _custom
            _presetGrid.Editable = True
            AddHandler _presetGrid.ColorPicked, AddressOf OnSwatchPicked
            AddHandler _presetGrid.SlotRightClicked, AddressOf OnCustomSlotRightClicked
            AddHandler _recentGrid.ColorPicked, AddressOf OnSwatchPicked
            RefreshRecentGrid()

            For i As Integer = 0 To _sliders.Length - 1
                Dim s As New ColorSlider() With {.Maximum = If(i = 0, 360.0, If(i <= 2, 100.0, 255.0))}
                AddHandler s.ValueChanged, AddressOf OnSliderChanged
                _sliders(i) = s
                Controls.Add(s)
            Next

            _hex.BorderStyle = BorderStyle.FixedSingle
            _hex.MaxLength = 7
            _hex.TextAlign = HorizontalAlignment.Right
            AddHandler _hex.KeyDown, Sub(s, e)
                                         If e.KeyCode = Keys.Enter Then
                                             ApplyHex()
                                             e.SuppressKeyPress = True
                                         End If
                                     End Sub
            AddHandler _hex.Leave, Sub(s, e) ApplyHex()
            Controls.Add(_hex)
            Controls.Add(_tools)

            ApplyTheme()
            ShowPage()
        End Sub

#Region "主題"

        Private _theme As ColorPickerTheme = ColorPickerTheme.Auto

        ''' <summary>配色:Auto 跟著 Aqua.Theme.Dark(預設)、Dark 深色、Light 淺色。</summary>
        <Category("Appearance"), DefaultValue(GetType(ColorPickerTheme), "Auto"), Description("配色:Auto 跟著 Aqua.Theme、Dark 深色、Light 淺色。")>
        Public Property Theme As ColorPickerTheme
            Get
                Return _theme
            End Get
            Set(ByVal value As ColorPickerTheme)
                If value = _theme Then Return
                _theme = value
                ApplyTheme()
                RaiseEvent ThemeChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Theme 變更後觸發(ColorPickerDialog 用來同步視窗與標題列)。</summary>
        Public Event ThemeChanged As EventHandler

        Private ReadOnly Property Scheme As PickerTheme Implements IThemeHost.Scheme
            Get
                Return PickerTheme.For(_theme)
            End Get
        End Property

        ''' <summary>
        ''' 自繪子控制項在 OnPaint 時以 PickerTheme.Of 取色,只需重繪;
        ''' 標準 WinForms 元件(TextBox / ComboBox / Button)則要明確上色。
        ''' </summary>
        Private Sub ApplyTheme()
            Dim s As PickerTheme = Scheme
            BackColor = s.Back
            ForeColor = s.Text
            s.StyleInput(_hex)
            _harmony.ApplyTheme(s)
            _book.ApplyTheme(s)
            _tools.ApplyTheme(s)
            Invalidate(True)
        End Sub

#End Region

#Region "公開 API"

        ''' <summary>目前選取的色彩(不含透明度)。</summary>
        <Category("Appearance"), Description("目前選取的色彩。")>
        Public Property SelectedColor As Color
            Get
                Return _state.Color
            End Get
            Set(ByVal value As Color)
                _state.Color = Color.FromArgb(255, value)
            End Set
        End Property

        ''' <summary>「歷史」色塊顯示的原始色彩;點它可還原。</summary>
        <Category("Appearance"), Description("「歷史」色塊顯示的原始色彩,點擊可還原。")>
        Public Property OriginalColor As Color
            Get
                Return _original
            End Get
            Set(ByVal value As Color)
                _original = Color.FromArgb(255, value)
                Invalidate()
            End Set
        End Property

        <Category("Behavior"), DefaultValue(GetType(ColorPickerPage), "Wheel")>
        Public Property SelectedPage As ColorPickerPage
            Get
                Return CType(_tabs.SelectedIndex, ColorPickerPage)
            End Get
            Set(ByVal value As ColorPickerPage)
                _tabs.SelectedIndex = CInt(value)
            End Set
        End Property

        <Category("Behavior"), DefaultValue(GetType(ColorHarmonyRule), "Complementary")>
        Public Property HarmonyRule As ColorHarmonyRule
            Get
                Return _harmony.Rule
            End Get
            Set(ByVal value As ColorHarmonyRule)
                _harmony.Rule = value
            End Set
        End Property

        ''' <summary>色票頁的分組;更動後呼叫 <see cref="RefreshPalettes"/>。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Palettes As List(Of ColorPalette)
            Get
                Return _palettes
            End Get
        End Property

        Public Sub RefreshPalettes()
            _book.RefreshPalettes()
        End Sub

        ''' <summary>頁籤列右端顯示縮小 / 還原按鈕(浮動視窗用;對話框不需要)。</summary>
        <Category("Behavior"), DefaultValue(False), Description("頁籤列右端顯示縮小 / 還原按鈕。")>
        Public Property ShowCollapseButton As Boolean
            Get
                Return _tabs.ShowToggle
            End Get
            Set(ByVal value As Boolean)
                _tabs.ShowToggle = value
                If Not value Then Collapsed = False
            End Set
        End Property

        ''' <summary>縮小到只剩頁籤列。控制項本身的高度由宿主依 PreferredHeight 調整。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Collapsed As Boolean
            Get
                Return _collapsed
            End Get
            Set(ByVal value As Boolean)
                If value = _collapsed Then Return
                _collapsed = value
                _tabs.Collapsed = value
                For Each c As Control In Controls
                    If c IsNot _tabs Then c.Visible = Not value
                Next
                If Not value Then
                    ShowPage()
                    UpdateSliderVisibility()
                End If
                Invalidate()
                RaiseEvent CollapsedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Collapsed 改變(宿主視窗跟著改高度)。</summary>
        Public Event CollapsedChanged As EventHandler

        ''' <summary>縮小時的高度:只剩頁籤列。</summary>
        Public Shared ReadOnly Property CollapsedHeight As Integer
            Get
                Return Pad + TabHeight + Pad
            End Get
        End Property

        ''' <summary>目前狀態(展開 / 縮小)在這個寬度下需要的高度。</summary>
        <Browsable(False)>
        Public ReadOnly Property PreferredHeight As Integer
            Get
                Return If(_collapsed, CollapsedHeight, PreferredHeightFor(Width, _compact))
            End Get
        End Property

        ''' <summary>
        ''' 精簡版(浮動視窗用,畫圖時不擋畫面):右側一次 3 條滑桿(點 HEX 左邊的「RGB／HSV」切換),
        ''' 下方工具列只有一列(滴管、複製、加入色票＋明暗變化),不顯示對比預覽。
        ''' </summary>
        <Category("Appearance"), DefaultValue(False), Description("精簡版:右側 3 條滑桿、工具列一列。")>
        Public Property Compact As Boolean
            Get
                Return _compact
            End Get
            Set(ByVal value As Boolean)
                If value = _compact Then Return
                _compact = value
                _tools.Compact = value
                UpdateSliderVisibility()
                PerformLayout()
                Invalidate()
            End Set
        End Property

        ''' <summary>精簡版看得到的滑桿(_sliders 的索引)。</summary>
        Private Function VisibleSliders() As Integer()
            If Not _compact Then Return New Integer() {0, 1, 2, 3, 4, 5}
            Return If(_rgbSliders, New Integer() {3, 4, 5}, New Integer() {0, 1, 2})
        End Function

        Private Sub UpdateSliderVisibility()
            Dim shown As Integer() = VisibleSliders()
            For i As Integer = 0 To _sliders.Length - 1
                _sliders(i).Visible = Not _collapsed AndAlso Array.IndexOf(shown, i) >= 0
            Next
        End Sub

        ''' <summary>展開時:頁籤列 + 主區(正方形,邊長由寬度決定)+ 下方工具列。</summary>
        Public Shared Function PreferredHeightFor(ByVal width As Integer, Optional ByVal compact As Boolean = False) As Integer
            Dim m As Metrics = MetricsFor(compact)
            Dim main As Integer = Math.Max(MinMainFor(compact), width - m.RightW - 3 * Pad)
            Return Pad + TabHeight + Pad + main + Pad + m.ToolsH + Pad
        End Function

        ''' <summary>精簡版最小的寬度(主區剛好放得下右側一欄的高度)。</summary>
        Public Shared ReadOnly Property CompactMinWidth As Integer
            Get
                Return MinMainFor(True) + MetricsFor(True).RightW + 3 * Pad
            End Get
        End Property

        ''' <summary>最近使用色(新到舊,最多 7 個)。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property RecentColors As Color()
            Get
                Return _recent.ToArray()
            End Get
            Set(ByVal value As Color())
                _recent.Clear()
                If value IsNot Nothing Then
                    For Each c As Color In value
                        If Not c.IsEmpty AndAlso _recent.Count < RecentCount Then _recent.Add(Color.FromArgb(255, c))
                    Next
                End If
                RefreshRecentGrid()
            End Set
        End Property

        ''' <summary>存檔的最近使用色(新到舊);沒有時是空的。不必建立選色器就能讀(快速面板用)。</summary>
        Public Shared Function SavedRecentColors() As Color()
            Try
                Dim list As List(Of Color) = PaletteStore.LoadColorList(PaletteStore.RecentColorsFile)
                If list IsNot Nothing Then
                    If list.Count > RecentCount Then list.RemoveRange(RecentCount, list.Count - RecentCount)
                    Return list.ToArray()
                End If
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            End Try
            Return New Color() {}
        End Function

        ''' <summary>存檔的常用色(21 格);沒有改過時是內建的預設。</summary>
        Public Shared Function SavedCustomColors() As Color()
            Dim result As Color() = CType(PresetColors.Clone(), Color())
            Try
                Dim list As List(Of Color) = PaletteStore.LoadColorList(PaletteStore.CustomColorsFile)
                If list IsNot Nothing Then
                    For i As Integer = 0 To Math.Min(list.Count, result.Length) - 1
                        result(i) = list(i)
                    Next
                End If
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            End Try
            Return result
        End Function

        ''' <summary>把顏色記進存檔的最近使用色(不必建立選色器;開著的選色器要另外更新 RecentColors)。</summary>
        Public Shared Sub RememberRecentColor(ByVal c As Color)
            c = Color.FromArgb(255, c)
            Dim list As New List(Of Color)(SavedRecentColors())
            list.RemoveAll(Function(x) x.ToArgb() = c.ToArgb())
            list.Insert(0, c)
            If list.Count > RecentCount Then list.RemoveRange(RecentCount, list.Count - RecentCount)
            Try
                PaletteStore.SaveColorList(PaletteStore.RecentColorsFile, list)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            End Try
        End Sub

        ''' <summary>把色彩放到最近使用色最前面(重複者移前),並存到 文件\PainterTool\RecentColors.txt。</summary>
        Public Sub AddRecentColor(ByVal c As Color)
            c = Color.FromArgb(255, c)
            _recent.RemoveAll(Function(x) x.ToArgb() = c.ToArgb())
            _recent.Insert(0, c)
            If _recent.Count > RecentCount Then _recent.RemoveRange(RecentCount, _recent.Count - RecentCount)
            RefreshRecentGrid()
            SaveQuietly(PaletteStore.RecentColorsFile, _recent)
        End Sub

#End Region

#Region "使用者資料(文件\PainterTool)"

        ''' <summary>
        ''' 建構時就載入,呼叫端之後仍可改寫 RecentColors 等。
        ''' VS 設計工具內不讀寫使用者資料夾。
        ''' </summary>
        Private Sub LoadUserData()
            If LicenseManager.UsageMode = LicenseUsageMode.Designtime Then Return
            _userDataLoaded = True
            Try
                Dim recent As List(Of Color) = PaletteStore.LoadColorList(PaletteStore.RecentColorsFile)
                If recent IsNot Nothing Then RecentColors = recent.ToArray()

                Dim custom As List(Of Color) = PaletteStore.LoadColorList(PaletteStore.CustomColorsFile)
                If custom IsNot Nothing Then
                    For i As Integer = 0 To Math.Min(custom.Count, _custom.Length) - 1
                        _custom(i) = custom(i)
                    Next
                End If

                _palettes.AddRange(PaletteStore.LoadUserPalettes())
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' 使用者資料讀不到時退回內建預設,不影響選色。
            End Try
            _presetGrid.Invalidate()
            _book.RefreshPalettes()
        End Sub

        ''' <summary>最近使用色 / 自訂色屬輔助資料,存檔失敗不打斷選色流程。</summary>
        Private Sub SaveQuietly(ByVal file As String, ByVal colors As IList(Of Color))
            If Not _userDataLoaded Then Return
            Try
                PaletteStore.SaveColorList(file, colors)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            End Try
        End Sub

        ''' <summary>常用色(自訂色)右鍵:以目前顏色取代 / 全部還原預設。</summary>
        Private Sub OnCustomSlotRightClicked(ByVal sender As Object, ByVal index As Integer, ByVal location As Point)
            Dim menu As New ContextMenuStrip()
            menu.Items.Add("設為目前顏色", Nothing, Sub(s, e)
                                                  _custom(index) = _state.Color
                                                  _presetGrid.Invalidate()
                                                  SaveQuietly(PaletteStore.CustomColorsFile, _custom)
                                              End Sub)
            menu.Items.Add("全部還原預設", Nothing, Sub(s, e)
                                                  Array.Copy(PresetColors, _custom, PresetColors.Length)
                                                  _presetGrid.Invalidate()
                                                  If _userDataLoaded Then
                                                      Try
                                                          PaletteStore.DeleteFile(PaletteStore.CustomColorsFile)
                                                      Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                                                      End Try
                                                  End If
                                              End Sub)
            AddHandler menu.Closed, Sub(s, e) menu.BeginInvoke(New MethodInvoker(AddressOf menu.Dispose))
            menu.Show(_presetGrid, location)
        End Sub

#End Region

        Private Sub RefreshRecentGrid()
            Dim slots(RecentCount - 1) As Color
            For i As Integer = 0 To RecentCount - 1
                slots(i) = If(i < _recent.Count, _recent(i), Color.Empty)
            Next
            _recentGrid.Colors = slots
        End Sub

        Private Sub ShowPage()
            If _collapsed Then Return
            Dim i As Integer = _tabs.SelectedIndex
            _wheel.Visible = (i = 0)
            _classic.Visible = (i = 1)
            _harmony.Visible = (i = 2)
            _book.Visible = (i = 3)
            _favorites.Visible = (i = 4)
        End Sub

        Private Sub OnSwatchPicked(ByVal sender As Object, ByVal c As Color)
            _state.Color = c
        End Sub

        Private Sub OnSliderChanged(ByVal sender As Object, ByVal e As EventArgs)
            If _syncing Then Return
            Dim c As Color = _state.Color
            Select Case Array.IndexOf(_sliders, sender)
                Case 0 : _state.SetHsv(_sliders(0).Value, _state.S, _state.V)
                Case 1 : _state.SetHsv(_state.H, _sliders(1).Value / 100.0, _state.V)
                Case 2 : _state.SetHsv(_state.H, _state.S, _sliders(2).Value / 100.0)
                Case 3 : _state.Color = Color.FromArgb(CInt(_sliders(3).Value), c.G, c.B)
                Case 4 : _state.Color = Color.FromArgb(c.R, CInt(_sliders(4).Value), c.B)
                Case 5 : _state.Color = Color.FromArgb(c.R, c.G, CInt(_sliders(5).Value))
            End Select
        End Sub

        Private Sub ApplyHex()
            Dim c As Color
            If ColorMath.TryParseHex(_hex.Text, c) Then
                _state.Color = c
            End If
            _hex.Text = ColorMath.ToHex(_state.Color)
        End Sub

        Private Sub OnStateChanged(ByVal sender As Object, ByVal e As EventArgs)
            SyncFromState()
            RaiseEvent ColorChanged(Me, EventArgs.Empty)
        End Sub

        ''' <summary>把 ColorState 推到滑桿、HEX 與各頁面。</summary>
        Private Sub SyncFromState()
            _syncing = True
            Try
                Dim h As Double = _state.H, s As Double = _state.S, v As Double = _state.V
                Dim c As Color = _state.Color
                _sliders(0).Value = h
                _sliders(1).Value = s * 100
                _sliders(2).Value = v * 100
                _sliders(3).Value = c.R
                _sliders(4).Value = c.G
                _sliders(5).Value = c.B
                _sliders(0).GradientColors = New Color() {
                    ColorMath.HsvToColor(0, 1, 1), ColorMath.HsvToColor(60, 1, 1), ColorMath.HsvToColor(120, 1, 1),
                    ColorMath.HsvToColor(180, 1, 1), ColorMath.HsvToColor(240, 1, 1), ColorMath.HsvToColor(300, 1, 1),
                    ColorMath.HsvToColor(360, 1, 1)}
                _sliders(1).GradientColors = New Color() {ColorMath.HsvToColor(h, 0, v), ColorMath.HsvToColor(h, 1, v)}
                _sliders(2).GradientColors = New Color() {Color.Black, ColorMath.HsvToColor(h, s, 1)}
                _sliders(3).GradientColors = New Color() {Color.FromArgb(0, c.G, c.B), Color.FromArgb(255, c.G, c.B)}
                _sliders(4).GradientColors = New Color() {Color.FromArgb(c.R, 0, c.B), Color.FromArgb(c.R, 255, c.B)}
                _sliders(5).GradientColors = New Color() {Color.FromArgb(c.R, c.G, 0), Color.FromArgb(c.R, c.G, 255)}
                If Not _hex.Focused Then _hex.Text = ColorMath.ToHex(c)
            Finally
                _syncing = False
            End Try
            _wheel.Invalidate()
            _classic.Invalidate()
            _harmony.Invalidate()
            _tools.Invalidate()
            Invalidate(New Rectangle(_rightX, 0, _rightW + Pad, Height))
        End Sub

#Region "版面與繪製"

        Protected Overrides Sub OnLayout(ByVal e As LayoutEventArgs)
            MyBase.OnLayout(e)
            ArrangeLayout()
        End Sub

        Private Sub ArrangeLayout()
            If _hex Is Nothing OrElse _sliders(5) Is Nothing Then Return
            Dim w As Integer = ClientSize.Width, h As Integer = ClientSize.Height
            _tabs.SetBounds(Pad, Pad, w - 2 * Pad, TabHeight)

            ' 主區為正方形(邊長由寬度決定、受高度限制),右側一欄滑桿,下方工具列橫跨整個寬度。
            ' 縮小時版面不變(只是隱藏),還原時直接顯示。
            Dim m As Metrics = MetricsFor(_compact)
            Dim top As Integer = Pad + TabHeight + Pad
            Dim byWidth As Integer = w - m.RightW - 3 * Pad
            Dim byHeight As Integer = If(_collapsed, byWidth, h - top - Pad - m.ToolsH - Pad)
            Dim main As Integer = Math.Max(MinMainFor(_compact), Math.Min(byWidth, byHeight))
            For Each page As Control In New Control() {_wheel, _classic, _harmony, _book, _favorites}
                page.SetBounds(Pad, top, main, main)
            Next

            _rightX = Pad + main + Pad
            _rightW = Math.Max(m.RightW - 30, w - _rightX - Pad)
            _currentRect = New Rectangle(_rightX, top, m.BoxW, m.BoxH)
            _historyRect = New Rectangle(_rightX + _rightW - m.BoxW, top, m.BoxW, m.BoxH)
            _rowTop = top + m.BoxH + m.LabelH

            Dim shown As Integer() = VisibleSliders()
            For k As Integer = 0 To shown.Length - 1
                _sliders(shown(k)).SetBounds(_rightX + m.CaptionW, _rowTop + k * m.Row + 3, _rightW - m.CaptionW - m.ValueW, m.Row - 6)
            Next
            UpdateSliderVisibility()
            Dim hexTop As Integer = _rowTop + shown.Length * m.Row + 8
            _hex.SetBounds(_rightX + m.CaptionW, hexTop, _rightW - m.CaptionW, 22)
            ' 精簡版:HEX 左邊是「RGB／HSV」切換鈕(取代 HEX 字樣)
            _modeRect = If(_compact, New Rectangle(_rightX, hexTop, m.CaptionW - 4, 22), Rectangle.Empty)

            ' 工具列在主區下方,橫跨整個寬度。
            _tools.SetBounds(Pad, top + main + Pad, w - 2 * Pad, m.ToolsH)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            MyBase.OnPaint(e)
            If _collapsed Then Return
            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim c As Color = _state.Color

            Dim s As PickerTheme = Scheme
            DrawSwatchBox(g, _currentRect, c, s)
            DrawSwatchBox(g, _historyRect, _original, s)
            Using fg As New SolidBrush(s.Text), sub2 As New SolidBrush(s.SubText),
                  center As New StringFormat() With {.Alignment = StringAlignment.Center},
                  right As New StringFormat() With {.Alignment = StringAlignment.Far, .LineAlignment = StringAlignment.Center},
                  left As New StringFormat() With {.LineAlignment = StringAlignment.Center}
                Dim mid As Integer = _currentRect.Top + _currentRect.Height \ 2
                Using arrow As New Pen(s.Text, 1.5F) With {.EndCap = LineCap.ArrowAnchor}
                    g.DrawLine(arrow, _currentRect.Right + 12, mid, _historyRect.Left - 12, mid)
                End Using
                Dim m As Metrics = MetricsFor(_compact)
                g.DrawString("目前", Font, sub2, New RectangleF(_currentRect.X - 10, _currentRect.Bottom + 1, _currentRect.Width + 20, m.LabelH), center)
                g.DrawString("歷史", Font, sub2, New RectangleF(_historyRect.X - 10, _historyRect.Bottom + 1, _historyRect.Width + 20, m.LabelH), center)

                Dim values() As String = {
                    Math.Round(_state.H).ToString() & "°",
                    Math.Round(_state.S * 100).ToString() & "%",
                    Math.Round(_state.V * 100).ToString() & "%",
                    c.R.ToString(), c.G.ToString(), c.B.ToString()}
                Dim captions As String() = If(_compact, CompactCaptions, SliderCaptions)
                Dim shown As Integer() = VisibleSliders()
                For k As Integer = 0 To shown.Length - 1
                    Dim i As Integer = shown(k)
                    Dim y As Integer = _rowTop + k * m.Row
                    g.DrawString(captions(i), Font, fg, New RectangleF(_rightX, y, m.CaptionW, m.Row), left)
                    g.DrawString(values(i), Font, fg, New RectangleF(_rightX + _rightW - m.ValueW, y, m.ValueW, m.Row), right)
                Next
                Dim hexTop As Integer = _rowTop + shown.Length * m.Row + 8
                If _compact Then
                    ' 切換鈕:顯示「按了會換成」的那一組
                    Using bg As New SolidBrush(s.ButtonBack), edge As New Pen(s.Border), path As GraphicsPath = PickerPaint.RoundRect(_modeRect, 4),
                          mid2 As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center, .FormatFlags = StringFormatFlags.NoWrap}
                        g.FillPath(bg, path)
                        g.DrawPath(edge, path)
                        g.DrawString(If(_rgbSliders, "HSV", "RGB"), Font, fg, _modeRect, mid2)
                    End Using
                Else
                    g.DrawString("HEX", Font, fg, New RectangleF(_rightX, hexTop, m.CaptionW, 22), left)
                End If
            End Using
        End Sub

        Private Shared Sub DrawSwatchBox(ByVal g As Graphics, ByVal r As Rectangle, ByVal c As Color, ByVal s As PickerTheme)
            Using b As New SolidBrush(c), p As GraphicsPath = PickerPaint.RoundRect(r, 6), edge As New Pen(s.Border)
                g.FillPath(b, p)
                g.DrawPath(edge, p)
            End Using
        End Sub

        Protected Overrides Sub OnMouseClick(ByVal e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button = MouseButtons.Left AndAlso _historyRect.Contains(e.Location) Then
                _state.Color = _original
            ElseIf e.Button = MouseButtons.Left AndAlso _modeRect.Contains(e.Location) Then
                _rgbSliders = Not _rgbSliders
                PerformLayout()
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Cursor = If(_historyRect.Contains(e.Location) OrElse _modeRect.Contains(e.Location), Cursors.Hand, Cursors.Default)
        End Sub

#End Region

    End Class

End Namespace
