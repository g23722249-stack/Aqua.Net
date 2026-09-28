Imports System.Windows.Forms

''' <summary>Test harness for AquaForm/iForm: a plain launcher window with buttons that open a
''' fully configured AquaForm (title, icon-less title bar, two-level main menu, sizable border)
''' and a plain iForm, plus a few controls placed on the AquaForm's own content area to exercise
''' TopMost/Opacity/BorderStyle/menu-selection at runtime.</summary>
Public Class frmTest_AquaForm
    Inherits Form

    Private WithEvents btnOpenAquaForm As Button
    Private WithEvents btnOpenIForm As Button
    Private lblHint As Label

    Private _aquaFormInstance As Aqua.AquaForm
    Private _iFormInstance As Aqua.iForm

    Public Sub New()
        Text = "frmTest_AquaForm"
        ClientSize = New Drawing.Size(360, 160)
        StartPosition = FormStartPosition.CenterScreen

        lblHint = New Label() With {
            .Text = "測試 AquaForm（含主選單、標題列、控制鈕）與 iForm（四角圓角、無選單）。" & vbCrLf &
                    "開啟後請實際試試拖曳標題列、縮放右下角、最小化/最大化、以及選單點擊。",
            .Location = New Drawing.Point(16, 12),
            .Size = New Drawing.Size(328, 48),
            .AutoSize = False
        }
        Controls.Add(lblHint)

        btnOpenAquaForm = New Button() With {
            .Text = "開啟 AquaForm 測試視窗",
            .Location = New Drawing.Point(16, 70),
            .Size = New Drawing.Size(328, 32)
        }
        Controls.Add(btnOpenAquaForm)

        btnOpenIForm = New Button() With {
            .Text = "開啟 iForm 測試視窗",
            .Location = New Drawing.Point(16, 110),
            .Size = New Drawing.Size(328, 32)
        }
        Controls.Add(btnOpenIForm)
    End Sub

    Private Sub btnOpenAquaForm_Click(sender As Object, e As EventArgs) Handles btnOpenAquaForm.Click
        If _aquaFormInstance IsNot Nothing AndAlso Not _aquaFormInstance.IsDisposed Then
            _aquaFormInstance.Activate()
            Return
        End If

        Dim frm As New Aqua.AquaForm()
        frm.Text = "AquaForm 測試視窗"
        frm.Size = New Drawing.Size(480, 340)
        frm.StartPosition = FormStartPosition.Manual
        frm.Location = New Drawing.Point(Left + Width + 30, Top)
        frm.WindowBorderStyle = Aqua.FormBorderStyle.Sizable
        frm.CloseButton = True
        frm.MinButton = True
        frm.MaxButton = True

        Dim root As New Aqua.MenuItem()
        Dim fileMenu As New Aqua.MenuItem("檔案")
        fileMenu.AddItem(New Aqua.MenuItem("開新檔案"))
        fileMenu.AddItem(New Aqua.MenuItem("開啟舊檔"))
        fileMenu.AddItem(New Aqua.MenuItem("-"))
        Dim exportItem As New Aqua.MenuItem("匯出")
        exportItem.AddItem(New Aqua.MenuItem("匯出成 PDF"))
        exportItem.AddItem(New Aqua.MenuItem("匯出成 PNG"))
        fileMenu.AddItem(exportItem)
        root.AddItem(fileMenu)
        Dim editMenu As New Aqua.MenuItem("編輯")
        editMenu.AddItem(New Aqua.MenuItem("復原"))
        editMenu.AddItem(New Aqua.MenuItem("取消復原"))
        root.AddItem(editMenu)
        root.AddItem(New Aqua.MenuItem("說明"))
        frm.AddMenu(root)

        Dim lblStatus As New Label() With {
            .Text = "選單點擊會顯示在這裡",
            .Location = New Drawing.Point(20, 65),
            .Size = New Drawing.Size(420, 24),
            .AutoSize = False
        }
        EnableDoubleBuffer(lblStatus)
        frm.Controls.Add(lblStatus)
        AddHandler frm.MenuSelected, Sub(s, item) lblStatus.Text = "上次選擇的選單項目：" & item.Text

        Dim btnTopMost As New Button() With {.Text = "切換 TopMost", .Location = New Drawing.Point(20, 100), .Size = New Drawing.Size(140, 28)}
        AddHandler btnTopMost.Click, Sub() frm.TopMost = Not frm.TopMost
        EnableDoubleBuffer(btnTopMost)
        frm.Controls.Add(btnTopMost)

        Dim btnOpacity As New Button() With {.Text = "切換 Opacity", .Location = New Drawing.Point(170, 100), .Size = New Drawing.Size(140, 28)}
        AddHandler btnOpacity.Click, Sub() frm.Opacity = If(frm.Opacity < 1.0, 1.0, 0.7)
        EnableDoubleBuffer(btnOpacity)
        frm.Controls.Add(btnOpacity)

        Dim btnBorder As New Button() With {.Text = "切換 BorderStyle", .Location = New Drawing.Point(320, 100), .Size = New Drawing.Size(120, 28)}
        AddHandler btnBorder.Click, Sub() frm.WindowBorderStyle = If(frm.WindowBorderStyle = Aqua.FormBorderStyle.Sizable, Aqua.FormBorderStyle.Fixed, Aqua.FormBorderStyle.Sizable)
        EnableDoubleBuffer(btnBorder)
        frm.Controls.Add(btnBorder)

        Dim lblEvents As New Label() With {
            .Text = "",
            .Location = New Drawing.Point(20, 140),
            .Size = New Drawing.Size(420, 100),
            .AutoSize = False
        }
        EnableDoubleBuffer(lblEvents)
        frm.Controls.Add(lblEvents)
        AddHandler frm.Minimize, Sub() AppendEventLog(lblEvents, "Minimize")
        AddHandler frm.Maximize, Sub() AppendEventLog(lblEvents, "Maximize")
        AddHandler frm.Restore, Sub() AppendEventLog(lblEvents, "Restore")
        AddHandler frm.Activated, Sub() AppendEventLog(lblEvents, "Activated")
        AddHandler frm.MenuOpen, Sub() AppendEventLog(lblEvents, "MenuOpen")
        AddHandler frm.MenuClose, Sub() AppendEventLog(lblEvents, "MenuClose")

        _aquaFormInstance = frm
        frm.Show()
    End Sub

    Private Sub btnOpenIForm_Click(sender As Object, e As EventArgs) Handles btnOpenIForm.Click
        If _iFormInstance IsNot Nothing AndAlso Not _iFormInstance.IsDisposed Then
            _iFormInstance.Activate()
            Return
        End If

        Dim frm As New Aqua.iForm()
        frm.Text = "iForm 測試視窗"
        frm.Size = New Drawing.Size(360, 260)
        frm.StartPosition = FormStartPosition.Manual
        frm.Location = New Drawing.Point(Left, Top + Height + 30)
        frm.WindowBorderStyle = Aqua.FormBorderStyle.Sizable

        Dim btnBorder As New Button() With {.Text = "切換 BorderStyle", .Location = New Drawing.Point(20, 60), .Size = New Drawing.Size(140, 28)}
        AddHandler btnBorder.Click, Sub() frm.WindowBorderStyle = If(frm.WindowBorderStyle = Aqua.FormBorderStyle.Sizable, Aqua.FormBorderStyle.Fixed, Aqua.FormBorderStyle.Sizable)
        EnableDoubleBuffer(btnBorder)
        frm.Controls.Add(btnBorder)

        Dim btnTopMost As New Button() With {.Text = "切換 TopMost", .Location = New Drawing.Point(170, 60), .Size = New Drawing.Size(140, 28)}
        AddHandler btnTopMost.Click, Sub() frm.TopMost = Not frm.TopMost
        EnableDoubleBuffer(btnTopMost)
        frm.Controls.Add(btnTopMost)

        _iFormInstance = frm
        frm.Show()
    End Sub

    Private Sub AppendEventLog(lbl As Label, text As String)
        lbl.Text = (lbl.Text & vbCrLf & DateTime.Now.ToString("HH:mm:ss") & "  " & text).TrimStart(vbCrLf.ToCharArray())
    End Sub

    ''' <summary>Plain System.Windows.Forms.Button/Label aren't double-buffered by default, so
    ''' while their AquaForm/iForm host resizes rapidly they redraw via the ordinary (flicker-prone)
    ''' path -- separately from, and on top of, AquaForm/iForm's own manually double-buffered
    ''' chrome. Control.DoubleBuffered is Protected, so a caller outside the control's own class
    ''' can't just set it; flipping it on via reflection here is the standard workaround.</summary>
    Private Shared Sub EnableDoubleBuffer(ctrl As Control)
        Dim prop = GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        prop?.SetValue(ctrl, True, Nothing)
    End Sub

    Private Sub InitializeComponent()
        Me.SuspendLayout()
        '
        'frmTest_AquaForm
        '
        Me.ClientSize = New System.Drawing.Size(284, 261)
        Me.Name = "frmTest_AquaForm"
        Me.ResumeLayout(False)

    End Sub

    Private Sub frmTest_AquaForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class
