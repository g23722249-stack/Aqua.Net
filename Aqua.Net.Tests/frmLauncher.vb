Option Strict Off

Imports System.Drawing
Imports System.Windows.Forms
Imports Aqua

''' <summary>
''' 啟動器:開啟各測試表單,並就地示範 ImageButton / ImageCheckBox / ImageRadioButton
''' (這三個控制項在原測試專案沒有專屬乾淨表單,故直接放在啟動器上示範)。
''' 本專案只參考 Aqua.Net,所有 Aqua.* 型別皆從 Aqua.Net 解析。
''' </summary>
Public Class frmLauncher
    Inherits Form

    Public Sub New()
        Text = "Aqua.Net 已移植控制項 — 測試啟動器"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(560, 460)
        Font = New Font("Microsoft JhengHei", 9.0F)

        Dim y As Integer = 16
        AddLauncher("frmKeyImage (CheckBox + MediaItem + MediaViewerControl + RatingControl)",
                    Function() CType(New frmKeyImage(), Form), y)
        AddLauncher("frmMediaList (MediaList + PrintImage + ScreenPrintImage)",
                    Function() CType(New frmMediaList(), Form), y)
        AddLauncher("RatingContextMenuDemo (RatingContextMenu)",
                    Function() CType(New RatingContextMenuDemo(), Form), y)

        ' --- Button / CheckBox / RadioButton(影像式)就地示範 ---
        Dim lbl As New Label() With {
            .Text = "Button / CheckBox / RadioButton 示範:",
            .Location = New Point(16, y + 8), .AutoSize = True}
        Controls.Add(lbl)
        y += 34

        Dim ib As New Aqua.Button() With {.Text = "ImageButton", .Location = New Point(16, y), .Size = New Size(120, 40)}
        AddHandler ib.Click, Sub() MessageBox.Show("ImageButton clicked")
        Controls.Add(ib)

        Dim icb As New Aqua.CheckBox() With {.Text = "ImageCheckBox", .Location = New Point(160, y), .AutoSize = True}
        Controls.Add(icb)

        Dim irb1 As New Aqua.RadioButton() With {.Text = "選項 A", .Location = New Point(320, y), .AutoSize = True}
        Dim irb2 As New Aqua.RadioButton() With {.Text = "選項 B", .Location = New Point(320, y + 24), .AutoSize = True}
        Controls.Add(irb1)
        Controls.Add(irb2)
    End Sub

    Private Sub AddLauncher(caption As String, opener As Func(Of Form), ByRef y As Integer)
        Dim b As New System.Windows.Forms.Button() With {
            .Text = caption, .Location = New Point(16, y), .Size = New Size(510, 30),
            .TextAlign = ContentAlignment.MiddleLeft}
        AddHandler b.Click, Sub()
                                Try
                                    opener().Show()
                                Catch ex As Exception
                                    MessageBox.Show(ex.ToString(), "開啟失敗")
                                End Try
                            End Sub
        Controls.Add(b)
        y += 36
    End Sub

End Class
