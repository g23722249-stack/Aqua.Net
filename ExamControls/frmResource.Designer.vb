Option Strict Off
Namespace Global.Aqua

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmResource
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmResource))
        Me.picButtonImageNormal = New System.Windows.Forms.PictureBox()
        Me.picButtonImageHover = New System.Windows.Forms.PictureBox()
        Me.picButtonImagePressed = New System.Windows.Forms.PictureBox()
        Me.picButtonImageDisabled = New System.Windows.Forms.PictureBox()
        Me.picCheckImageDisabled = New System.Windows.Forms.PictureBox()
        Me.picCheckImageUnChecked = New System.Windows.Forms.PictureBox()
        Me.picCheckImageUnCheckDisabled = New System.Windows.Forms.PictureBox()
        Me.picCheckImageChecked = New System.Windows.Forms.PictureBox()
        Me.picRadioImageDisabled = New System.Windows.Forms.PictureBox()
        Me.picRadioImageUnChecked = New System.Windows.Forms.PictureBox()
        Me.picRadioImageUnCheckDisabled = New System.Windows.Forms.PictureBox()
        Me.picRadioImageChecked = New System.Windows.Forms.PictureBox()
            CType(Me.picButtonImageNormal, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picButtonImageHover, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picButtonImagePressed, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picButtonImageDisabled, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picCheckImageDisabled, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picCheckImageUnChecked, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picCheckImageUnCheckDisabled, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picCheckImageChecked, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picRadioImageDisabled, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picRadioImageUnChecked, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picRadioImageUnCheckDisabled, System.ComponentModel.ISupportInitialize).BeginInit()
            CType(Me.picRadioImageChecked, System.ComponentModel.ISupportInitialize).BeginInit()
            Me.SuspendLayout()
            '
            'picButtonImageNormal
            '
            Me.picButtonImageNormal.Image = CType(resources.GetObject("picButtonImageNormal.Image"), System.Drawing.Image)
            Me.picButtonImageNormal.Location = New System.Drawing.Point(12, 21)
            Me.picButtonImageNormal.Name = "picButtonImageNormal"
            Me.picButtonImageNormal.Size = New System.Drawing.Size(148, 49)
            Me.picButtonImageNormal.TabIndex = 0
            Me.picButtonImageNormal.TabStop = False
            '
            'picButtonImageHover
            '
            Me.picButtonImageHover.Image = CType(resources.GetObject("picButtonImageHover.Image"), System.Drawing.Image)
            Me.picButtonImageHover.Location = New System.Drawing.Point(196, 21)
            Me.picButtonImageHover.Name = "picButtonImageHover"
            Me.picButtonImageHover.Size = New System.Drawing.Size(148, 49)
            Me.picButtonImageHover.TabIndex = 1
            Me.picButtonImageHover.TabStop = False
            '
            'picButtonImagePressed
            '
            Me.picButtonImagePressed.Image = CType(resources.GetObject("picButtonImagePressed.Image"), System.Drawing.Image)
            Me.picButtonImagePressed.Location = New System.Drawing.Point(372, 21)
            Me.picButtonImagePressed.Name = "picButtonImagePressed"
            Me.picButtonImagePressed.Size = New System.Drawing.Size(148, 49)
            Me.picButtonImagePressed.TabIndex = 2
            Me.picButtonImagePressed.TabStop = False
            '
            'picButtonImageDisabled
            '
            Me.picButtonImageDisabled.Image = CType(resources.GetObject("picButtonImageDisabled.Image"), System.Drawing.Image)
            Me.picButtonImageDisabled.Location = New System.Drawing.Point(550, 21)
            Me.picButtonImageDisabled.Name = "picButtonImageDisabled"
            Me.picButtonImageDisabled.Size = New System.Drawing.Size(148, 49)
            Me.picButtonImageDisabled.TabIndex = 3
            Me.picButtonImageDisabled.TabStop = False
            '
            'picCheckImageDisabled
            '
            Me.picCheckImageDisabled.Image = CType(resources.GetObject("picCheckImageDisabled.Image"), System.Drawing.Image)
            Me.picCheckImageDisabled.Location = New System.Drawing.Point(550, 121)
            Me.picCheckImageDisabled.Name = "picCheckImageDisabled"
            Me.picCheckImageDisabled.Size = New System.Drawing.Size(148, 49)
            Me.picCheckImageDisabled.TabIndex = 7
            Me.picCheckImageDisabled.TabStop = False
            '
            'picCheckImageUnChecked
            '
            Me.picCheckImageUnChecked.Image = CType(resources.GetObject("picCheckImageUnChecked.Image"), System.Drawing.Image)
            Me.picCheckImageUnChecked.Location = New System.Drawing.Point(372, 121)
            Me.picCheckImageUnChecked.Name = "picCheckImageUnChecked"
            Me.picCheckImageUnChecked.Size = New System.Drawing.Size(148, 49)
            Me.picCheckImageUnChecked.TabIndex = 6
            Me.picCheckImageUnChecked.TabStop = False
            '
            'picCheckImageUnCheckDisabled
            '
            Me.picCheckImageUnCheckDisabled.Image = CType(resources.GetObject("picCheckImageUnCheckDisabled.Image"), System.Drawing.Image)
            Me.picCheckImageUnCheckDisabled.Location = New System.Drawing.Point(196, 121)
            Me.picCheckImageUnCheckDisabled.Name = "picCheckImageUnCheckDisabled"
            Me.picCheckImageUnCheckDisabled.Size = New System.Drawing.Size(148, 49)
            Me.picCheckImageUnCheckDisabled.TabIndex = 5
            Me.picCheckImageUnCheckDisabled.TabStop = False
            '
            'picCheckImageChecked
            '
            Me.picCheckImageChecked.Image = CType(resources.GetObject("picCheckImageChecked.Image"), System.Drawing.Image)
            Me.picCheckImageChecked.Location = New System.Drawing.Point(12, 121)
            Me.picCheckImageChecked.Name = "picCheckImageChecked"
            Me.picCheckImageChecked.Size = New System.Drawing.Size(148, 49)
            Me.picCheckImageChecked.TabIndex = 4
            Me.picCheckImageChecked.TabStop = False
            '
            'picRadioImageDisabled
            '
            Me.picRadioImageDisabled.Image = CType(resources.GetObject("picRadioImageDisabled.Image"), System.Drawing.Image)
            Me.picRadioImageDisabled.Location = New System.Drawing.Point(550, 197)
            Me.picRadioImageDisabled.Name = "picRadioImageDisabled"
            Me.picRadioImageDisabled.Size = New System.Drawing.Size(148, 49)
            Me.picRadioImageDisabled.TabIndex = 11
            Me.picRadioImageDisabled.TabStop = False
            '
            'picRadioImageUnChecked
            '
            Me.picRadioImageUnChecked.Image = CType(resources.GetObject("picRadioImageUnChecked.Image"), System.Drawing.Image)
            Me.picRadioImageUnChecked.Location = New System.Drawing.Point(372, 197)
            Me.picRadioImageUnChecked.Name = "picRadioImageUnChecked"
            Me.picRadioImageUnChecked.Size = New System.Drawing.Size(148, 49)
            Me.picRadioImageUnChecked.TabIndex = 10
            Me.picRadioImageUnChecked.TabStop = False
            '
            'picRadioImageUnCheckDisabled
            '
            Me.picRadioImageUnCheckDisabled.Image = CType(resources.GetObject("picRadioImageUnCheckDisabled.Image"), System.Drawing.Image)
            Me.picRadioImageUnCheckDisabled.Location = New System.Drawing.Point(196, 197)
            Me.picRadioImageUnCheckDisabled.Name = "picRadioImageUnCheckDisabled"
            Me.picRadioImageUnCheckDisabled.Size = New System.Drawing.Size(148, 49)
            Me.picRadioImageUnCheckDisabled.TabIndex = 9
            Me.picRadioImageUnCheckDisabled.TabStop = False
            '
            'picRadioImageChecked
            '
            Me.picRadioImageChecked.Image = CType(resources.GetObject("picRadioImageChecked.Image"), System.Drawing.Image)
            Me.picRadioImageChecked.Location = New System.Drawing.Point(12, 197)
            Me.picRadioImageChecked.Name = "picRadioImageChecked"
            Me.picRadioImageChecked.Size = New System.Drawing.Size(148, 49)
            Me.picRadioImageChecked.TabIndex = 8
            Me.picRadioImageChecked.TabStop = False
            '
            'frmResource
            '
            Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
            Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
            Me.ClientSize = New System.Drawing.Size(800, 450)
            Me.Controls.Add(Me.picRadioImageDisabled)
            Me.Controls.Add(Me.picRadioImageUnChecked)
            Me.Controls.Add(Me.picRadioImageUnCheckDisabled)
            Me.Controls.Add(Me.picRadioImageChecked)
            Me.Controls.Add(Me.picCheckImageDisabled)
            Me.Controls.Add(Me.picCheckImageUnChecked)
            Me.Controls.Add(Me.picCheckImageUnCheckDisabled)
            Me.Controls.Add(Me.picCheckImageChecked)
            Me.Controls.Add(Me.picButtonImageDisabled)
            Me.Controls.Add(Me.picButtonImagePressed)
            Me.Controls.Add(Me.picButtonImageHover)
            Me.Controls.Add(Me.picButtonImageNormal)
            Me.Name = "frmResource"
            Me.Text = "frmResource"
            CType(Me.picButtonImageNormal, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picButtonImageHover, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picButtonImagePressed, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picButtonImageDisabled, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picCheckImageDisabled, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picCheckImageUnChecked, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picCheckImageUnCheckDisabled, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picCheckImageChecked, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picRadioImageDisabled, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picRadioImageUnChecked, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picRadioImageUnCheckDisabled, System.ComponentModel.ISupportInitialize).EndInit()
            CType(Me.picRadioImageChecked, System.ComponentModel.ISupportInitialize).EndInit()
            Me.ResumeLayout(False)

        End Sub

        Friend WithEvents picButtonImageNormal As PictureBox
    Friend WithEvents picButtonImageHover As PictureBox
    Friend WithEvents picButtonImagePressed As PictureBox
    Friend WithEvents picButtonImageDisabled As PictureBox
    Friend WithEvents picCheckImageDisabled As PictureBox
    Friend WithEvents picCheckImageUnChecked As PictureBox
    Friend WithEvents picCheckImageUnCheckDisabled As PictureBox
    Friend WithEvents picCheckImageChecked As PictureBox
    Friend WithEvents picRadioImageDisabled As PictureBox
    Friend WithEvents picRadioImageUnChecked As PictureBox
    Friend WithEvents picRadioImageUnCheckDisabled As PictureBox
    Friend WithEvents picRadioImageChecked As PictureBox
End Class

End Namespace
