Option Strict Off
Namespace Global.Aqua

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class MediaItem
    Inherits System.Windows.Forms.UserControl

    'UserControl 覆寫 Dispose 以清除元件清單。
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(MediaItem))
        Me.pnlBottom = New System.Windows.Forms.Panel()
        Me.MediaViewerControl1 = New Aqua.MediaViewerControl()
        Me.RatingControl1 = New Aqua.RatingControl()
        Me.ImageCheckBox1 = New Aqua.CheckBox()
        Me.pnlBottom.SuspendLayout()
        Me.SuspendLayout()
        '
        'pnlBottom
        '
        Me.pnlBottom.Controls.Add(Me.RatingControl1)
        Me.pnlBottom.Controls.Add(Me.ImageCheckBox1)
        Me.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.pnlBottom.Location = New System.Drawing.Point(0, 266)
        Me.pnlBottom.Name = "pnlBottom"
        Me.pnlBottom.Padding = New System.Windows.Forms.Padding(5, 0, 0, 0)
        Me.pnlBottom.Size = New System.Drawing.Size(351, 27)
        Me.pnlBottom.TabIndex = 2
        '
        'MediaViewerControl1
        '
        Me.MediaViewerControl1.AllowDrop = True
        Me.MediaViewerControl1.BackColor = System.Drawing.Color.White
        Me.MediaViewerControl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.MediaViewerControl1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.MediaViewerControl1.FileName = Nothing
        Me.MediaViewerControl1.Location = New System.Drawing.Point(0, 0)
        Me.MediaViewerControl1.Name = "MediaViewerControl1"
        Me.MediaViewerControl1.Size = New System.Drawing.Size(351, 266)
        Me.MediaViewerControl1.TabIndex = 1
        Me.MediaViewerControl1.TabStop = False
        '
        'RatingControl1
        '
        Me.RatingControl1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.RatingControl1.Location = New System.Drawing.Point(33, 0)
        Me.RatingControl1.Name = "RatingControl1"
        Me.RatingControl1.Rating = 0
        Me.RatingControl1.Size = New System.Drawing.Size(318, 27)
        Me.RatingControl1.TabIndex = 3
        Me.RatingControl1.TabStop = False
        '
        'ImageCheckBox1
        '
        Me.ImageCheckBox1.BackColor = System.Drawing.Color.Transparent
        Me.ImageCheckBox1.Checked = False
        Me.ImageCheckBox1.Dock = System.Windows.Forms.DockStyle.Left
        Me.ImageCheckBox1.ImageCheckDisabled = CType(resources.GetObject("ImageCheckBox1.ImageCheckDisabled"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageChecked = CType(resources.GetObject("ImageCheckBox1.ImageChecked"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageUnCheckDisabled = CType(resources.GetObject("ImageCheckBox1.ImageUnCheckDisabled"), System.Drawing.Image)
        Me.ImageCheckBox1.ImageUnChecked = CType(resources.GetObject("ImageCheckBox1.ImageUnChecked"), System.Drawing.Image)
        Me.ImageCheckBox1.Location = New System.Drawing.Point(5, 0)
        Me.ImageCheckBox1.Margin = New System.Windows.Forms.Padding(0, 3, 3, 3)
        Me.ImageCheckBox1.Name = "ImageCheckBox1"
        Me.ImageCheckBox1.Padding = New System.Windows.Forms.Padding(4, 0, 0, 0)
        Me.ImageCheckBox1.Size = New System.Drawing.Size(28, 27)
        Me.ImageCheckBox1.TabIndex = 0
        Me.ImageCheckBox1.TabStop = False
        Me.ImageCheckBox1.TextValue = ""
        '
        'MediaItem
        '
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.BackColor = System.Drawing.Color.White
        Me.Controls.Add(Me.MediaViewerControl1)
        Me.Controls.Add(Me.pnlBottom)
        Me.Name = "MediaItem"
        Me.Size = New System.Drawing.Size(351, 293)
        Me.pnlBottom.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents ImageCheckBox1 As CheckBox
    Friend WithEvents MediaViewerControl1 As MediaViewerControl
    Friend WithEvents pnlBottom As System.Windows.Forms.Panel
    Friend WithEvents RatingControl1 As RatingControl
End Class

End Namespace
