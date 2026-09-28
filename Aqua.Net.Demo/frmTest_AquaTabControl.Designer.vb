<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmTest_AquaTabControl
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
        Me.AquaTabControl1 = New Aqua.TabControl()
        Me.TabPage1 = New Aqua.TabPage()
        Me.TabPage2 = New Aqua.TabPage()
        Me.TabPage3 = New Aqua.TabPage()
        Me.TabPage4 = New Aqua.TabPage()
        Me.AquaTabControl1.SuspendLayout()
        Me.SuspendLayout()
        '
        'AquaTabControl1
        '
        Me.AquaTabControl1.BackColor = System.Drawing.Color.White
        Me.AquaTabControl1.Controls.Add(Me.TabPage1)
        Me.AquaTabControl1.Controls.Add(Me.TabPage2)
        Me.AquaTabControl1.Controls.Add(Me.TabPage3)
        Me.AquaTabControl1.Controls.Add(Me.TabPage4)
        Me.AquaTabControl1.Location = New System.Drawing.Point(26, 28)
        Me.AquaTabControl1.Name = "AquaTabControl1"
        Me.AquaTabControl1.Size = New System.Drawing.Size(753, 396)
        Me.AquaTabControl1.TabIndex = 1
        Me.AquaTabControl1.TabPages.Add(Me.TabPage1)
        Me.AquaTabControl1.TabPages.Add(Me.TabPage2)
        Me.AquaTabControl1.TabPages.Add(Me.TabPage3)
        Me.AquaTabControl1.TabPages.Add(Me.TabPage4)
        Me.AquaTabControl1.Text = "AquaTabControl1"
        '
        'TabPage1
        '
        Me.TabPage1.BackColor = System.Drawing.Color.White
        Me.TabPage1.Location = New System.Drawing.Point(3, 30)
        Me.TabPage1.Name = "TabPage1"
        Me.TabPage1.Size = New System.Drawing.Size(747, 363)
        Me.TabPage1.TabIndex = 0
        '
        'TabPage2
        '
        Me.TabPage2.BackColor = System.Drawing.Color.White
        Me.TabPage2.Location = New System.Drawing.Point(3, 30)
        Me.TabPage2.Name = "TabPage2"
        Me.TabPage2.Size = New System.Drawing.Size(747, 363)
        Me.TabPage2.TabIndex = 1
        '
        'TabPage3
        '
        Me.TabPage3.BackColor = System.Drawing.Color.White
        Me.TabPage3.Location = New System.Drawing.Point(3, 30)
        Me.TabPage3.Name = "TabPage3"
        Me.TabPage3.Size = New System.Drawing.Size(747, 363)
        Me.TabPage3.TabIndex = 2
        '
        'TabPage4
        '
        Me.TabPage4.BackColor = System.Drawing.Color.White
        Me.TabPage4.Location = New System.Drawing.Point(3, 30)
        Me.TabPage4.Name = "TabPage4"
        Me.TabPage4.Size = New System.Drawing.Size(747, 363)
        Me.TabPage4.TabIndex = 3
        '
        'frmTest_AquaTabControl
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(800, 450)
        Me.Controls.Add(Me.AquaTabControl1)
        Me.Name = "frmTest_AquaTabControl"
        Me.Text = "frmTest_AquaTabControl"
        Me.AquaTabControl1.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents AquaTabControl1 As Aqua.TabControl
    Friend WithEvents TabPage1 As Aqua.TabPage
    Friend WithEvents TabPage2 As Aqua.TabPage
    Friend WithEvents TabPage3 As Aqua.TabPage
    Friend WithEvents TabPage4 As Aqua.TabPage
End Class
