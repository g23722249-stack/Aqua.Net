<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmKeyImage
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
        Me.components = New System.ComponentModel.Container()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.ContextMenuStrip1 = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.Button3 = New System.Windows.Forms.Button()
        Me.MediaItem1 = New Aqua.MediaItem()
        Me.ImageCheckBox1 = New Aqua.CheckBox()
        Me.MediaViewerControl2 = New Aqua.MediaViewerControl()
        Me.RatingControl1 = New Aqua.RatingControl()
        Me.MediaViewerControl1 = New Aqua.MediaViewerControl()
        Me.SuspendLayout()
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(164, 49)
        Me.Button1.Margin = New System.Windows.Forms.Padding(4)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(125, 64)
        Me.Button1.TabIndex = 1
        Me.Button1.Text = "Button1"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'Button2
        '
        Me.Button2.Location = New System.Drawing.Point(353, 49)
        Me.Button2.Margin = New System.Windows.Forms.Padding(4)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(125, 64)
        Me.Button2.TabIndex = 2
        Me.Button2.Text = "Button2"
        Me.Button2.UseVisualStyleBackColor = True
        '
        'ContextMenuStrip1
        '
        Me.ContextMenuStrip1.Name = "ContextMenuStrip1"
        Me.ContextMenuStrip1.Size = New System.Drawing.Size(61, 4)
        '
        'Button3
        '
        Me.Button3.Location = New System.Drawing.Point(948, 23)
        Me.Button3.Margin = New System.Windows.Forms.Padding(4)
        Me.Button3.Name = "Button3"
        Me.Button3.Size = New System.Drawing.Size(109, 43)
        Me.Button3.TabIndex = 6
        Me.Button3.Text = "Button3"
        Me.Button3.UseVisualStyleBackColor = True
        '
        'MediaItem1
        '
        Me.MediaItem1.BackColor = System.Drawing.Color.White
        Me.MediaItem1.CheckText = "重要病灶"
        Me.MediaItem1.FileName = "C:\Pacs\YM_PIC\220260525893135_2026052514314700717.JPG"
        Me.MediaItem1.Location = New System.Drawing.Point(475, 259)
        Me.MediaItem1.Margin = New System.Windows.Forms.Padding(4)
        Me.MediaItem1.Name = "MediaItem1"
        Me.MediaItem1.Rating = 3
        Me.MediaItem1.Size = New System.Drawing.Size(229, 208)
        Me.MediaItem1.TabIndex = 7
        '
        'ImageCheckBox1
        '
        Me.ImageCheckBox1.BackColor = System.Drawing.Color.Transparent
        Me.ImageCheckBox1.Checked = False
        Me.ImageCheckBox1.ImageCheckDisabled = Nothing
        Me.ImageCheckBox1.ImageChecked = Nothing
        Me.ImageCheckBox1.ImageUnCheckDisabled = Nothing
        Me.ImageCheckBox1.ImageUnChecked = Nothing
        Me.ImageCheckBox1.Location = New System.Drawing.Point(317, 468)
        Me.ImageCheckBox1.Margin = New System.Windows.Forms.Padding(4)
        Me.ImageCheckBox1.Name = "ImageCheckBox1"
        Me.ImageCheckBox1.Size = New System.Drawing.Size(12, 20)
        Me.ImageCheckBox1.TabIndex = 5
        Me.ImageCheckBox1.TextValue = ""
        '
        'MediaViewerControl2
        '
        Me.MediaViewerControl2.AllowDrop = True
        Me.MediaViewerControl2.BackColor = System.Drawing.Color.White
        Me.MediaViewerControl2.FileName = Nothing
        Me.MediaViewerControl2.Location = New System.Drawing.Point(707, 100)
        Me.MediaViewerControl2.Margin = New System.Windows.Forms.Padding(4)
        Me.MediaViewerControl2.Name = "MediaViewerControl2"
        Me.MediaViewerControl2.Size = New System.Drawing.Size(211, 127)
        Me.MediaViewerControl2.TabIndex = 4
        '
        'RatingControl1
        '
        Me.RatingControl1.Location = New System.Drawing.Point(137, 371)
        Me.RatingControl1.Margin = New System.Windows.Forms.Padding(4)
        Me.RatingControl1.Name = "RatingControl1"
        Me.RatingControl1.Rating = 0
        Me.RatingControl1.Size = New System.Drawing.Size(231, 37)
        Me.RatingControl1.TabIndex = 3
        '
        'MediaViewerControl1
        '
        Me.MediaViewerControl1.AllowDrop = True
        Me.MediaViewerControl1.BackColor = System.Drawing.Color.White
        Me.MediaViewerControl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.MediaViewerControl1.ContextMenuStrip = Me.ContextMenuStrip1
        Me.MediaViewerControl1.FileName = Nothing
        Me.MediaViewerControl1.Location = New System.Drawing.Point(117, 148)
        Me.MediaViewerControl1.Margin = New System.Windows.Forms.Padding(4)
        Me.MediaViewerControl1.Name = "MediaViewerControl1"
        Me.MediaViewerControl1.Size = New System.Drawing.Size(266, 202)
        Me.MediaViewerControl1.TabIndex = 0
        '
        'frmKeyImage
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1067, 600)
        Me.Controls.Add(Me.MediaItem1)
        Me.Controls.Add(Me.Button3)
        Me.Controls.Add(Me.ImageCheckBox1)
        Me.Controls.Add(Me.MediaViewerControl2)
        Me.Controls.Add(Me.RatingControl1)
        Me.Controls.Add(Me.Button2)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.MediaViewerControl1)
        Me.Font = New System.Drawing.Font("細明體", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(136, Byte))
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.Name = "frmKeyImage"
        Me.Text = "Form5"
        Me.WindowState = System.Windows.Forms.FormWindowState.Maximized
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents MediaViewerControl1 As Aqua.MediaViewerControl
    Friend WithEvents Button1 As Button
    Friend WithEvents Button2 As Button
    Friend WithEvents RatingControl1 As Aqua.RatingControl
    Friend WithEvents MediaViewerControl2 As Aqua.MediaViewerControl
    Friend WithEvents ImageCheckBox1 As Aqua.CheckBox
    Friend WithEvents ContextMenuStrip1 As ContextMenuStrip
    Friend WithEvents Button3 As Button
    Friend WithEvents MediaItem1 As Aqua.MediaItem
End Class
