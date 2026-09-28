Imports System.Windows.Forms
Imports Aqua

Public Class frmTest_Grid

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Text = "Aqua Grid — Form1"
        Grid1.Color = ColorConstants.Blue
        Grid1.Parhelia = True

        ' columns
        Grid1.Header.AdditionHeader(Nothing, "姓名", AlignmentConstants.LeftJustify, 160, True, Aqua.SortOrder.None)
        Grid1.Header.AdditionHeader(Nothing, "部門", AlignmentConstants.Center, 130, True, Aqua.SortOrder.None)
        Grid1.Header.AdditionHeader(Nothing, "分機", AlignmentConstants.RightJustify, 90, True, Aqua.SortOrder.None)
        Grid1.Header.AdditionHeader(Nothing, "在職", AlignmentConstants.Center, 90, False, Aqua.SortOrder.None)
        Grid1.SetHeaderProperty()

        ' sample data
        Dim names = New String() {"王小明", "李美華", "陳大文", "林志偉", "張淑芬", "黃建宏",
                                   "吳雅婷", "劉俊傑", "蔡佳蓉", "鄭文彬", "許雅雯", "謝東霖",
                                   "楊淑惠", "曾志豪", "周宜蓁", "施品睿", "趙敏君", "潘冠廷"}
        Dim depts = New String() {"內科", "外科", "放射科", "檢驗科", "藥劑部", "護理部"}

        Grid1.BeginUpdate()
        Try
            For i = 0 To 29
                Grid1.Cell(0, i).Text = names(i Mod names.Length) & (i \ names.Length + 1).ToString()
                Grid1.Cell(1, i).Text = depts(i Mod depts.Length)
                Grid1.Cell(2, i).Text = CStr(3000 + i)
                Dim st As Cell = Grid1.Cell(3, i)
                st.CheckStyle = ItemCheckStyle.Check
                st.Checked = (i Mod 3 <> 0)
            Next
        Finally
            Grid1.EndUpdate()
        End Try
    End Sub

End Class
