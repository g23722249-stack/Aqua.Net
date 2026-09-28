Public Class frmMediaList
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        MediaList1.Clear()

        Dim strFiles() As String = IO.Directory.GetFiles("C:\Pacs\YM_PIC\OPDEEG150514059B")

        For I As Integer = 0 To strFiles.Length - 1
            MediaList1.AddItem(strFiles(I), 0, False, False, "")
        Next


    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        PrintImage1.SelectPrinter()
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim strFiles() As String = IO.Directory.GetFiles("C:\Pacs\YM_PIC\OPDEEG150514059B")

        PrintImage1.MarginTop = 15
        PrintImage1.MarginBottom = 15

        PrintImage1.RotatePortraitToLandscape = True
        PrintImage1.PrintImage(strFiles, 2, True)
    End Sub

    ' 自動填入:把資料夾圖片依序填入 ScreenPrintImage 的空格
    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Dim strFiles() As String = IO.Directory.GetFiles("C:\Pacs\YM_PIC\OPDEEG150514059B")
        ScreenPrintImage1.AutoFill(strFiles)
    End Sub

    ' 匯出 PDF
    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "PDF|*.pdf"
            dlg.FileName = "ScreenPrint.pdf"
            If dlg.ShowDialog() = DialogResult.OK Then
                If ScreenPrintImage1.ExportPdf(dlg.FileName) Then
                    MessageBox.Show("已匯出:" & dlg.FileName)
                Else
                    MessageBox.Show("匯出 PDF 失敗(請確認已安裝『Microsoft Print to PDF』印表機)。")
                End If
            End If
        End Using
    End Sub

    ' 匯出圖片
    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp"
            dlg.FileName = "ScreenPrint.png"
            If dlg.ShowDialog() = DialogResult.OK Then
                If ScreenPrintImage1.ExportImage(dlg.FileName, 200) Then
                    MessageBox.Show("已匯出:" & dlg.FileName)
                Else
                    MessageBox.Show("匯出圖片失敗。")
                End If
            End If
        End Using
    End Sub

    ' 每列張數
    Private Sub nudRow_ValueChanged(sender As Object, e As EventArgs) Handles nudRow.ValueChanged
        ScreenPrintImage1.RowCount = CInt(nudRow.Value)
    End Sub

    ' 上下左右邊界(mm)
    Private Sub nudMargin_ValueChanged(sender As Object, e As EventArgs) Handles nudMargin.ValueChanged
        Dim m As Single = CSng(nudMargin.Value)
        ScreenPrintImage1.MarginTop = m
        ScreenPrintImage1.MarginBottom = m
        ScreenPrintImage1.MarginLeft = m
        ScreenPrintImage1.MarginRight = m
    End Sub

    Private Sub ScreenPrintImage1_Load(sender As Object, e As EventArgs) Handles ScreenPrintImage1.Load

    End Sub
End Class