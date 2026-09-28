Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Test harness for ItemListBox (Control\ListBox.ctl) and DropDownList
''' (Control\DropDownList.ctl): populates both with sample items (some carrying icons and check
''' marks) and logs their events so the interactive behaviour (selection, checking, scrolling, the
''' DropDownList popup) can be exercised at runtime.</summary>
Public Class frmTest_ListDropDown
    Inherits Form

    Private WithEvents ListBox1 As Aqua.ItemListBox
    Private WithEvents DropDownList1 As Aqua.DropDownList
    Private WithEvents btnAddItem As Button
    Private WithEvents btnRemoveSelected As Button
    Private WithEvents btnToggleCheckStyle As Button
    Private WithEvents btnToggleEnabled As Button
    Private WithEvents btnToggleColor As Button
    Private lblEvents As Label

    Private _addCounter As Integer = 0
    Private _checkStyleOn As Boolean = True

    Public Sub New()
        Text = "ItemListBox / DropDownList 測試視窗"
        ClientSize = New Drawing.Size(520, 420)
        StartPosition = FormStartPosition.CenterScreen

        ' Created before ListBox1/DropDownList1 below: populating them (DropDownList1.SelectedIndex
        ' in particular) raises SelectedChanged synchronously, and the handler calls AppendLog,
        ' which touches lblEvents -- it has to already exist by then.
        lblEvents = New Label() With {
            .Text = "",
            .Location = New Drawing.Point(16, 304),
            .Size = New Drawing.Size(488, 100),
            .AutoSize = False,
            .BorderStyle = BorderStyle.FixedSingle
        }
        Controls.Add(lblEvents)

        ListBox1 = New Aqua.ItemListBox() With {
            .Location = New Drawing.Point(16, 16),
            .Size = New Drawing.Size(220, 200),
            .CheckStyle = Aqua.ItemCheckStyle.Check,
            .Color = Aqua.ColorConstants.Blue
        }
        Controls.Add(ListBox1)

        Dim c1 = ListBox1.AddItem("apple", "蘋果")
        c1.Icon = SystemIcons.Information.ToBitmap()
        Dim c2 = ListBox1.AddItem("banana", "香蕉")
        Dim c3 = ListBox1.AddItem("cherry", "櫻桃")
        c3.Icon = SystemIcons.Warning.ToBitmap()
        ListBox1.AddItem("date", "棗子")
        ListBox1.AddItem("elderberry", "接骨木莓")
        ListBox1.AddItem("fig", "無花果")
        ListBox1.AddItem("grape", "葡萄")
        ListBox1.AddItem("honeydew", "哈密瓜")

        DropDownList1 = New Aqua.DropDownList() With {
            .Location = New Drawing.Point(260, 16),
            .Size = New Drawing.Size(220, 32),
            .Color = Aqua.ColorConstants.Green
        }
        Controls.Add(DropDownList1)
        Dim d1 = DropDownList1.AddItem("small", "小")
        Dim d2 = DropDownList1.AddItem("medium", "中")
        d2.Icon = SystemIcons.Information.ToBitmap()
        DropDownList1.AddItem("large", "大")
        DropDownList1.AddItem("xlarge", "特大")
        DropDownList1.SelectedIndex = 0

        btnAddItem = New Button() With {.Text = "AddItem", .Location = New Drawing.Point(16, 228), .Size = New Drawing.Size(104, 28)}
        Controls.Add(btnAddItem)

        btnRemoveSelected = New Button() With {.Text = "移除選取項", .Location = New Drawing.Point(132, 228), .Size = New Drawing.Size(104, 28)}
        Controls.Add(btnRemoveSelected)

        btnToggleCheckStyle = New Button() With {.Text = "切換 CheckStyle", .Location = New Drawing.Point(16, 264), .Size = New Drawing.Size(220, 28)}
        Controls.Add(btnToggleCheckStyle)

        btnToggleColor = New Button() With {.Text = "切換 Color", .Location = New Drawing.Point(260, 60), .Size = New Drawing.Size(104, 28)}
        Controls.Add(btnToggleColor)

        btnToggleEnabled = New Button() With {.Text = "切換 Enabled", .Location = New Drawing.Point(372, 60), .Size = New Drawing.Size(108, 28)}
        Controls.Add(btnToggleEnabled)
    End Sub

    Private Sub ListBox1_SelectedChanged(sender As Object, e As EventArgs) Handles ListBox1.SelectedChanged
        AppendLog($"ListBox SelectedChanged: index={ListBox1.SelectedIndex} text={If(ListBox1.SelectedItem?.Text, "(none)")}")
    End Sub

    Private Sub ListBox1_ItemClick(sender As Object, index As Integer) Handles ListBox1.ItemClick
        AppendLog($"ListBox ItemClick: index={index}")
    End Sub

    Private Sub DropDownList1_SelectedChanged(sender As Object, e As EventArgs) Handles DropDownList1.SelectedChanged
        AppendLog($"DropDownList SelectedChanged: {DropDownList1.Text}")
    End Sub

    Private Sub DropDownList1_MenuOpen(sender As Object, e As EventArgs) Handles DropDownList1.MenuOpen
        AppendLog("DropDownList MenuOpen")
    End Sub

    Private Sub DropDownList1_MenuClose(sender As Object, e As EventArgs) Handles DropDownList1.MenuClose
        AppendLog("DropDownList MenuClose")
    End Sub

    Private Sub btnAddItem_Click(sender As Object, e As EventArgs) Handles btnAddItem.Click
        _addCounter += 1
        ListBox1.AddItem("extra" & _addCounter, "新項目 " & _addCounter)
        AppendLog("AddItem: 新項目 " & _addCounter)
    End Sub

    Private Sub btnRemoveSelected_Click(sender As Object, e As EventArgs) Handles btnRemoveSelected.Click
        If ListBox1.SelectedIndex >= 0 Then
            Dim text = ListBox1.SelectedItem.Text
            ListBox1.RemoveItem(ListBox1.SelectedIndex)
            AppendLog("RemoveItem: " & text)
        End If
    End Sub

    Private Sub btnToggleCheckStyle_Click(sender As Object, e As EventArgs) Handles btnToggleCheckStyle.Click
        _checkStyleOn = Not _checkStyleOn
        ListBox1.CheckStyle = If(_checkStyleOn, Aqua.ItemCheckStyle.Check, Aqua.ItemCheckStyle.None)
        AppendLog("CheckStyle -> " & ListBox1.CheckStyle.ToString())
    End Sub

    Private Sub btnToggleColor_Click(sender As Object, e As EventArgs) Handles btnToggleColor.Click
        DropDownList1.Color = If(DropDownList1.Color = Aqua.ColorConstants.Green, Aqua.ColorConstants.Red, Aqua.ColorConstants.Green)
        AppendLog("DropDownList Color -> " & DropDownList1.Color.ToString())
    End Sub

    Private Sub btnToggleEnabled_Click(sender As Object, e As EventArgs) Handles btnToggleEnabled.Click
        DropDownList1.Enabled = Not DropDownList1.Enabled
        AppendLog("DropDownList Enabled -> " & DropDownList1.Enabled.ToString())
    End Sub

    Private Sub AppendLog(ByVal text As String)
        lblEvents.Text = (lblEvents.Text & vbCrLf & DateTime.Now.ToString("HH:mm:ss") & "  " & text).TrimStart(vbCrLf.ToCharArray())
    End Sub

End Class
