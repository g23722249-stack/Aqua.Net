Imports Aqua

' RatingContextMenu 示範表單 (純程式碼建立，無 designer/resx)
' 執行方式：把專案 StartupObject 設成此表單，或在 Form1 按鈕內以
'   Dim f As New RatingContextMenuDemo() : f.Show()  開啟。
Public Class RatingContextMenuDemo
    Inherits Form

    Private WithEvents starMenu As New RatingContextMenu()   ' 5 顆星，可改成 New RatingContextMenu(10)
    Private ReadOnly lblResult As New System.Windows.Forms.Label()
    Private ReadOnly target As New Panel()

    Public Sub New()
        Me.Text = "RatingContextMenu 示範"
        Me.ClientSize = New Size(360, 220)
        Me.StartPosition = FormStartPosition.CenterScreen

        ' 在評分列上下各加幾個一般項目，預覽整體選單外觀
        starMenu.Items.Insert(0, New ToolStripSeparator())
        starMenu.Items.Insert(0, New ToolStripMenuItem("上方項目 2"))
        starMenu.Items.Insert(0, New ToolStripMenuItem("上方項目 1"))
        starMenu.Items.Add(New ToolStripSeparator())
        starMenu.Items.Add(New ToolStripMenuItem("下方項目 1"))
        starMenu.Items.Add(New ToolStripMenuItem("下方項目 2"))

        ' 讓灰色面板的右鍵選單使用星等評分
        target.Bounds = New Rectangle(20, 20, 320, 120)
        target.BackColor = Color.WhiteSmoke
        target.BorderStyle = BorderStyle.FixedSingle
        target.ContextMenuStrip = starMenu

        Dim hint As New System.Windows.Forms.Label() With {
            .Text = "在此區域按右鍵，點選星星評分",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleCenter
        }
        target.Controls.Add(hint)

        lblResult.Bounds = New Rectangle(20, 160, 320, 30)
        lblResult.Text = "目前評分：0"

        Me.Controls.Add(target)
        Me.Controls.Add(lblResult)
    End Sub

    Private Sub starMenu_RatingChanged(rating As Integer) Handles starMenu.RatingChanged
        lblResult.Text = String.Format("目前評分：{0} 顆星 (SelectedRating = {1})", rating, starMenu.SelectedRating)
    End Sub
End Class
