Public Class frmKeyImage
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        MediaViewerControl1.FileName = "C:\Pacs\YM_PIC\220260525893135_2026052514314700717.JPG"
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        MediaViewerControl1.FileName = "C:\Pacs\vecteezy_buffering-loading-animation-black-screen_44175645.mp4"
    End Sub

    Private Sub MediaViewerControl2_MediaClicked(sender As Object, e As Aqua.MediaClickedEventArgs) Handles MediaViewerControl2.MediaClicked

    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim A As New RatingContextMenuDemo
        A.ShowDialog()
    End Sub

    Private Sub MediaViewerControl1_MediaClicked(sender As Object, e As Aqua.MediaClickedEventArgs) Handles MediaViewerControl1.MediaClicked

    End Sub

    Private Sub MediaViewerControl1_DropMedia(FileName As String) Handles MediaViewerControl1.DropMedia
        MsgBox(FileName)
    End Sub

    Private Sub MediaViewerControl1_DragDrop(sender As Object, e As DragEventArgs) Handles MediaViewerControl1.DragDrop

    End Sub
End Class