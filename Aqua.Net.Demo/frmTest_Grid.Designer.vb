<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmTest_Grid
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
        Me.Grid1 = New Aqua.Grid()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'Grid1
        '
        Me.Grid1.ActiveControl = True
        Me.Grid1.BackColor = System.Drawing.Color.White
        Me.Grid1.BorderColor = System.Drawing.Color.FromArgb(CType(CType(99, Byte), Integer), CType(CType(101, Byte), Integer), CType(CType(99, Byte), Integer))
        Me.Grid1.Color = Aqua.ColorConstants.Blue
        Me.Grid1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Grid1.EvenBackColor = System.Drawing.Color.White
        Me.Grid1.Font = New System.Drawing.Font("微軟正黑體", 10.0!)
        Me.Grid1.ForeColor = System.Drawing.Color.Black
        Me.Grid1.Location = New System.Drawing.Point(0, 0)
        Me.Grid1.Margin = New System.Windows.Forms.Padding(3, 2, 3, 2)
        Me.Grid1.Name = "Grid1"
        Me.Grid1.OddBackColor = System.Drawing.Color.FromArgb(CType(CType(239, Byte), Integer), CType(CType(243, Byte), Integer), CType(CType(255, Byte), Integer))
        Me.Grid1.SelColor = System.Drawing.Color.FromArgb(CType(CType(61, Byte), Integer), CType(CType(128, Byte), Integer), CType(CType(223, Byte), Integer))
        Me.Grid1.SelectionMode = Aqua.GridSelectionMode.Row
        Me.Grid1.Size = New System.Drawing.Size(735, 412)
        Me.Grid1.TabIndex = 0
        Me.Grid1.TopIndex = 0
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(444, 38)
        Me.Button1.Margin = New System.Windows.Forms.Padding(3, 2, 3, 2)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(84, 22)
        Me.Button1.TabIndex = 1
        Me.Button1.Text = "Button1"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'frmTest_Grid
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(735, 412)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.Grid1)
        Me.Margin = New System.Windows.Forms.Padding(3, 2, 3, 2)
        Me.Name = "frmTest_Grid"
        Me.Text = "Form1"
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents Grid1 As Aqua.Grid
    Friend WithEvents Button1 As System.Windows.Forms.Button
End Class
