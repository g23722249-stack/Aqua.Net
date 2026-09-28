Option Strict Off

Imports System.Windows.Forms

' 焦點測試專案的自訂進入點:只參考 Aqua.Net(已移植的 10 個 Aqua 控制項)。
Module Program

    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New frmLauncher())
    End Sub

End Module
