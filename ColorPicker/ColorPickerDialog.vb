Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 包住 ColorPicker 的確定 / 取消對話框。開啟時「歷史」= 傳入的 Color;
    ''' 按確定才把結果加進最近使用色(存於 文件\PainterTool,跨程式共用)。
    ''' 標題列配色隨 Theme 變化(Win10 1809+ 深色標題列;Win11 另指定底色與文字色)。
    ''' </summary>
    <System.ComponentModel.DesignerCategory("Code")>   ' 版面為手寫,按兩下直接開程式碼
    Public Class ColorPickerDialog
        Inherits Form
        Implements IThemeHost

        Private ReadOnly _picker As New ColorPicker()
        Private ReadOnly _ok As New System.Windows.Forms.Button()
        Private ReadOnly _cancel As New System.Windows.Forms.Button()

        Public Sub New()
            BuildLayout()
        End Sub

        Private Sub BuildLayout()
            Text = "顏色"
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            StartPosition = FormStartPosition.CenterParent
            ClientSize = New Size(_picker.Width, _picker.Height + 44)

            _picker.Dock = DockStyle.Top
            AddHandler _picker.ThemeChanged, Sub(s, e) ApplyTheme()
            Controls.Add(_picker)

            _ok.Text = "確定"
            _ok.DialogResult = DialogResult.OK
            _cancel.Text = "取消"
            _cancel.DialogResult = DialogResult.Cancel
            For Each b As System.Windows.Forms.Button In New System.Windows.Forms.Button() {_ok, _cancel}
                b.Size = New Size(84, 28)
                b.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
                Controls.Add(b)
            Next
            _cancel.Location = New Point(ClientSize.Width - 8 - _cancel.Width, ClientSize.Height - 8 - _cancel.Height)
            _ok.Location = New Point(_cancel.Left - 8 - _ok.Width, _cancel.Top)
            AcceptButton = _ok
            CancelButton = _cancel
            ApplyTheme()
        End Sub

        ''' <summary>配色:Dark 深色(預設)、Light 淺色;與 Picker.Theme 同步。</summary>
        Public Property Theme As ColorPickerTheme
            Get
                Return _picker.Theme
            End Get
            Set(ByVal value As ColorPickerTheme)
                _picker.Theme = value   ' 透過 ThemeChanged 回呼 ApplyTheme
            End Set
        End Property

        Private ReadOnly Property Scheme As PickerTheme Implements IThemeHost.Scheme
            Get
                Return PickerTheme.For(_picker.Theme)
            End Get
        End Property

        Private Sub ApplyTheme()
            Dim s As PickerTheme = Scheme
            BackColor = s.Back
            s.StyleButton(_ok)
            s.StyleButton(_cancel)
            TitleBarTheme.Apply(Me, s)
        End Sub

        Protected Overrides Sub OnHandleCreated(ByVal e As EventArgs)
            MyBase.OnHandleCreated(e)
            TitleBarTheme.Apply(Me, Scheme)
        End Sub

        ''' <summary>開啟前設定為原始色;關閉後為選取結果。</summary>
        Public Property Color As Color
            Get
                Return _picker.SelectedColor
            End Get
            Set(ByVal value As Color)
                _picker.SelectedColor = value
                _picker.OriginalColor = value
            End Set
        End Property

        Public ReadOnly Property Picker As ColorPicker
            Get
                Return _picker
            End Get
        End Property

        Protected Overrides Sub OnFormClosed(ByVal e As FormClosedEventArgs)
            If DialogResult = DialogResult.OK Then
                _picker.AddRecentColor(_picker.SelectedColor)
            End If
            MyBase.OnFormClosed(e)
        End Sub
    End Class

End Namespace
