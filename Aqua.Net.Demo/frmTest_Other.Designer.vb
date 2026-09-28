<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmTest_Other
    Inherits System.Windows.Forms.Form

    'Form 覆寫 Dispose 以清除元件清單。
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    '為 Windows Form 設計工具的必要項
    Private components As System.ComponentModel.IContainer

    '注意: 以下為 Windows Form 設計工具所需的程序
    '可以使用 Windows Form 設計工具進行修改。
    '請勿使用程式碼編輯器進行修改。
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmTest_Other))
        Dim ButtonItem1 As Aqua.Buttons.ButtonItem = New Aqua.Buttons.ButtonItem()
        Dim ButtonItem2 As Aqua.Buttons.ButtonItem = New Aqua.Buttons.ButtonItem()
        Dim ButtonItem3 As Aqua.Buttons.ButtonItem = New Aqua.Buttons.ButtonItem()
        Me.Panel1 = New Aqua.Panel()
        Me.Slider1 = New Aqua.Slider()
        Me.ProgressBar1 = New Aqua.ProgressBar()
        Me.Loading1 = New Aqua.Loading()
        Me.TimeLine1 = New Aqua.TimeLine()
        Me.FileListBox1 = New Aqua.FileListBox()
        Me.DirListBox1 = New Aqua.DirListBox()
        Me.DriveListBox1 = New Aqua.DriveListBox()
        Me.Button1 = New Aqua.Button()
        Me.ImageRadioButton1 = New Aqua.RadioButton()
        Me.ImageCheckBox1 = New Aqua.CheckBox()
        Me.ImageButton1 = New Aqua.Button()
        Me.Button2 = New Aqua.Button()
        Me.Buttons1 = New Aqua.Buttons()
        Me.FlashButton1 = New Aqua.FlashButton()
        Me.Panel1.SuspendLayout()
        Me.SuspendLayout()
        '
        'Panel1
        '
        Me.Panel1.BackColor = System.Drawing.Color.White
        Me.Panel1.BorderColor = System.Drawing.Color.FromArgb(CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer))
        Me.Panel1.BorderFocusColor = System.Drawing.Color.FromArgb(CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer))
        Me.Panel1.Controls.Add(Me.Slider1)
        Me.Panel1.Image = Nothing
        Me.Panel1.Location = New System.Drawing.Point(128, 146)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(210, 150)
        Me.Panel1.SoundFileOfClick = Nothing
        Me.Panel1.SoundFileOfEnterFocus = Nothing
        Me.Panel1.SoundFileOfExitFocus = Nothing
        Me.Panel1.SoundFileOfMouseEnter = Nothing
        Me.Panel1.SoundFileOfMouseHover = Nothing
        Me.Panel1.SoundFileOfMouseLeave = Nothing
        Me.Panel1.TabIndex = 20
        '
        'Slider1
        '
        Me.Slider1.Location = New System.Drawing.Point(16, 18)
        Me.Slider1.Name = "Slider1"
        Me.Slider1.Size = New System.Drawing.Size(175, 51)
        Me.Slider1.SoundFileOfClick = Nothing
        Me.Slider1.SoundFileOfEnterFocus = Nothing
        Me.Slider1.SoundFileOfExitFocus = Nothing
        Me.Slider1.TabIndex = 16
        Me.Slider1.Text = "Slider1"
        '
        'ProgressBar1
        '
        Me.ProgressBar1.Location = New System.Drawing.Point(579, 435)
        Me.ProgressBar1.Name = "ProgressBar1"
        Me.ProgressBar1.Size = New System.Drawing.Size(234, 24)
        Me.ProgressBar1.TabIndex = 19
        Me.ProgressBar1.TabStop = False
        Me.ProgressBar1.Text = "ProgressBar1"
        Me.ProgressBar1.Value = 3
        '
        'Loading1
        '
        Me.Loading1.Location = New System.Drawing.Point(544, 358)
        Me.Loading1.Name = "Loading1"
        Me.Loading1.Play = False
        Me.Loading1.Size = New System.Drawing.Size(330, 16)
        Me.Loading1.TabIndex = 18
        Me.Loading1.TabStop = False
        Me.Loading1.Text = "Loading1"
        '
        'TimeLine1
        '
        Me.TimeLine1.Location = New System.Drawing.Point(369, 428)
        Me.TimeLine1.Name = "TimeLine1"
        Me.TimeLine1.Size = New System.Drawing.Size(151, 31)
        Me.TimeLine1.TabIndex = 17
        Me.TimeLine1.TabStop = False
        Me.TimeLine1.Text = "TimeLine1"
        '
        'FileListBox1
        '
        Me.FileListBox1.BackColor = System.Drawing.SystemColors.Window
        Me.FileListBox1.BorderColor = System.Drawing.Color.FromArgb(CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer))
        Me.FileListBox1.BorderFocusColor = System.Drawing.Color.FromArgb(CType(CType(159, Byte), Integer), CType(CType(182, Byte), Integer), CType(CType(244, Byte), Integer))
        Me.FileListBox1.Location = New System.Drawing.Point(515, 221)
        Me.FileListBox1.Name = "FileListBox1"
        Me.FileListBox1.Path = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE"
        Me.FileListBox1.Size = New System.Drawing.Size(373, 133)
        Me.FileListBox1.TabIndex = 15
        Me.FileListBox1.TabStop = False
        '
        'DirListBox1
        '
        Me.DirListBox1.BackColor = System.Drawing.SystemColors.Window
        Me.DirListBox1.BorderColor = System.Drawing.Color.FromArgb(CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer))
        Me.DirListBox1.BorderFocusColor = System.Drawing.Color.FromArgb(CType(CType(159, Byte), Integer), CType(CType(182, Byte), Integer), CType(CType(244, Byte), Integer))
        Me.DirListBox1.Location = New System.Drawing.Point(684, 53)
        Me.DirListBox1.Name = "DirListBox1"
        Me.DirListBox1.Root = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE"
        Me.DirListBox1.Size = New System.Drawing.Size(208, 162)
        Me.DirListBox1.TabIndex = 14
        Me.DirListBox1.TabStop = False
        '
        'DriveListBox1
        '
        Me.DriveListBox1.BackColor = System.Drawing.SystemColors.Window
        Me.DriveListBox1.BorderColor = System.Drawing.Color.FromArgb(CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer), CType(CType(189, Byte), Integer))
        Me.DriveListBox1.BorderFocusColor = System.Drawing.Color.FromArgb(CType(CType(159, Byte), Integer), CType(CType(182, Byte), Integer), CType(CType(244, Byte), Integer))
        Me.DriveListBox1.Font = New System.Drawing.Font("新細明體", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(136, Byte))
        Me.DriveListBox1.Location = New System.Drawing.Point(515, 54)
        Me.DriveListBox1.Name = "DriveListBox1"
        Me.DriveListBox1.Size = New System.Drawing.Size(162, 162)
        Me.DriveListBox1.TabIndex = 13
        Me.DriveListBox1.TabStop = False
        '
        'Button1
        '
        Me.Button1.BackColor = System.Drawing.Color.Transparent
        Me.Button1.ImageDisabled = CType(resources.GetObject("Button1.ImageDisabled"), System.Drawing.Image)
        Me.Button1.ImageHover = CType(resources.GetObject("Button1.ImageHover"), System.Drawing.Image)
        Me.Button1.ImageNormal = CType(resources.GetObject("Button1.ImageNormal"), System.Drawing.Image)
        Me.Button1.ImagePressed = CType(resources.GetObject("Button1.ImagePressed"), System.Drawing.Image)
        Me.Button1.Location = New System.Drawing.Point(203, 25)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(144, 34)
        Me.Button1.TabIndex = 12
        Me.Button1.Text = "Button1"
        Me.Button1.TintBrightness = 100
        Me.Button1.TintHue = 0
        Me.Button1.TintSaturation = 87
        '
        'ImageRadioButton1
        '
        Me.ImageRadioButton1.BackColor = System.Drawing.Color.Transparent
        Me.ImageRadioButton1.Checked = False
        Me.ImageRadioButton1.ImageCheckDisabled = CType(resources.GetObject("ImageRadioButton1.ImageCheckDisabled"), System.Drawing.Image)
        Me.ImageRadioButton1.ImageChecked = CType(resources.GetObject("ImageRadioButton1.ImageChecked"), System.Drawing.Image)
        Me.ImageRadioButton1.ImageUnCheckDisabled = CType(resources.GetObject("ImageRadioButton1.ImageUnCheckDisabled"), System.Drawing.Image)
        Me.ImageRadioButton1.ImageUnChecked = CType(resources.GetObject("ImageRadioButton1.ImageUnChecked"), System.Drawing.Image)
        Me.ImageRadioButton1.Location = New System.Drawing.Point(40, 112)
        Me.ImageRadioButton1.Name = "ImageRadioButton1"
        Me.ImageRadioButton1.Size = New System.Drawing.Size(52, 16)
        Me.ImageRadioButton1.TabIndex = 11
        Me.ImageRadioButton1.TextValue = "Test"
        '
        'ImageCheckBox1
        '
        Me.ImageCheckBox1.BackColor = System.Drawing.Color.Transparent
        Me.ImageCheckBox1.Checked = False
        Me.ImageCheckBox1.ImageCheckDisabled = CType(resources.GetObject("ImageCheckBox1.ImageCheckDisabled"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageChecked = CType(resources.GetObject("ImageCheckBox1.ImageChecked"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageUnCheckDisabled = CType(resources.GetObject("ImageCheckBox1.ImageUnCheckDisabled"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageUnChecked = CType(resources.GetObject("ImageCheckBox1.ImageUnChecked"), System.Drawing.Image)
        Me.ImageCheckBox1.Location = New System.Drawing.Point(40, 81)
        Me.ImageCheckBox1.Name = "ImageCheckBox1"
        Me.ImageCheckBox1.Size = New System.Drawing.Size(52, 16)
        Me.ImageCheckBox1.TabIndex = 10
        Me.ImageCheckBox1.TextValue = "Test"
        '
        'ImageButton1
        '
        Me.ImageButton1.BackColor = System.Drawing.Color.Transparent
        Me.ImageButton1.ImageDisabled = CType(resources.GetObject("ImageButton1.ImageDisabled"), System.Drawing.Image)
        Me.ImageButton1.ImageHover = CType(resources.GetObject("ImageButton1.ImageHover"), System.Drawing.Image)
        Me.ImageButton1.ImageNormal = CType(resources.GetObject("ImageButton1.ImageNormal"), System.Drawing.Image)
        Me.ImageButton1.ImagePressed = CType(resources.GetObject("ImageButton1.ImagePressed"), System.Drawing.Image)
        Me.ImageButton1.Location = New System.Drawing.Point(29, 25)
        Me.ImageButton1.Name = "ImageButton1"
        Me.ImageButton1.Size = New System.Drawing.Size(144, 34)
        Me.ImageButton1.TabIndex = 9
        Me.ImageButton1.Text = "ImageButton1"
        '
        'Button2
        '
        Me.Button2.BackColor = System.Drawing.Color.Transparent
        Me.Button2.ImageDisabled = CType(resources.GetObject("Button2.ImageDisabled"), System.Drawing.Image)
        Me.Button2.ImageHover = CType(resources.GetObject("Button2.ImageHover"), System.Drawing.Image)
        Me.Button2.ImageNormal = CType(resources.GetObject("Button2.ImageNormal"), System.Drawing.Image)
        Me.Button2.ImagePressed = CType(resources.GetObject("Button2.ImagePressed"), System.Drawing.Image)
        Me.Button2.Location = New System.Drawing.Point(292, 117)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(93, 20)
        Me.Button2.TabIndex = 21
        Me.Button2.Text = "Button2"
        '
        'Buttons1
        '
        ButtonItem1.Icon = Nothing
        ButtonItem1.Text = "123"
        ButtonItem2.Icon = Nothing
        ButtonItem2.Text = "222"
        ButtonItem3.Icon = Nothing
        ButtonItem3.Text = "333"
        Me.Buttons1.Items.Add(ButtonItem1)
        Me.Buttons1.Items.Add(ButtonItem2)
        Me.Buttons1.Items.Add(ButtonItem3)
        Me.Buttons1.Location = New System.Drawing.Point(318, 348)
        Me.Buttons1.Name = "Buttons1"
        Me.Buttons1.Size = New System.Drawing.Size(161, 26)
        Me.Buttons1.SoundFileOfClick = Nothing
        Me.Buttons1.SoundFileOfEnterFocus = Nothing
        Me.Buttons1.SoundFileOfExitFocus = Nothing
        Me.Buttons1.SoundFileOfMouseEnter = Nothing
        Me.Buttons1.SoundFileOfMouseHover = Nothing
        Me.Buttons1.SoundFileOfMouseLeave = Nothing
        Me.Buttons1.TabIndex = 22
        Me.Buttons1.Text = "Buttons1"
        '
        'FlashButton1
        '
        Me.FlashButton1.Location = New System.Drawing.Point(346, 315)
        Me.FlashButton1.Name = "FlashButton1"
        Me.FlashButton1.Size = New System.Drawing.Size(133, 27)
        Me.FlashButton1.SoundFileOfClick = Nothing
        Me.FlashButton1.SoundFileOfEnterFocus = Nothing
        Me.FlashButton1.SoundFileOfExitFocus = Nothing
        Me.FlashButton1.SoundFileOfMouseEnter = Nothing
        Me.FlashButton1.SoundFileOfMouseHover = Nothing
        Me.FlashButton1.SoundFileOfMouseLeave = Nothing
        Me.FlashButton1.TabIndex = 23
        Me.FlashButton1.Text = "FlashButton1"
        '
        'frmTest_Other
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.SystemColors.ActiveBorder
        Me.ClientSize = New System.Drawing.Size(971, 540)
        Me.Controls.Add(Me.FlashButton1)
        Me.Controls.Add(Me.Buttons1)
        Me.Controls.Add(Me.Button2)
        Me.Controls.Add(Me.Panel1)
        Me.Controls.Add(Me.ProgressBar1)
        Me.Controls.Add(Me.Loading1)
        Me.Controls.Add(Me.TimeLine1)
        Me.Controls.Add(Me.FileListBox1)
        Me.Controls.Add(Me.DirListBox1)
        Me.Controls.Add(Me.DriveListBox1)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.ImageRadioButton1)
        Me.Controls.Add(Me.ImageCheckBox1)
        Me.Controls.Add(Me.ImageButton1)
        Me.Name = "frmTest_Other"
        Me.Text = "frmTest_Other"
        Me.Panel1.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents ImageRadioButton1 As Aqua.RadioButton
    Friend WithEvents ImageCheckBox1 As Aqua.CheckBox
    Friend WithEvents ImageButton1 As Aqua.Button
    Friend WithEvents Button1 As Aqua.Button
    Friend WithEvents DriveListBox1 As Aqua.DriveListBox
    Friend WithEvents DirListBox1 As Aqua.DirListBox
    Friend WithEvents FileListBox1 As Aqua.FileListBox
    Friend WithEvents Slider1 As Aqua.Slider
    Friend WithEvents TimeLine1 As Aqua.TimeLine
    Friend WithEvents Loading1 As Aqua.Loading
    Friend WithEvents ProgressBar1 As Aqua.ProgressBar
    Friend WithEvents Panel1 As Aqua.Panel
    Friend WithEvents Button2 As Aqua.Button
    Friend WithEvents Buttons1 As Aqua.Buttons
    Friend WithEvents FlashButton1 As Aqua.FlashButton
End Class
