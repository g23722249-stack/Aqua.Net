Option Strict Off
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Printing
Imports System.IO
Imports System.Windows.Forms

Namespace Global.Aqua


''' <summary>
''' 圖片列印元件:結合 PrintDialog(選印表機)與 PrintPreviewDialog(預覽)。
''' 依每行張數自動計算圖片大小(等比、可將直式圖旋轉為橫向)、每行高度取最高者、
''' 自動換頁。座標單位為 1/100 吋(印表機 Graphics 預設單位)。
''' </summary>
<ToolboxItem(True)>
Public Class PrintImage
    Inherits Component

    Private ReadOnly _doc As New PrintDocument()

    ' 屬性欄位
    Private _rotatePortrait As Boolean = False
    Private _marginTop As Single = 5.0F
    Private _marginBottom As Single = 5.0F
    Private _marginLeft As Single = 5.0F
    Private _marginRight As Single = 5.0F
    Private _imageSpacing As Single = 2.0F

    ' 列印過程狀態
    Private _files As String() = Nothing
    Private _rowCount As Integer = 1
    Private _index As Integer = 0

    Public Sub New()
        _doc.DocumentName = "PrintImage"
        AddHandler _doc.BeginPrint, AddressOf OnBeginPrint
        AddHandler _doc.PrintPage, AddressOf OnPrintPage
    End Sub

    ' ===== 屬性 =====

    ''' <summary>是否將直式圖片(高>寬)旋轉 90° 以橫向列印。</summary>
    <Category("PrintImage"), Description("是否將直式圖片旋轉 90° 以橫向列印。"), DefaultValue(False)>
    Public Property RotatePortraitToLandscape As Boolean
        Get
            Return _rotatePortrait
        End Get
        Set(value As Boolean)
            _rotatePortrait = value
        End Set
    End Property

    ''' <summary>上邊界(mm)。</summary>
    <Category("PrintImage"), Description("上邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginTop As Single
        Get
            Return _marginTop
        End Get
        Set(value As Single)
            _marginTop = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>下邊界(mm)。</summary>
    <Category("PrintImage"), Description("下邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginBottom As Single
        Get
            Return _marginBottom
        End Get
        Set(value As Single)
            _marginBottom = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>左邊界(mm)。</summary>
    <Category("PrintImage"), Description("左邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginLeft As Single
        Get
            Return _marginLeft
        End Get
        Set(value As Single)
            _marginLeft = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>右邊界(mm)。</summary>
    <Category("PrintImage"), Description("右邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginRight As Single
        Get
            Return _marginRight
        End Get
        Set(value As Single)
            _marginRight = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>圖片格之間的間距(mm)。</summary>
    <Category("PrintImage"), Description("圖片格之間的間距(mm)。"), DefaultValue(2.0!)>
    Public Property ImageSpacing As Single
        Get
            Return _imageSpacing
        End Get
        Set(value As Single)
            _imageSpacing = Math.Max(0, value)
        End Set
    End Property

    ''' <summary>目前選定的印表機名稱(可先設定或用 SelectPrinter 選擇,列印時沿用)。</summary>
    <Browsable(False)>
    Public Property PrinterName As String
        Get
            Return _doc.PrinterSettings.PrinterName
        End Get
        Set(value As String)
            If Not String.IsNullOrEmpty(value) Then
                _doc.PrinterSettings.PrinterName = value
            End If
        End Set
    End Property

    ''' <summary>目前選定的印表機是否有效可用。</summary>
    <Browsable(False)>
    Public ReadOnly Property IsPrinterValid As Boolean
        Get
            Return _doc.PrinterSettings.IsValid
        End Get
    End Property

    ' ===== 選擇印表機(獨立於列印,先選一次即可沿用) =====

    ''' <summary>
    ''' 顯示 PrintDialog 讓使用者選擇印表機,選定後儲存於元件,之後列印(Preview=False)直接沿用,不再每次跳對話框。
    ''' </summary>
    ''' <returns>使用者按確定傳回 True;取消傳回 False。</returns>
    Public Function SelectPrinter() As Boolean
        Using pd As New PrintDialog()
            pd.Document = _doc          ' 綁定後,按確定會將選擇寫回 _doc.PrinterSettings
            pd.UseEXDialog = True
            If pd.ShowDialog() = DialogResult.OK Then
                Return True
            End If
        End Using
        Return False
    End Function

    ' ===== 列印 =====

    ''' <summary>
    ''' 列印一組圖片。
    ''' </summary>
    ''' <param name="FileName">要列印的圖片檔清單。</param>
    ''' <param name="RowCount">每行列印的張數(每格寬 = 可印寬 ÷ RowCount)。</param>
    ''' <param name="Preview">True:顯示預覽視窗;False:直接以目前選定的印表機列印(先用 SelectPrinter 選一次即可,未選則用預設印表機)。</param>
    ''' <returns>實際送出列印/開啟預覽傳回 True;無圖片傳回 False。</returns>
    Public Function PrintImage(ByVal FileName() As String,
                               ByVal RowCount As Integer,
                               ByVal Preview As Boolean) As Boolean
        If FileName Is Nothing OrElse FileName.Length = 0 Then Return False

        _files = FileName
        _rowCount = Math.Max(1, RowCount)
        ApplyMargins()

        If Preview Then
            Using dlg As New PrintPreviewDialog()
                dlg.Document = _doc
                dlg.WindowState = FormWindowState.Maximized
                dlg.ShowDialog()
            End Using
        Else
            _doc.Print()   ' 直接沿用目前印表機設定,不再跳 PrintDialog
        End If
        Return True
    End Function

    ' 將 mm 邊界套用到 PrintDocument(Margins 單位為 1/100 吋)
    Private Sub ApplyMargins()
        _doc.DefaultPageSettings.Margins = New Margins(
            MmToHundredthInch(_marginLeft),
            MmToHundredthInch(_marginRight),
            MmToHundredthInch(_marginTop),
            MmToHundredthInch(_marginBottom))
    End Sub

    Private Shared Function MmToHundredthInch(ByVal mm As Single) As Integer
        Return CInt(Math.Round(mm / 25.4F * 100.0F))
    End Function

    Private Sub OnBeginPrint(sender As Object, e As PrintEventArgs)
        _index = 0
    End Sub

    Private Sub OnPrintPage(sender As Object, e As PrintPageEventArgs)
        Dim g As Graphics = e.Graphics
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.SmoothingMode = SmoothingMode.HighQuality

        Dim bounds As Rectangle = e.MarginBounds
        Dim total As Integer = _files.Length
        Dim sp As Single = MmToHundredthInch(_imageSpacing)
        Dim cellW As Single = (bounds.Width - (_rowCount - 1) * sp) / _rowCount
        If cellW < 1 Then cellW = 1

        Dim y As Single = bounds.Top

        Do While _index < total
            ' 載入本行圖片(最多 RowCount 張),旋轉、量測
            Dim imgs As New List(Of Image)()
            Dim drawW As New List(Of Single)()
            Dim drawH As New List(Of Single)()
            Dim rowHeight As Single = 0
            Dim startIdx As Integer = _index
            Dim col As Integer = 0
            While col < _rowCount AndAlso (startIdx + col) < total
                Dim img As Image = LoadForPrint(_files(startIdx + col))
                Dim iw As Single = 1, ih As Single = 1
                If img IsNot Nothing Then
                    iw = img.Width
                    ih = img.Height
                End If
                Dim scale As Single = cellW / iw
                imgs.Add(img)
                drawW.Add(cellW)
                drawH.Add(ih * scale)
                rowHeight = Math.Max(rowHeight, ih * scale)
                col += 1
            End While

            ' 單行高於整頁時,整行等比縮小以塞入頁高(避免裁切/無限迴圈)
            If rowHeight > bounds.Height Then
                Dim f As Single = bounds.Height / rowHeight
                For k As Integer = 0 To drawH.Count - 1
                    drawW(k) *= f
                    drawH(k) *= f
                Next
                rowHeight = bounds.Height
            End If

            ' 換頁:本行放不下且本頁已有內容 → 延到下一頁(不推進索引)
            If (y + rowHeight > bounds.Bottom) AndAlso (y > bounds.Top) Then
                DisposeAll(imgs)
                e.HasMorePages = True
                Return
            End If

            ' 繪製本行(每格內水平/垂直置中)
            For k As Integer = 0 To imgs.Count - 1
                Dim img As Image = imgs(k)
                Dim dW As Single = drawW(k)
                Dim dH As Single = drawH(k)
                Dim cellLeft As Single = bounds.Left + k * (cellW + sp)
                Dim ox As Single = cellLeft + (cellW - dW) / 2
                Dim oy As Single = y + (rowHeight - dH) / 2
                If img IsNot Nothing Then
                    g.DrawImage(img, ox, oy, dW, dH)
                    img.Dispose()
                End If
            Next

            y += rowHeight + sp
            _index = startIdx + col
        Loop

        e.HasMorePages = False
    End Sub

    ' 讀取圖片(不鎖檔),並依設定將直式圖旋轉為橫向
    Private Function LoadForPrint(ByVal path As String) As Image
        Try
            Dim bmp As Bitmap
            Dim bytes() As Byte = File.ReadAllBytes(path)
            Using ms As New MemoryStream(bytes)
                Using tmp As Image = Image.FromStream(ms)
                    bmp = New Bitmap(tmp)   ' 複製一份,解除對串流/檔案的相依
                End Using
            End Using
            If _rotatePortrait AndAlso bmp.Height > bmp.Width Then
                bmp.RotateFlip(RotateFlipType.Rotate90FlipNone)
            End If
            Return bmp
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Sub DisposeAll(ByVal imgs As List(Of Image))
        For Each im As Image In imgs
            If im IsNot Nothing Then im.Dispose()
        Next
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _doc.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

End Namespace
