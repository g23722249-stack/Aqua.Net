<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmMediaList
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
        Me.Button1 = New System.Windows.Forms.Button()
        Me.MediaList1 = New Aqua.MediaList()
        Me.PrintImage1 = New Aqua.PrintImage()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Button3 = New System.Windows.Forms.Button()
        Me.Button4 = New System.Windows.Forms.Button()
        Me.Button5 = New System.Windows.Forms.Button()
        Me.Button6 = New System.Windows.Forms.Button()
        Me.ScreenPrintImage1 = New Aqua.ScreenPrintImage()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.nudRow = New System.Windows.Forms.NumericUpDown()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.nudMargin = New System.Windows.Forms.NumericUpDown()
        CType(Me.nudRow, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudMargin, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(9, 12)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(97, 33)
        Me.Button1.TabIndex = 1
        Me.Button1.Text = "Add"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'MediaList1
        '
        Me.MediaList1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.MediaList1.ItemSize = New System.Drawing.Size(180, 160)
        Me.MediaList1.Location = New System.Drawing.Point(9, 51)
        Me.MediaList1.Name = "MediaList1"
        Me.MediaList1.SelectedIndex = -1
        Me.MediaList1.SelectedItem = Nothing
        Me.MediaList1.ShowCheckBox = False
        Me.MediaList1.ShowRating = False
        Me.MediaList1.Size = New System.Drawing.Size(473, 666)
        Me.MediaList1.TabIndex = 0
        '
        'PrintImage1
        '
        Me.PrintImage1.PrinterName = "PA4"
        '
        'Button2
        '
        Me.Button2.Location = New System.Drawing.Point(133, 12)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(97, 33)
        Me.Button2.TabIndex = 2
        Me.Button2.Text = "印表機"
        Me.Button2.UseVisualStyleBackColor = True
        '
        'Button3
        '
        Me.Button3.Location = New System.Drawing.Point(258, 12)
        Me.Button3.Name = "Button3"
        Me.Button3.Size = New System.Drawing.Size(97, 33)
        Me.Button3.TabIndex = 3
        Me.Button3.Text = "預覽"
        Me.Button3.UseVisualStyleBackColor = True
        '
        'Button4
        '
        Me.Button4.Location = New System.Drawing.Point(383, 12)
        Me.Button4.Name = "Button4"
        Me.Button4.Size = New System.Drawing.Size(97, 33)
        Me.Button4.TabIndex = 5
        Me.Button4.Text = "自動填入"
        Me.Button4.UseVisualStyleBackColor = True
        '
        'Button5
        '
        Me.Button5.Location = New System.Drawing.Point(508, 12)
        Me.Button5.Name = "Button5"
        Me.Button5.Size = New System.Drawing.Size(97, 33)
        Me.Button5.TabIndex = 6
        Me.Button5.Text = "匯出PDF"
        Me.Button5.UseVisualStyleBackColor = True
        '
        'Button6
        '
        Me.Button6.Location = New System.Drawing.Point(633, 12)
        Me.Button6.Name = "Button6"
        Me.Button6.Size = New System.Drawing.Size(97, 33)
        Me.Button6.TabIndex = 7
        Me.Button6.Text = "匯出圖片"
        Me.Button6.UseVisualStyleBackColor = True
        '
        'ScreenPrintImage1
        '
        Me.ScreenPrintImage1.BackColor = System.Drawing.Color.FromArgb(CType(CType(230, Byte), Integer), CType(CType(230, Byte), Integer), CType(CType(230, Byte), Integer))
        Me.ScreenPrintImage1.Location = New System.Drawing.Point(507, 51)
        Me.ScreenPrintImage1.Name = "ScreenPrintImage1"
        Me.ScreenPrintImage1.PrinterName = "PA4"
        Me.ScreenPrintImage1.RotatePortraitToLandscape = True
        Me.ScreenPrintImage1.Size = New System.Drawing.Size(590, 666)
        Me.ScreenPrintImage1.TabIndex = 4
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(745, 20)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(29, 12)
        Me.Label1.TabIndex = 11
        Me.Label1.Text = "每列"
        '
        'nudRow
        '
        Me.nudRow.Location = New System.Drawing.Point(785, 16)
        Me.nudRow.Maximum = New Decimal(New Integer() {6, 0, 0, 0})
        Me.nudRow.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.nudRow.Name = "nudRow"
        Me.nudRow.Size = New System.Drawing.Size(50, 22)
        Me.nudRow.TabIndex = 8
        Me.nudRow.Value = New Decimal(New Integer() {2, 0, 0, 0})
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(850, 20)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(47, 12)
        Me.Label2.TabIndex = 10
        Me.Label2.Text = "邊界mm"
        '
        'nudMargin
        '
        Me.nudMargin.Location = New System.Drawing.Point(910, 16)
        Me.nudMargin.Maximum = New Decimal(New Integer() {30, 0, 0, 0})
        Me.nudMargin.Name = "nudMargin"
        Me.nudMargin.Size = New System.Drawing.Size(50, 22)
        Me.nudMargin.TabIndex = 9
        Me.nudMargin.Value = New Decimal(New Integer() {5, 0, 0, 0})
        '
        'frmMediaList
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1251, 731)
        Me.Controls.Add(Me.nudMargin)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.nudRow)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.ScreenPrintImage1)
        Me.Controls.Add(Me.Button6)
        Me.Controls.Add(Me.Button5)
        Me.Controls.Add(Me.Button4)
        Me.Controls.Add(Me.Button3)
        Me.Controls.Add(Me.Button2)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.MediaList1)
        Me.Name = "frmMediaList"
        Me.Text = "frmMediaList"
        CType(Me.nudRow, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudMargin, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents MediaList1 As Aqua.MediaList
    Friend WithEvents Button1 As Button
    Friend WithEvents PrintImage1 As Aqua.PrintImage
    Friend WithEvents Button2 As Button
    Friend WithEvents Button3 As Button
    Friend WithEvents Button4 As Button
    Friend WithEvents Button5 As Button
    Friend WithEvents Button6 As Button
    Friend WithEvents Label1 As Label
    Friend WithEvents nudRow As NumericUpDown
    Friend WithEvents Label2 As Label
    Friend WithEvents nudMargin As NumericUpDown
    Friend WithEvents ScreenPrintImage1 As Aqua.ScreenPrintImage
End Class
