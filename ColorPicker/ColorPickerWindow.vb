Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 浮動的選色視窗(工具視窗,畫圖時可以一直開著):改顏色立即觸發 ColorChanged,沒有確定 / 取消。
    ''' Targets 不是空的時,上方有一排目標切換(例如「線條、填色、文字」),宿主依 SelectedTarget 決定要改哪個顏色。
    ''' 頁籤列右端的按鈕把視窗縮小到只剩頁籤,再按一次或點任何頁籤就還原;關閉只是隱藏。
    ''' 位置由宿主記住(LocationChanged),本視窗不存檔。
    ''' </summary>
    <DesignerCategory("Code")>
    Public Class ColorPickerWindow
        Inherits AquaForm
        Implements IThemeHost

        Private Const TitleBarHeight As Integer = 23
        Private Const Pad As Integer = 6
        Private Const TargetHeight As Integer = 22
        ''' <summary>預設寬度(100% 縮放):精簡版選色器的色環約 170 像素見方。</summary>
        Private Const DefaultWidth As Integer = 340

        Private ReadOnly _picker As New ColorPicker()
        Private ReadOnly _targets As New SegmentBar()
        Private _silent As Boolean

        ''' <summary>使用者改了顏色(拖曳時連續觸發);用 Color 屬性設定時不觸發。</summary>
        Public Event ColorChanged As EventHandler
        ''' <summary>使用者切換了目標(線條 / 填色 / 文字…)。</summary>
        Public Event TargetChanged As EventHandler

        Public Sub New()
            Text = "顏色"
            WindowBorderStyle = Aqua.FormBorderStyle.Fixed
            MinButton = False
            MaxButton = False
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            KeyPreview = True
            Font = New Font("Microsoft JhengHei UI", 9.0F)

            _picker.ShowCollapseButton = True
            _picker.Compact = True ' 浮動在畫布上：精簡版，少擋一點畫面
            AddHandler _picker.ColorChanged, Sub(s, e) If Not _silent Then RaiseEvent ColorChanged(Me, EventArgs.Empty)
            AddHandler _picker.CollapsedChanged, Sub(s, e) FitHeight()
            AddHandler _picker.ThemeChanged, Sub(s, e) ApplyTheme()
            _targets.Visible = False
            AddHandler _targets.SelectedIndexChanged, Sub(s, e) RaiseEvent TargetChanged(Me, EventArgs.Empty)
            Controls.Add(_targets)
            Controls.Add(_picker)
            ApplyTheme()
            ClientSize = New Size(DefaultWidth, 200)
            FitHeight()
        End Sub

        Private ReadOnly Property Scheme As PickerTheme Implements IThemeHost.Scheme
            Get
                Return PickerTheme.For(_picker.Theme)
            End Get
        End Property

        Private Sub ApplyTheme()
            BackColor = Scheme.Back
            Invalidate(True)
        End Sub

        ''' <summary>目標名稱(例如「線條、填色、文字」);空的或 Nothing 時不顯示目標列。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Targets As String()
            Get
                Return _targets.Items
            End Get
            Set(ByVal value As String())
                _targets.Items = value
                _targets.Visible = value IsNot Nothing AndAlso value.Length > 0
                FitHeight()
            End Set
        End Property

        ''' <summary>目前的目標;程式設定也會觸發 TargetChanged。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property SelectedTarget As Integer
            Get
                Return _targets.SelectedIndex
            End Get
            Set(ByVal value As Integer)
                _targets.SelectedIndex = value
            End Set
        End Property

        ''' <summary>目前顏色;設定時「歷史」也改成這個顏色(點歷史可還原),而且不觸發 ColorChanged。</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Color As Color
            Get
                Return _picker.SelectedColor
            End Get
            Set(ByVal value As Color)
                _silent = True
                Try
                    _picker.OriginalColor = value
                    _picker.SelectedColor = value
                Finally
                    _silent = False
                End Try
            End Set
        End Property

        Public ReadOnly Property Picker As ColorPicker
            Get
                Return _picker
            End Get
        End Property

        Private _showInactive As Boolean

        ''' <summary>顯示但不搶焦點(宿主切換分頁時自動打開,鍵盤仍留在主視窗)。</summary>
        Public Sub ShowInactive(ByVal owner As IWin32Window)
            _showInactive = True
            Try
                Show(owner)
            Finally
                _showInactive = False
            End Try
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return _showInactive OrElse MyBase.ShowWithoutActivation
            End Get
        End Property

        Public Sub AddRecentColor(ByVal c As Color)
            _picker.AddRecentColor(c)
        End Sub

        ''' <summary>高 DPI 時 WinForms 在顯示前會依字型放大視窗:放大之後再依寬度重算高度,縮小 / 還原才會一致。</summary>
        Protected Overrides Sub OnLoad(ByVal e As EventArgs)
            MyBase.OnLoad(e)
            FitHeight()
        End Sub

        Protected Overrides Sub OnResize(ByVal e As EventArgs)
            MyBase.OnResize(e)
            LayoutBody()
        End Sub

        Private Function BodyTop() As Integer
            Return TitleBarHeight + If(_targets IsNot Nothing AndAlso _targets.Visible, Pad + TargetHeight, 0)
        End Function

        ''' <summary>外框粗細:內容往內縮這麼多,外框才不會被選色器蓋住。</summary>
        Private Const Edge As Integer = 2

        Private Sub LayoutBody()
            If _picker Is Nothing Then Return ' AquaForm 的建構式會先觸發 OnResize,那時欄位還沒建立
            Dim w As Integer = ClientSize.Width
            _targets.SetBounds(Pad, TitleBarHeight + Pad, w - 2 * Pad, TargetHeight)
            _picker.SetBounds(Edge, BodyTop(), w - 2 * Edge, _picker.PreferredHeight)
            Invalidate()
        End Sub

        ''' <summary>
        ''' 浮動視窗浮在畫布上:深色時視窗底色和畫布很接近,畫一圈明顯的外框(深色用亮灰、淺色用深灰)。
        ''' </summary>
        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim c As Color = If(Scheme.IsDark, Color.FromArgb(150, 160, 178), Color.FromArgb(130, 136, 150))
            Using p As New Pen(c, Edge)
                ' 筆在矩形線上置中:往內退半個筆寬,整條框才都在視窗裡
                Dim h As Single = Edge / 2.0F
                e.Graphics.DrawRectangle(p, h, h, ClientSize.Width - Edge, ClientSize.Height - Edge)
            End Using
        End Sub

        ''' <summary>依目標列與選色器(展開 / 縮小)調整視窗高度,寬度不變。</summary>
        Private Sub FitHeight()
            If _picker Is Nothing Then Return
            Dim w As Integer = If(ClientSize.Width > 0 AndAlso _picker.Width > 0, ClientSize.Width, DefaultWidth)
            ClientSize = New Size(w, BodyTop() + If(_picker.Collapsed, ColorPicker.CollapsedHeight, ColorPicker.PreferredHeightFor(w - 2 * Edge, True)) + Edge)
            LayoutBody()
        End Sub

        ''' <summary>關閉只是隱藏:顏色、頁籤、縮小狀態都留著,下次打開還在。</summary>
        Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
            If e.CloseReason = CloseReason.UserClosing Then
                e.Cancel = True
                Hide()
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub

        Protected Overrides Sub OnKeyDown(ByVal e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Escape Then Hide()
        End Sub

    End Class

End Namespace
