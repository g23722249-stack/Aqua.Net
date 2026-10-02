Option Strict Off
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Drawing.Printing
Imports System.IO
Imports System.Windows.Forms

''' <summary>
''' 視覺化 A4 排版列印控制項:畫面呈現 A4 比例頁面,依 RowCount 產生等距格子容器;
''' 可從 MediaItem / MediaViewerControl 拖曳圖片放入指定格子決定列印位置,再直接列印/預覽/匯出。
''' </summary>
Namespace Global.Aqua

<DefaultProperty("RowCount")>
Public Class ScreenPrintImage
    Inherits UserControl

    Private Const A4W_MM As Single = 210.0F
    Private Const A4H_MM As Single = 297.0F
    Friend Const MediaDragFormat As String = "MediaViewerFile"   ' 與 MediaViewerControl 相同

    Private ReadOnly _doc As New PrintDocument()
    Private ReadOnly _slots As New List(Of SlotBox)()

    ' 右鍵選單
    Private _menu As ContextMenuStrip
    Private _miClear As ToolStripMenuItem
    Private _miRotate As ToolStripMenuItem
    Private _miFill As ToolStripMenuItem
    Private _miPreview As ToolStripMenuItem
    Private _miPrint As ToolStripMenuItem
    Private _miSelectPrinter As ToolStripMenuItem
    Private _menuSlot As SlotBox = Nothing

    Private _rotatePortrait As Boolean = False
    Private _marginTop As Single = 5.0F
    Private _marginBottom As Single = 5.0F
    Private _marginLeft As Single = 5.0F
    Private _marginRight As Single = 5.0F
    Private _rowCount As Integer = 2
    Private _slotAspect As Single = 1.5F
    Private _slotSpacing As Single = 2.0F

    ''' <summary>格子內容(放入/清除/旋轉/填滿)改變時發生。</summary>
    Public Event SlotsChanged()

    Public Sub New()
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.OptimizedDoubleBuffer Or
                    ControlStyles.UserPaint Or
                    ControlStyles.ResizeRedraw, True)
        Me.BackColor = Color.FromArgb(230, 230, 230)
        _doc.DocumentName = "ScreenPrintImage"
        AddHandler _doc.PrintPage, AddressOf OnPrintPage
        BuildMenu()
        RebuildSlots()
    End Sub

    ' ===== 右鍵選單 =====

    Private Sub BuildMenu()
        _menu = New ContextMenuStrip()
        _miClear = New ToolStripMenuItem("全部清除")
        _miRotate = New ToolStripMenuItem("向右旋轉 90°")
        _miFill = New ToolStripMenuItem("填滿 / 完整 切換")
        _miPreview = New ToolStripMenuItem("預覽")
        _miPrint = New ToolStripMenuItem("列印")
        _miSelectPrinter = New ToolStripMenuItem("選擇印表機")
        _menu.Items.AddRange(New ToolStripItem() {
            _miClear, New ToolStripSeparator(),
            _miRotate, _miFill, New ToolStripSeparator(),
            _miPreview, _miPrint, _miSelectPrinter})
        AddHandler _miClear.Click, AddressOf OnMenuClear
        AddHandler _miRotate.Click, AddressOf OnMenuRotate
        AddHandler _miFill.Click, AddressOf OnMenuFill
        AddHandler _miPreview.Click, AddressOf OnMenuPreview
        AddHandler _miPrint.Click, AddressOf OnMenuPrint
        AddHandler _miSelectPrinter.Click, AddressOf OnMenuSelectPrinter
    End Sub

    Private Sub ShowSlotMenu(ByVal slot As SlotBox, ByVal screenPt As Point)
        _menuSlot = slot
        Dim hasFile As Boolean = (slot IsNot Nothing AndAlso Not String.IsNullOrEmpty(slot.FileName))
        _miClear.Enabled = (FilledCount > 0)
        _miRotate.Enabled = hasFile
        _miFill.Enabled = hasFile
        _menu.Show(screenPt)
    End Sub

    Private Sub OnMenuClear(sender As Object, e As EventArgs)
        Clear()
    End Sub

    Private Sub OnMenuRotate(sender As Object, e As EventArgs)
        If _menuSlot IsNot Nothing Then _menuSlot.RotateRight()
    End Sub

    Private Sub OnMenuFill(sender As Object, e As EventArgs)
        If _menuSlot IsNot Nothing Then _menuSlot.ToggleFill()
    End Sub

    Private Sub OnMenuPreview(sender As Object, e As EventArgs)
        Print(True)
    End Sub

    Private Sub OnMenuPrint(sender As Object, e As EventArgs)
        Print(False)
    End Sub

    Private Sub OnMenuSelectPrinter(sender As Object, e As EventArgs)
        SelectPrinter()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Right Then
            ShowSlotMenu(Nothing, Me.PointToScreen(e.Location))
        End If
    End Sub

    ' ===== 屬性 =====

    <Category("ScreenPrintImage"), Description("是否將直式圖片旋轉 90° 以橫向顯示/列印。"), DefaultValue(False)>
    Public Property RotatePortraitToLandscape As Boolean
        Get
            Return _rotatePortrait
        End Get
        Set(value As Boolean)
            If _rotatePortrait <> value Then
                _rotatePortrait = value
                For Each s As SlotBox In _slots
                    s.RefreshDisplay()
                Next
            End If
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("上邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginTop As Single
        Get
            Return _marginTop
        End Get
        Set(value As Single)
            _marginTop = Math.Max(0, value) : RebuildSlots()
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("下邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginBottom As Single
        Get
            Return _marginBottom
        End Get
        Set(value As Single)
            _marginBottom = Math.Max(0, value) : RebuildSlots()
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("左邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginLeft As Single
        Get
            Return _marginLeft
        End Get
        Set(value As Single)
            _marginLeft = Math.Max(0, value) : RebuildSlots()
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("右邊界(mm)。"), DefaultValue(5.0!)>
    Public Property MarginRight As Single
        Get
            Return _marginRight
        End Get
        Set(value As Single)
            _marginRight = Math.Max(0, value) : RebuildSlots()
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("每列圖片張數。"), DefaultValue(2)>
    Public Property RowCount As Integer
        Get
            Return _rowCount
        End Get
        Set(value As Integer)
            value = Math.Max(1, value)
            If _rowCount <> value Then
                _rowCount = value : RebuildSlots()
            End If
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("空格子的目標長寬比(寬÷高),決定列數。"), DefaultValue(1.5!)>
    Public Property SlotAspectRatio As Single
        Get
            Return _slotAspect
        End Get
        Set(value As Single)
            If value < 0.1F Then value = 0.1F
            If _slotAspect <> value Then
                _slotAspect = value : RebuildSlots()
            End If
        End Set
    End Property

    <Category("ScreenPrintImage"), Description("格子之間的間距(mm)。"), DefaultValue(2.0!)>
    Public Property SlotSpacing As Single
        Get
            Return _slotSpacing
        End Get
        Set(value As Single)
            _slotSpacing = Math.Max(0, value) : RebuildSlots()
        End Set
    End Property

    <Browsable(False)>
    Public ReadOnly Property SlotCount As Integer
        Get
            Return _slots.Count
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property FilledCount As Integer
        Get
            Dim n As Integer = 0
            For Each s As SlotBox In _slots
                If Not String.IsNullOrEmpty(s.FileName) Then n += 1
            Next
            Return n
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property StatusText As String
        Get
            Return FilledCount.ToString() & " / " & _slots.Count.ToString()
        End Get
    End Property

    <Browsable(False)>
    Public Property PrinterName As String
        Get
            Return _doc.PrinterSettings.PrinterName
        End Get
        Set(value As String)
            If Not String.IsNullOrEmpty(value) Then _doc.PrinterSettings.PrinterName = value
        End Set
    End Property

    <Browsable(False)>
    Public ReadOnly Property IsPrinterValid As Boolean
        Get
            Return _doc.PrinterSettings.IsValid
        End Get
    End Property

    ' ===== 公開方法 =====

    ''' <summary>清除所有格子的圖片。</summary>
    Public Sub Clear()
        For Each s As SlotBox In _slots
            s.ClearFile()
        Next
    End Sub

    ''' <summary>清除指定索引格子的圖片。</summary>
    Public Sub ClearSlot(ByVal index As Integer)
        If index >= 0 AndAlso index < _slots.Count Then _slots(index).ClearFile()
    End Sub

    ''' <summary>以程式將圖片放入指定格子。</summary>
    Public Sub SetSlotFile(ByVal index As Integer, ByVal fileName As String)
        If index >= 0 AndAlso index < _slots.Count Then _slots(index).SetFile(fileName)
    End Sub

    ''' <summary>將圖片依序填入尚未有圖的空格子。</summary>
    Public Sub AutoFill(ByVal files() As String)
        If files Is Nothing Then Return
        Dim fi As Integer = 0
        For Each s As SlotBox In _slots
            If fi >= files.Length Then Exit For
            If String.IsNullOrEmpty(s.FileName) Then
                s.SetFile(files(fi))
                fi += 1
            End If
        Next
    End Sub

    ' 從指定格子起,依序填入多個檔案(多張拖放用)
    Private Sub FillFromSlot(ByVal slot As SlotBox, ByVal files() As String)
        Dim idx As Integer = _slots.IndexOf(slot)
        If idx < 0 OrElse files Is Nothing Then Return
        For Each f As String In files
            If idx >= _slots.Count Then Exit For
            _slots(idx).SetFile(f)
            idx += 1
        Next
    End Sub

    ''' <summary>取得各格子的檔名(空格子為空字串),長度 = SlotCount。</summary>
    Public Function GetFileNames() As String()
        Dim arr(_slots.Count - 1) As String
        For i As Integer = 0 To _slots.Count - 1
            arr(i) = If(_slots(i).FileName, String.Empty)
        Next
        Return arr
    End Function

    ''' <summary>顯示 PrintDialog 選擇印表機(獨立於列印)。</summary>
    Public Function SelectPrinter() As Boolean
        Using pd As New PrintDialog()
            pd.Document = _doc
            pd.UseEXDialog = True
            If pd.ShowDialog() = DialogResult.OK Then Return True
        End Using
        Return False
    End Function

    ''' <summary>列印或預覽。Preview=True 只出預覽視窗;False 直接以目前印表機列印。</summary>
    Public Function Print(ByVal Preview As Boolean) As Boolean
        ApplyMargins()
        If Preview Then
            Using dlg As New PrintPreviewDialog()
                dlg.Document = _doc
                dlg.WindowState = FormWindowState.Maximized
                dlg.ShowDialog()
            End Using
        Else
            _doc.Print()
        End If
        Return True
    End Function

    ''' <summary>將目前排版匯出為圖片檔(依副檔名 png/jpg/bmp)。</summary>
    Public Function ExportImage(ByVal path As String, Optional ByVal dpi As Integer = 150) As Boolean
        Try
            Dim ppm As Single = dpi / 25.4F
            Dim pw As Integer = CInt(A4W_MM * ppm)
            Dim ph As Integer = CInt(A4H_MM * ppm)
            Using bmp As New Bitmap(pw, ph)
                bmp.SetResolution(dpi, dpi)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.White)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality
                    g.SmoothingMode = SmoothingMode.HighQuality
                    Dim printable As New RectangleF(_marginLeft * ppm, _marginTop * ppm,
                                                    (A4W_MM - _marginLeft - _marginRight) * ppm,
                                                    (A4H_MM - _marginTop - _marginBottom) * ppm)
                    RenderSlots(g, printable, ppm)
                End Using
                bmp.Save(path, FormatFromExt(path))
            End Using
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>將目前排版匯出為 PDF(透過「Microsoft Print to PDF」印表機)。</summary>
    Public Function ExportPdf(ByVal path As String) As Boolean
        Dim prev As String = _doc.PrinterSettings.PrinterName
        Try
            _doc.PrinterSettings.PrinterName = "Microsoft Print to PDF"
            If Not _doc.PrinterSettings.IsValid Then Return False
            ApplyMargins()
            _doc.PrinterSettings.PrintToFile = True
            _doc.PrinterSettings.PrintFileName = path
            _doc.Print()
            Return True
        Catch
            Return False
        Finally
            _doc.PrinterSettings.PrintToFile = False
            _doc.PrinterSettings.PrintFileName = Nothing
            If Not String.IsNullOrEmpty(prev) Then
                Try
                    _doc.PrinterSettings.PrinterName = prev
                Catch
                End Try
            End If
        End Try
    End Function

    ' ===== 版面計算 =====

    Private Sub ApplyMargins()
        _doc.DefaultPageSettings.Margins = New Margins(
            MmTo100(_marginLeft), MmTo100(_marginRight), MmTo100(_marginTop), MmTo100(_marginBottom))
        _doc.DefaultPageSettings.Landscape = False
    End Sub

    ' 各格子相對「可印區左上角」的矩形(mm)
    Private Function ComputeSlotRectsMm() As List(Of RectangleF)
        Dim list As New List(Of RectangleF)()
        Dim pw As Single = A4W_MM - _marginLeft - _marginRight
        Dim ph As Single = A4H_MM - _marginTop - _marginBottom
        If pw <= 0 OrElse ph <= 0 Then Return list

        Dim cols As Integer = Math.Max(1, _rowCount)
        Dim sp As Single = _slotSpacing
        Dim cellW As Single = (pw - (cols - 1) * sp) / cols
        If cellW <= 0 Then cellW = pw / cols

        Dim baseH As Single = cellW / _slotAspect
        Dim rows As Integer = Math.Max(1, CInt(Math.Floor((ph + sp) / (baseH + sp))))
        Dim cellH As Single = (ph - (rows - 1) * sp) / rows

        For r As Integer = 0 To rows - 1
            For c As Integer = 0 To cols - 1
                list.Add(New RectangleF(c * (cellW + sp), r * (cellH + sp), cellW, cellH))
            Next
        Next
        Return list
    End Function

    Private Function PageRectPx(ByRef scale As Single) As RectangleF
        Const pad As Integer = 10
        Dim availW As Integer = Math.Max(1, Me.ClientSize.Width - pad * 2)
        Dim availH As Integer = Math.Max(1, Me.ClientSize.Height - pad * 2)
        scale = Math.Min(availW / A4W_MM, availH / A4H_MM)
        Dim pgW As Single = A4W_MM * scale
        Dim pgH As Single = A4H_MM * scale
        Return New RectangleF((Me.ClientSize.Width - pgW) / 2, (Me.ClientSize.Height - pgH) / 2, pgW, pgH)
    End Function

    Private Sub RebuildSlots()
        Dim rects As List(Of RectangleF) = ComputeSlotRectsMm()

        If rects.Count <> _slots.Count Then
            Dim old As New List(Of String)()
            For Each s As SlotBox In _slots
                old.Add(s.FileName)
            Next
            For Each s As SlotBox In _slots
                Me.Controls.Remove(s)
                s.Dispose()
            Next
            _slots.Clear()

            For i As Integer = 0 To rects.Count - 1
                Dim s As New SlotBox(Me)
                s.SetIndex(i + 1)
                _slots.Add(s)
                Me.Controls.Add(s)
                If i < old.Count AndAlso Not String.IsNullOrEmpty(old(i)) Then s.SetFile(old(i))
            Next
        End If

        RepositionSlots()
        Me.Invalidate()
    End Sub

    Private Sub RepositionSlots()
        If _slots.Count = 0 Then Return
        Dim scale As Single
        Dim pr As RectangleF = PageRectPx(scale)
        Dim plLeft As Single = pr.X + _marginLeft * scale
        Dim plTop As Single = pr.Y + _marginTop * scale

        Dim rects As List(Of RectangleF) = ComputeSlotRectsMm()
        Dim n As Integer = Math.Min(rects.Count, _slots.Count)
        For i As Integer = 0 To n - 1
            Dim r As RectangleF = rects(i)
            _slots(i).SetBounds(
                CInt(plLeft + r.X * scale),
                CInt(plTop + r.Y * scale),
                Math.Max(1, CInt(r.Width * scale)),
                Math.Max(1, CInt(r.Height * scale)))
        Next
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        RepositionSlots()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        Dim scale As Single
        Dim pr As RectangleF = PageRectPx(scale)

        Using sh As New SolidBrush(Color.FromArgb(40, 0, 0, 0))
            g.FillRectangle(sh, pr.X + 3, pr.Y + 3, pr.Width, pr.Height)
        End Using
        g.FillRectangle(Brushes.White, pr.X, pr.Y, pr.Width, pr.Height)
        Using pen As New Pen(Color.FromArgb(180, 180, 180))
            g.DrawRectangle(pen, pr.X, pr.Y, pr.Width, pr.Height)
        End Using
        Using pen As New Pen(Color.FromArgb(210, 210, 210))
            pen.DashStyle = DashStyle.Dash
            g.DrawRectangle(pen, pr.X + _marginLeft * scale, pr.Y + _marginTop * scale,
                            (A4W_MM - _marginLeft - _marginRight) * scale,
                            (A4H_MM - _marginTop - _marginBottom) * scale)
        End Using
    End Sub

    ' ===== 列印/匯出共用繪製 =====

    Private Sub OnPrintPage(sender As Object, e As PrintPageEventArgs)
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality
        Dim b As Rectangle = e.MarginBounds
        RenderSlots(e.Graphics, New RectangleF(b.Left, b.Top, b.Width, b.Height), 100.0F / 25.4F)
        e.HasMorePages = False
    End Sub

    ' 將各格子的圖片畫到目標 Graphics(printable 為可印區左上角與大小,mmToUnit 為 mm→目標單位)
    Private Sub RenderSlots(ByVal g As Graphics, ByVal printable As RectangleF, ByVal mmToUnit As Single)
        Dim rects As List(Of RectangleF) = ComputeSlotRectsMm()
        Dim n As Integer = Math.Min(rects.Count, _slots.Count)
        For i As Integer = 0 To n - 1
            Dim slot As SlotBox = _slots(i)
            If String.IsNullOrEmpty(slot.FileName) Then Continue For
            Dim r As RectangleF = rects(i)
            Dim cell As New RectangleF(printable.Left + r.X * mmToUnit, printable.Top + r.Y * mmToUnit,
                                       r.Width * mmToUnit, r.Height * mmToUnit)
            Dim img As Image = LoadRotated(slot.FileName, slot.RotateSteps)
            If img Is Nothing Then Continue For
            Dim dest As RectangleF, src As RectangleF
            ComputeDrawRects(img.Width, img.Height, cell, slot.FillMode, dest, src)
            g.DrawImage(img, Rectangle.Round(dest), Rectangle.Round(src), GraphicsUnit.Pixel)
            img.Dispose()
        Next
    End Sub

    Friend Sub RaiseSlotsChanged()
        RaiseEvent SlotsChanged()
    End Sub

    ' ===== 共用工具 =====

    Private Shared Function MmTo100(ByVal mm As Single) As Integer
        Return CInt(Math.Round(mm / 25.4F * 100.0F))
    End Function

    Private Shared Function FormatFromExt(ByVal path As String) As ImageFormat
        Dim ext As String = IO.Path.GetExtension(path).ToLowerInvariant()
        Select Case ext
            Case ".jpg", ".jpeg" : Return ImageFormat.Jpeg
            Case ".bmp" : Return ImageFormat.Bmp
            Case ".gif" : Return ImageFormat.Gif
            Case ".tif", ".tiff" : Return ImageFormat.Tiff
            Case Else : Return ImageFormat.Png
        End Select
    End Function

    ' 讀取圖片並套用「直式轉橫」與「每格額外旋轉」
    Friend Function LoadRotated(ByVal path As String, ByVal rotateSteps As Integer) As Bitmap
        Dim bmp As Bitmap = LoadImageNoLock(path)
        If bmp Is Nothing Then Return Nothing
        If _rotatePortrait AndAlso bmp.Height > bmp.Width Then bmp.RotateFlip(RotateFlipType.Rotate90FlipNone)
        Select Case ((rotateSteps Mod 4) + 4) Mod 4
            Case 1 : bmp.RotateFlip(RotateFlipType.Rotate90FlipNone)
            Case 2 : bmp.RotateFlip(RotateFlipType.Rotate180FlipNone)
            Case 3 : bmp.RotateFlip(RotateFlipType.Rotate270FlipNone)
        End Select
        Return bmp
    End Function

    ' 依填滿(裁切)或完整(留白)計算目標與來源矩形
    Friend Shared Sub ComputeDrawRects(ByVal imgW As Single, ByVal imgH As Single, ByVal container As RectangleF,
                                       ByVal fill As Boolean, ByRef dest As RectangleF, ByRef src As RectangleF)
        If imgW <= 0 OrElse imgH <= 0 Then
            dest = container : src = New RectangleF(0, 0, Math.Max(1, imgW), Math.Max(1, imgH)) : Return
        End If
        If fill Then
            Dim scale As Single = Math.Max(container.Width / imgW, container.Height / imgH)
            Dim sw As Single = container.Width / scale
            Dim sh As Single = container.Height / scale
            src = New RectangleF((imgW - sw) / 2, (imgH - sh) / 2, sw, sh)
            dest = container
        Else
            Dim scale As Single = Math.Min(container.Width / imgW, container.Height / imgH)
            Dim w As Single = imgW * scale
            Dim h As Single = imgH * scale
            dest = New RectangleF(container.X + (container.Width - w) / 2, container.Y + (container.Height - h) / 2, w, h)
            src = New RectangleF(0, 0, imgW, imgH)
        End If
    End Sub

    Friend Shared Function LoadImageNoLock(ByVal path As String) As Bitmap
        Try
            Dim bytes() As Byte = File.ReadAllBytes(path)
            Using ms As New MemoryStream(bytes)
                Using tmp As Image = Image.FromStream(ms)
                    Return New Bitmap(tmp)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _doc.Dispose()
            If _menu IsNot Nothing Then _menu.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' ================= 巢狀:單一格子容器 =================
    Private Class SlotBox
        Inherits Control

        Private ReadOnly _owner As ScreenPrintImage
        Private _fileName As String = Nothing
        Private _display As Image = Nothing
        Private _index As Integer = 0
        Private _rotateSteps As Integer = 0
        Private _fill As Boolean = False          ' False=完整(留白) True=填滿(裁切)
        Private _hover As Boolean = False
        Private _dragHover As Boolean = False
        Private _pressed As Boolean = False
        Private _pressPt As Point = Point.Empty

        Private Const CloseSize As Integer = 16
        Private Const SlotRefFormat As String = "ScreenPrintSlotRef"
        Private Const MaxDisplaySide As Integer = 1200

        Public Sub New(ByVal owner As ScreenPrintImage)
            _owner = owner
            Me.AllowDrop = True
            Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or
                        ControlStyles.OptimizedDoubleBuffer Or
                        ControlStyles.UserPaint Or
                        ControlStyles.ResizeRedraw, True)
            Me.BackColor = Color.White
        End Sub

        Public ReadOnly Property FileName As String
            Get
                Return _fileName
            End Get
        End Property

        Public ReadOnly Property RotateSteps As Integer
            Get
                Return _rotateSteps
            End Get
        End Property

        Public ReadOnly Property FillMode As Boolean
            Get
                Return _fill
            End Get
        End Property

        Public Sub SetIndex(ByVal i As Integer)
            _index = i
        End Sub

        Public Sub RefreshDisplay()
            RegenerateDisplay()
            Invalidate()
        End Sub

        ' 放入新圖(重設旋轉/填滿)
        Public Sub SetFile(ByVal fn As String)
            _fileName = fn
            _rotateSteps = 0
            _fill = False
            RegenerateDisplay()
            Invalidate()
            _owner.RaiseSlotsChanged()
        End Sub

        Public Sub ClearFile()
            Dim had As Boolean = Not String.IsNullOrEmpty(_fileName)
            _fileName = Nothing
            _rotateSteps = 0
            _fill = False
            DisposeDisplay()
            Invalidate()
            If had Then _owner.RaiseSlotsChanged()
        End Sub

        Public Sub RotateRight()
            If String.IsNullOrEmpty(_fileName) Then Return
            _rotateSteps = (_rotateSteps + 1) Mod 4
            RegenerateDisplay()
            Invalidate()
            _owner.RaiseSlotsChanged()
        End Sub

        Public Sub ToggleFill()
            If String.IsNullOrEmpty(_fileName) Then Return
            _fill = Not _fill
            Invalidate()
            _owner.RaiseSlotsChanged()
        End Sub

        ' 指定完整狀態(互換用)
        Private Sub AssignState(ByVal fn As String, ByVal steps As Integer, ByVal fill As Boolean)
            _fileName = fn
            _rotateSteps = steps
            _fill = fill
            RegenerateDisplay()
            Invalidate()
            _owner.RaiseSlotsChanged()
        End Sub

        Private Sub DisposeDisplay()
            If _display IsNot Nothing Then
                _display.Dispose()
                _display = Nothing
            End If
        End Sub

        Private Sub RegenerateDisplay()
            DisposeDisplay()
            If String.IsNullOrEmpty(_fileName) Then Return
            Dim src As Bitmap = _owner.LoadRotated(_fileName, _rotateSteps)
            If src Is Nothing Then Return
            Dim longest As Integer = Math.Max(src.Width, src.Height)
            If longest > MaxDisplaySide Then
                Dim sc As Single = MaxDisplaySide / CSng(longest)
                Dim bw As Integer = Math.Max(1, CInt(src.Width * sc))
                Dim bh As Integer = Math.Max(1, CInt(src.Height * sc))
                Dim bmp As New Bitmap(bw, bh)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(src, New Rectangle(0, 0, bw, bh))
                End Using
                src.Dispose()
                _display = bmp
            Else
                _display = src
            End If
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g As Graphics = e.Graphics
            g.Clear(Color.White)
            g.InterpolationMode = InterpolationMode.HighQualityBicubic

            Dim filled As Boolean = Not String.IsNullOrEmpty(_fileName)

            If _display IsNot Nothing Then
                Dim container As New RectangleF(3, 3, Math.Max(1, Me.Width - 6), Math.Max(1, Me.Height - 6))
                Dim dest As RectangleF, src As RectangleF
                ComputeDrawRects(_display.Width, _display.Height, container, _fill, dest, src)
                g.DrawImage(_display, Rectangle.Round(dest), Rectangle.Round(src), GraphicsUnit.Pixel)
            ElseIf Not filled Then
                ' 空格提示:序號 + 說明
                Using bTxt As New SolidBrush(Color.FromArgb(200, 200, 200))
                    Dim fs As Single = CSng(Math.Max(12, Math.Min(Me.Width, Me.Height) \ 4))
                    Using fBig As New Font(Me.Font.FontFamily, fs, FontStyle.Bold)
                        Dim num As String = _index.ToString()
                        Dim sz As SizeF = g.MeasureString(num, fBig)
                        g.DrawString(num, fBig, bTxt, (Me.Width - sz.Width) / 2, (Me.Height - sz.Height) / 2 - 8)
                    End Using
                    Using sf As New StringFormat()
                        sf.Alignment = StringAlignment.Center
                        g.DrawString("拖曳圖片至此", Me.Font, bTxt, New RectangleF(0, Me.Height - 22, Me.Width, 18), sf)
                    End Using
                End Using
            End If

            ' 邊框
            Using pen As New Pen(If(filled, Color.FromArgb(120, 170, 230), Color.FromArgb(170, 170, 170)))
                If Not filled Then pen.DashStyle = DashStyle.Dash
                g.DrawRectangle(pen, 0, 0, Me.Width - 1, Me.Height - 1)
            End Using

            ' 拖曳目標高亮
            If _dragHover Then
                Using hb As New SolidBrush(Color.FromArgb(60, 120, 170, 230))
                    g.FillRectangle(hb, 1, 1, Me.Width - 2, Me.Height - 2)
                End Using
                Using hp As New Pen(Color.FromArgb(90, 150, 220), 2)
                    g.DrawRectangle(hp, 1, 1, Me.Width - 3, Me.Height - 3)
                End Using
            End If

            ' 已放:右上小叉(hover 顯示)
            If filled AndAlso _hover Then
                Dim rc As Rectangle = CloseRect()
                Using b As New SolidBrush(Color.FromArgb(200, 60, 60))
                    g.FillRectangle(b, rc)
                End Using
                Using p As New Pen(Color.White, 2)
                    g.DrawLine(p, rc.Left + 4, rc.Top + 4, rc.Right - 4, rc.Bottom - 4)
                    g.DrawLine(p, rc.Right - 4, rc.Top + 4, rc.Left + 4, rc.Bottom - 4)
                End Using
            End If
        End Sub

        Private Function CloseRect() As Rectangle
            Return New Rectangle(Me.Width - CloseSize - 2, 2, CloseSize, CloseSize)
        End Function

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Right Then
                _owner.ShowSlotMenuFor(Me, Me.PointToScreen(e.Location))
                Return
            End If
            If String.IsNullOrEmpty(_fileName) Then Return
            If e.Button = MouseButtons.Left Then
                If CloseRect().Contains(e.Location) Then
                    ClearFile()
                Else
                    _pressed = True
                    _pressPt = e.Location
                End If
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _pressed AndAlso (e.Button And MouseButtons.Left) = MouseButtons.Left AndAlso Not String.IsNullOrEmpty(_fileName) Then
                If Math.Abs(e.X - _pressPt.X) >= SystemInformation.DragSize.Width OrElse
                   Math.Abs(e.Y - _pressPt.Y) >= SystemInformation.DragSize.Height Then
                    StartInternalDrag()
                End If
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
        End Sub

        Private Sub StartInternalDrag()
            _pressed = False
            Dim data As New DataObject()
            data.SetData(SlotRefFormat, Me)
            data.SetData(MediaDragFormat, _fileName)
            Me.DoDragDrop(data, DragDropEffects.Move)
        End Sub

        Protected Overrides Sub OnDragEnter(e As DragEventArgs)
            MyBase.OnDragEnter(e)
            If e.Data.GetDataPresent(SlotRefFormat) Then
                e.Effect = DragDropEffects.Move
                _dragHover = True : Invalidate()
            ElseIf e.Data.GetDataPresent(MediaDragFormat) OrElse e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
                _dragHover = True : Invalidate()
            Else
                e.Effect = DragDropEffects.None
            End If
        End Sub

        Protected Overrides Sub OnDragLeave(e As EventArgs)
            MyBase.OnDragLeave(e)
            _dragHover = False : Invalidate()
        End Sub

        Protected Overrides Sub OnDragDrop(e As DragEventArgs)
            MyBase.OnDragDrop(e)
            _dragHover = False : Invalidate()

            ' 格子間互換/搬移
            If e.Data.GetDataPresent(SlotRefFormat) Then
                Dim src As SlotBox = TryCast(e.Data.GetData(SlotRefFormat), SlotBox)
                If src IsNot Nothing AndAlso src IsNot Me Then SwapWith(src)
                Return
            End If

            ' 多張檔案:從本格起依序填入
            If e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim files As String() = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If files IsNot Nothing AndAlso files.Length > 1 Then
                    _owner.FillFromSlotPublic(Me, files)
                    Return
                End If
            End If

            ' 單張
            Dim fn As String = Nothing
            If e.Data.GetDataPresent(MediaDragFormat) Then
                fn = TryCast(e.Data.GetData(MediaDragFormat), String)
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim files As String() = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If files IsNot Nothing AndAlso files.Length > 0 Then fn = files(0)
            End If
            If Not String.IsNullOrEmpty(fn) Then SetFile(fn)
        End Sub

        Private Sub SwapWith(ByVal other As SlotBox)
            Dim f1 As String = Me._fileName, f2 As String = other._fileName
            Dim r1 As Integer = Me._rotateSteps, r2 As Integer = other._rotateSteps
            Dim m1 As Boolean = Me._fill, m2 As Boolean = other._fill
            Me.AssignState(f2, r2, m2)
            other.AssignState(f1, r1, m1)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then DisposeDisplay()
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ' 供巢狀 SlotBox 呼叫(避免直接以 Private 型別暴露介面)
    Private Sub ShowSlotMenuFor(ByVal slot As SlotBox, ByVal screenPt As Point)
        ShowSlotMenu(slot, screenPt)
    End Sub

    Private Sub FillFromSlotPublic(ByVal slot As SlotBox, ByVal files() As String)
        FillFromSlot(slot, files)
    End Sub
End Class

End Namespace
