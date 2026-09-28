Option Strict Off
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 媒體項目複合控制項:MediaViewerControl(圖片/影片)填滿上方,
''' 下方 Panel1 內含可顯示/隱藏的 ImageCheckBox1 與可給值的 RatingControl1。
''' 版面由設計工具(MediaItem.Designer.vb)定義,此處僅提供對外屬性與事件。
''' </summary>
Namespace Global.Aqua

<DefaultEvent("MediaClicked")>
Partial Public Class MediaItem

    ''' <summary>核取方塊勾選狀態改變。</summary>
    <Category("Media"), Description("核取方塊勾選狀態改變時發生。")>
    Public Event CheckedChanged As EventHandler
    ''' <summary>評分改變,帶出新的星數。</summary>
    <Category("Media"), Description("評分改變時發生,帶出新的星數。")>
    Public Event RatingChanged(ByVal newRating As Integer)
    ''' <summary>點擊媒體。</summary>
    <Category("Media"), Description("點擊媒體時發生。")>
    Public Event MediaClicked As EventHandler(Of MediaClickedEventArgs)
    ''' <summary>拖放媒體完成(來源檔名)。</summary>
    <Category("Media"), Description("拖放媒體完成時發生,帶出來源檔名。")>
    Public Event DropMedia(ByVal FileName As String)

    ' 選取邊框寬度與邊框到媒體之間的留白間距
    Friend Const SelectionBorderWidth As Integer = 2
    Friend Const SelectionGap As Integer = 3
    Friend Const BottomPanelHeight As Integer = 27   ' pnlBottom 在 MediaItem.Designer.vb 的高度
    Private _selected As Boolean = False

    Public Sub New()
        InitializeComponent()

        ' 保留邊框帶 + 留白間距(子控制項內縮),選取時在最外緣畫淡藍框,與媒體之間保留 SelectionGap 留白
        Me.Padding = New Padding(SelectionBorderWidth + SelectionGap)
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True)

        ' 事件轉發
        AddHandler ImageCheckBox1.CheckedChanged, AddressOf OnChildCheckedChanged
        AddHandler RatingControl1.RatingChanged, AddressOf OnChildRatingChanged
        AddHandler MediaViewerControl1.MediaClicked, AddressOf OnChildMediaClicked
        AddHandler MediaViewerControl1.DropMedia, AddressOf OnChildDropMedia
        AddHandler MediaViewerControl1.MouseDown, AddressOf OnChildMouseDown
        AddHandler MediaViewerControl1.DoubleClick, AddressOf OnChildDoubleClick
        AddHandler MediaViewerControl1.DragCompleted, AddressOf OnChildDragCompleted
        AddHandler MediaViewerControl1.Paint, AddressOf OnViewerPaint

        UpdateBottomVisibility()
    End Sub

    ' ===== 媒體 =====

    ' MediaList 延遲載入時先記下的檔名:尚未實際載入前,FileName 仍回傳它(VB6 的 FileName 一加入就有值)
    Private _deferredFileName As String = Nothing

    ''' <summary>要顯示的媒體檔(圖片或影片),設定後即載入。</summary>
    <Category("Media"), Description("要顯示的媒體檔(圖片或影片),設定後即載入。"), Browsable(True)>
    Public Property FileName As String
        Get
            If _deferredFileName IsNot Nothing Then Return _deferredFileName
            Return MediaViewerControl1.FileName
        End Get
        Set(value As String)
            _deferredFileName = Nothing
            MediaViewerControl1.FileName = value
            If _marked Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    ''' <summary>MediaList 延遲載入用:記下檔名但先不載入。</summary>
    Friend Sub SetDeferredFileName(ByVal fileName As String)
        _deferredFileName = fileName
    End Sub

    ''' <summary>MediaList 虛擬化用:項目捲出畫面時釋放它的所有視窗(本身、子控制項、提示框),
    ''' 資料與縮圖都保留;再加回畫面時由 WinForms 重新建立。呼叫前先從父控制項移除。</summary>
    Friend Sub ReleaseWindow()
        MediaViewerControl1.StopPlayback()   ' 播放器綁在原生視窗上
        If _toolTip IsNot Nothing Then       ' 提示框本身也是一個視窗
            _toolTip.Dispose()
            _toolTip = Nothing
        End If
        If IsHandleCreated Then DestroyHandle()
    End Sub

    ''' <summary>MediaList 用:縮圖在背景產生(見 ThumbnailLoader)。</summary>
    Friend Sub UseBackgroundLoading()
        MediaViewerControl1.LoadAsynchronously = True
    End Sub

    ''' <summary>MediaList 用:捲出畫面時釋放縮圖(檔名等保留)。</summary>
    Friend Sub UnloadMedia()
        MediaViewerControl1.UnloadImage()
    End Sub

    ''' <summary>MediaList 用:再捲進畫面時取回縮圖(最近用過的由快取直接給)。</summary>
    Friend Sub ReloadMedia()
        MediaViewerControl1.ReloadIfUnloaded()
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If _toolTip Is Nothing AndAlso _toolTipText.Length > 0 Then ApplyToolTip()   ' ReleaseWindow 釋放過
    End Sub

    ''' <summary>是否為選取狀態(選取時顯示淡藍色邊框)。</summary>
    <Category("Media"), Description("是否為選取狀態(選取時顯示淡藍色邊框)。"), DefaultValue(False)>
    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            If _selected <> value Then
                _selected = value
                Me.Invalidate()
            End If
        End Set
    End Property

    ''' <summary>媒體實際解析度。</summary>
    <Category("Media"), Description("媒體實際解析度(唯讀)。"), Browsable(False)>
    Public ReadOnly Property RealSize As Size
        Get
            Return MediaViewerControl1.RealSize
        End Get
    End Property

    ' ===== 核取方塊(可顯示/隱藏) =====

    ''' <summary>是否顯示核取方塊。</summary>
    <Category("Media"), Description("是否顯示核取方塊(ImageCheckBox1)。"), DefaultValue(True)>
    Public Property ShowCheckBox As Boolean
        Get
            Return _showCheckBox
        End Get
        Set(value As Boolean)
            _showCheckBox = value
            ImageCheckBox1.Visible = value
            UpdateBottomVisibility()
        End Set
    End Property

    ' 以欄位記住顯示設定:子控制項的 Visible 在父面板(或項目本身)隱藏時一律讀回 False,
    ' 用它判斷會讓底部面板一旦隱藏就再也顯示不回來。
    Private _showCheckBox As Boolean = True
    Private _showRating As Boolean = True

    ''' <summary>核取方塊是否勾選。</summary>
    <Category("Media"), Description("核取方塊是否勾選。"), DefaultValue(False)>
    Public Property Checked As Boolean
        Get
            Return ImageCheckBox1.Checked
        End Get
        Set(value As Boolean)
            ImageCheckBox1.Checked = value
        End Set
    End Property

    ''' <summary>核取方塊文字。</summary>
    <Category("Media"), Description("核取方塊顯示的文字。")>
    Public Property CheckText As String
        Get
            Return ImageCheckBox1.TextValue
        End Get
        Set(value As String)
            ImageCheckBox1.TextValue = value
        End Set
    End Property

    ' ===== 評分(可給值) =====

    ''' <summary>是否顯示評分控制項。</summary>
    <Category("Media"), Description("是否顯示評分控制項(RatingControl1)。"), DefaultValue(True)>
    Public Property ShowRating As Boolean
        Get
            Return _showRating
        End Get
        Set(value As Boolean)
            _showRating = value
            RatingControl1.Visible = value
            UpdateBottomVisibility()
        End Set
    End Property

    ''' <summary>核取方塊與評分皆不顯示時,隱藏底部面板(讓媒體區佔滿)。</summary>
    Private Sub UpdateBottomVisibility()
        pnlBottom.Visible = _showCheckBox OrElse _showRating
    End Sub

    ''' <summary>評分值(0~5),可直接給值。</summary>
    <Category("Media"), Description("評分值(0~5),可直接給值。"), DefaultValue(0)>
    Public Property Rating As Integer
        Get
            Return RatingControl1.Rating
        End Get
        Set(value As Integer)
            RatingControl1.Rating = value
        End Set
    End Property

    ''' <summary>清空媒體顯示。</summary>
    Public Sub Clear()
        MediaViewerControl1.Clear()
    End Sub

    ' ===== VB6 Aqua.MediaItem 相容 =====

    ''' <summary>標記(Marked)改變時發生(VB6: MarkedChanged),程式設定也會觸發。</summary>
    Public Event MarkedChanged As EventHandler
    ''' <summary>在媒體上按下滑鼠(VB6: ItemMouseDown),座標相對於媒體區。</summary>
    Public Event MediaMouseDown As MouseEventHandler
    ''' <summary>雙擊媒體(VB6: ItemDblClick)。</summary>
    Public Event MediaDoubleClick As EventHandler
    ''' <summary>把媒體拖出後結束時發生,帶出拖放結果(VB6: ItemCompleteDrag)。</summary>
    Public Event DragCompleted(ByVal effect As DragDropEffects)

    Private _marked As Boolean = False
    Private _keepChange As Boolean = False
    Private _markImage As Image = Nothing
    Private _markAlignment As ContentAlignment = ContentAlignment.BottomLeft
    Private _markPosition As MediaItemMarkPosition = MediaItemMarkPosition.SnapToPhoto
    Private _borderSize As Integer = SelectionGap
    Private _ratingHiddenByRanking As Boolean = False
    Private _toolTip As ToolTip = Nothing
    Private _toolTipTitle As String = ""
    Private _toolTipText As String = ""

    ''' <summary>是否標記(例如已放入 Dock),標記時在媒體上疊 MarkImage(VB6: Marked)。</summary>
    <Category("Media"), Description("是否標記;標記時在媒體上疊 MarkImage。"), DefaultValue(False)>
    Public Property Marked As Boolean
        Get
            Return _marked
        End Get
        Set(value As Boolean)
            If _marked = value Then Return
            _marked = value
            If Not _keepChange Then MediaViewerControl1.Invalidate()
            RaiseEvent MarkedChanged(Me, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>True 時暫停因 Marked 改變而重繪;設回 False 時補畫一次(VB6: KeepChange)。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property KeepChange As Boolean
        Get
            Return _keepChange
        End Get
        Set(value As Boolean)
            If _keepChange = value Then Return
            _keepChange = value
            If Not value Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    ''' <summary>標記圖;未指定時畫預設的藍底白勾圓點(VB6: MarkImage)。</summary>
    <Category("Media"), Description("標記圖;未指定時畫預設的藍底白勾圓點。")>
    Public Property MarkImage As Image
        Get
            Return _markImage
        End Get
        Set(value As Image)
            _markImage = value
            If _marked Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    Private _markTransparencyKey As Color = Color.White

    ''' <summary>標記圖中當作透明的顏色(VB6: MarkTransparencyKey,預設白色):VB6 以這個顏色挖出
    ''' picMarked 的外形;這裡畫標記圖時把這個顏色設為透明。</summary>
    <Category("Media"), Description("標記圖中當作透明的顏色(預設白色)。"), DefaultValue(GetType(Color), "White")>
    Public Property MarkTransparencyKey As Color
        Get
            Return _markTransparencyKey
        End Get
        Set(value As Color)
            _markTransparencyKey = value
            If _marked Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    ''' <summary>標記圖對齊媒體的哪個位置(VB6: MarkAlignment,數值與 ContentAlignment 相同)。</summary>
    <Category("Media"), Description("標記圖對齊媒體的位置。"), DefaultValue(GetType(ContentAlignment), "BottomLeft")>
    Public Property MarkAlignment As ContentAlignment
        Get
            Return _markAlignment
        End Get
        Set(value As ContentAlignment)
            _markAlignment = value
            If _marked Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    ''' <summary>標記圖貼齊媒體內側,或以媒體邊緣為中心(VB6: MarkPosition)。</summary>
    <Category("Media"), Description("標記圖貼齊媒體內側,或以媒體邊緣為中心。"), DefaultValue(GetType(MediaItemMarkPosition), "SnapToPhoto")>
    Public Property MarkPosition As MediaItemMarkPosition
        Get
            Return _markPosition
        End Get
        Set(value As MediaItemMarkPosition)
            _markPosition = value
            If _marked Then MediaViewerControl1.Invalidate()
        End Set
    End Property

    ''' <summary>評分(VB6: Ranking)。None 隱藏評分列;其他值同 Rating,且會重新顯示被 None 隱藏的評分列。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property Ranking As MediaItemRanking
        Get
            If Not ShowRating Then Return MediaItemRanking.None
            Return CType(Rating, MediaItemRanking)
        End Get
        Set(value As MediaItemRanking)
            If value = MediaItemRanking.None Then
                If ShowRating Then
                    ShowRating = False
                    _ratingHiddenByRanking = True
                End If
            Else
                Rating = CInt(value)
                If _ratingHiddenByRanking Then
                    ShowRating = True
                    _ratingHiddenByRanking = False
                End If
            End If
        End Set
    End Property

    ''' <summary>媒體原始寬度(像素);尚未載入為 0(VB6: ItemWidth)。</summary>
    <Browsable(False)>
    Public ReadOnly Property ItemWidth As Integer
        Get
            Return RealSize.Width
        End Get
    End Property

    ''' <summary>媒體原始高度(像素);尚未載入為 0(VB6: ItemHeight)。</summary>
    <Browsable(False)>
    Public ReadOnly Property ItemHeight As Integer
        Get
            Return RealSize.Height
        End Get
    End Property

    ''' <summary>選取框與媒體之間的留白(像素,VB6: BorderSize)。</summary>
    <Category("Media"), Description("選取框與媒體之間的留白(像素)。"), DefaultValue(SelectionGap)>
    Public Property BorderSize As Integer
        Get
            Return _borderSize
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _borderSize = value Then Return
            _borderSize = value
            Me.Padding = New Padding(SelectionBorderWidth + value)
        End Set
    End Property

    ''' <summary>可否把媒體拖出(VB6: DragItem)。</summary>
    <Category("Media"), Description("可否把媒體拖出。"), DefaultValue(True)>
    Public Property DragItem As Boolean
        Get
            Return MediaViewerControl1.AllowDragOut
        End Get
        Set(value As Boolean)
            MediaViewerControl1.AllowDragOut = value
        End Set
    End Property

    ''' <summary>可否把媒體拖入(VB6: DropItem)。</summary>
    <Category("Media"), Description("可否把媒體拖入。"), DefaultValue(True)>
    Public Property DropItem As Boolean
        Get
            Return MediaViewerControl1.AllowDrop
        End Get
        Set(value As Boolean)
            MediaViewerControl1.AllowDrop = value
        End Set
    End Property

    ''' <summary>提示框標題(VB6 ToolTip 的 Title;MediaList.AddItem 會帶入檔名)。</summary>
    <Category("Media"), Description("滑鼠停在媒體上時的提示框標題。"), DefaultValue("")>
    Public Property ToolTipTitle As String
        Get
            Return _toolTipTitle
        End Get
        Set(value As String)
            _toolTipTitle = If(value, "")
            ApplyToolTip()
        End Set
    End Property

    ''' <summary>提示框內容;空白則不顯示提示(VB6 ToolTip 的 Context)。</summary>
    <Category("Media"), Description("滑鼠停在媒體上時的提示框內容;空白則不顯示。"), DefaultValue("")>
    Public Property ToolTipText As String
        Get
            Return _toolTipText
        End Get
        Set(value As String)
            _toolTipText = If(value, "")
            ApplyToolTip()
        End Set
    End Property

    Private Sub ApplyToolTip()
        If _toolTipText.Length = 0 Then
            If _toolTip IsNot Nothing Then _toolTip.SetToolTip(MediaViewerControl1, "")
            Return
        End If
        If _toolTip Is Nothing Then
            If components Is Nothing Then components = New System.ComponentModel.Container()
            _toolTip = New ToolTip(components)
            _toolTip.InitialDelay = 500   ' VB6 ToolTip DelayTime 預設 500
        End If
        _toolTip.ToolTipTitle = _toolTipTitle
        _toolTip.SetToolTip(MediaViewerControl1, _toolTipText)
    End Sub

    ' 標記圖畫在媒體上方(VB6 是獨立的 picMarked 子控制項;這裡直接畫在 MediaViewerControl 上)
    Private Sub OnViewerPaint(sender As Object, e As PaintEventArgs)
        If Not _marked Then Return
        Dim photo As Rectangle = MediaViewerControl1.DisplayedImageRect()
        Dim sz As Size = If(_markImage IsNot Nothing, _markImage.Size, New Size(18, 18))
        Dim x As Integer, y As Integer
        Select Case _markAlignment
            Case ContentAlignment.TopLeft, ContentAlignment.MiddleLeft, ContentAlignment.BottomLeft
                x = photo.Left
            Case ContentAlignment.TopCenter, ContentAlignment.MiddleCenter, ContentAlignment.BottomCenter
                x = photo.Left + (photo.Width - sz.Width) \ 2
            Case Else
                x = photo.Right - sz.Width
        End Select
        Select Case _markAlignment
            Case ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight
                y = photo.Top
            Case ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight
                y = photo.Top + (photo.Height - sz.Height) \ 2
            Case Else
                y = photo.Bottom - sz.Height
        End Select
        If _markPosition = MediaItemMarkPosition.CenterToPhoto Then
            ' 以媒體邊緣為中心:往對齊的外側移半個標記,但不超出媒體區(同 VB6 MarkUserControl)
            Dim cs As Size = MediaViewerControl1.ClientSize
            Select Case _markAlignment
                Case ContentAlignment.TopLeft, ContentAlignment.MiddleLeft, ContentAlignment.BottomLeft
                    If x - sz.Width \ 2 > 0 Then x -= sz.Width \ 2
                Case ContentAlignment.TopRight, ContentAlignment.MiddleRight, ContentAlignment.BottomRight
                    If x + sz.Width + sz.Width \ 2 < cs.Width Then x += sz.Width \ 2
            End Select
            Select Case _markAlignment
                Case ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight
                    If y - sz.Height \ 2 > 0 Then y -= sz.Height \ 2
                Case ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight
                    If y + sz.Height + sz.Height \ 2 < cs.Height Then y += sz.Height \ 2
            End Select
        End If
        If _markImage IsNot Nothing Then
            If _markTransparencyKey.IsEmpty OrElse _markTransparencyKey.A = 0 Then
                e.Graphics.DrawImage(_markImage, x, y, sz.Width, sz.Height)
            Else
                ' VB6 cut the key colour out of picMarked with a window region
                Dim key As Color = Color.FromArgb(255, _markTransparencyKey.R, _markTransparencyKey.G, _markTransparencyKey.B)
                Using attrs As New Imaging.ImageAttributes()
                    attrs.SetColorKey(key, key)
                    e.Graphics.DrawImage(_markImage, New Rectangle(x, y, sz.Width, sz.Height), 0, 0, _markImage.Width, _markImage.Height, GraphicsUnit.Pixel, attrs)
                End Using
            End If
        Else
            DrawDefaultMark(e.Graphics, New Rectangle(x, y, sz.Width, sz.Height))
        End If
    End Sub

    Private Shared Sub DrawDefaultMark(ByVal g As Graphics, ByVal r As Rectangle)
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Using b As New SolidBrush(Color.FromArgb(120, 170, 230))
            g.FillEllipse(b, r)
        End Using
        Using p As New Pen(Color.White, 2.0F)
            g.DrawLines(p, New PointF() {
                New PointF(r.X + r.Width * 0.27F, r.Y + r.Height * 0.52F),
                New PointF(r.X + r.Width * 0.44F, r.Y + r.Height * 0.7F),
                New PointF(r.X + r.Width * 0.74F, r.Y + r.Height * 0.32F)})
        End Using
    End Sub

    Private Sub OnChildMouseDown(sender As Object, e As MouseEventArgs)
        RaiseEvent MediaMouseDown(Me, e)
    End Sub

    Private Sub OnChildDoubleClick(sender As Object, e As EventArgs)
        RaiseEvent MediaDoubleClick(Me, EventArgs.Empty)
    End Sub

    Private Sub OnChildDragCompleted(ByVal effect As DragDropEffects)
        RaiseEvent DragCompleted(effect)
    End Sub

    ' 選取時在保留的邊框帶畫淡藍色框
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _selected Then
            Dim bw As Integer = SelectionBorderWidth
            Dim w As Integer = Me.Width
            Dim h As Integer = Me.Height
            ' 填滿四邊邊框帶,剛好對齊 padding 保留區,四邊等寬且不被子控制項覆蓋(避免壓線)
            Using b As New SolidBrush(Color.FromArgb(120, 170, 230))   ' 淡藍色
                e.Graphics.FillRectangle(b, 0, 0, w, bw)                ' 上
                e.Graphics.FillRectangle(b, 0, h - bw, w, bw)          ' 下
                e.Graphics.FillRectangle(b, 0, 0, bw, h)              ' 左
                e.Graphics.FillRectangle(b, w - bw, 0, bw, h)         ' 右
            End Using
        End If
    End Sub

    ' ===== 事件轉發 =====

    Private Sub OnChildCheckedChanged(sender As Object, e As EventArgs)
        RaiseEvent CheckedChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub OnChildRatingChanged(ByVal newRating As Integer)
        RaiseEvent RatingChanged(newRating)
    End Sub

    Private Sub OnChildMediaClicked(sender As Object, e As MediaClickedEventArgs)
        RaiseEvent MediaClicked(Me, e)
    End Sub

    Private Sub OnChildDropMedia(ByVal fileName As String)
        RaiseEvent DropMedia(fileName)
    End Sub
End Class

End Namespace
