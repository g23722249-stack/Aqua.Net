Option Strict Off
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>捲軸方向。</summary>
Namespace Global.Aqua

Public Enum MediaScrollDirection
    ''' <summary>垂直捲動:依寬度自動排列每列欄數,向下堆疊。</summary>
    Vertical = 0
    ''' <summary>水平捲動:依高度自動排列每欄列數,向右堆疊。</summary>
    Horizontal = 1
End Enum

''' <summary>
''' 顯示多個 MediaItem 的清單控制項。可設定捲軸方向;每列(交叉軸)的數量
''' 依 ItemSize 與可視區大小自動計算,並在縮放時重算。
''' 為避免圖片過多,採延遲載入:僅優先載入顯示區域內的 FileName,
''' 其餘項目捲動進入顯示區域時才載入。
''' </summary>
<DefaultProperty("ScrollDirection")>
Public Class MediaList
    Inherits UserControl
    Implements IMessageFilter

    ' 虛擬化:每張相片都有一個 MediaItem 物件(資料與縮圖),但只有顯示區域附近的那一段
    ' (_realFirst.._realLast,連續的索引)真的放進 viewport、有視窗。其他項目沒有任何視窗:
    ' 以前每個項目都是 5 個視窗,約 2000 張就用完 Windows 每個程式 10000 個視窗的上限。
    ' 項目直接放在 viewport 裡、用畫面座標(邏輯座標減捲動位移);邏輯座標在 _bounds。
    Private ReadOnly _viewport As New FocusPanel()
    Private ReadOnly _bar As New ThinScrollBar()   ' 自繪浮層捲軸
    Private _realFirst As Integer = -1, _realLast As Integer = -1   ' 目前有視窗的項目範圍(-1 = 沒有)
    Private _layoutVersion As Integer = 0            ' RelayoutItems 每次加一:項目位置/大小改了
    Private _placedOffset As Integer = -1, _placedLayout As Integer = -1   ' 有視窗的項目是依哪個捲動位置/版面擺的
    Private _contentSize As Size = Size.Empty       ' 全部項目排好的大小(邏輯座標)
    Private _scrollOffset As Integer = 0            ' 主軸捲動位移(>=0)
    Private _syncingBar As Boolean = False          ' 防止 ApplyScroll 與 bar.ValueChanged 互相遞迴
    Private _filterAdded As Boolean = False

    Private ReadOnly _items As New List(Of MediaItem)()
    Private ReadOnly _bounds As New List(Of Rectangle)()          ' 各項的邏輯座標(未捲動時的位置)
    Private ReadOnly _pending As New Dictionary(Of MediaItem, String)()  ' 尚未載入的 FileName
    Private ReadOnly _relays As New Dictionary(Of MediaItem, ItemEventRelay)()  ' 各項事件轉發器

    ' ===== 清單層事件(由項目轉發,均帶出來源 MediaItem) =====
    ''' <summary>點擊某個項目的媒體。</summary>
    <Category("MediaList"), Description("點擊某個項目的媒體時發生。")>
    Public Event ItemClick(ByVal item As MediaItem)
    ''' <summary>某個項目的核取方塊狀態改變。</summary>
    <Category("MediaList"), Description("某個項目的核取方塊狀態改變時發生。")>
    Public Event ItemCheckedChanged(ByVal item As MediaItem)
    ''' <summary>某個項目的評分改變。</summary>
    <Category("MediaList"), Description("某個項目的評分改變時發生。")>
    Public Event ItemRatingChanged(ByVal item As MediaItem, ByVal newRating As Integer)
    ''' <summary>拖放媒體到某個項目完成。</summary>
    <Category("MediaList"), Description("拖放媒體到某個項目完成時發生。")>
    Public Event ItemDropMedia(ByVal item As MediaItem, ByVal fileName As String)
    ''' <summary>目前選取項目改變(item 可能為 Nothing)。</summary>
    <Category("MediaList"), Description("目前選取項目改變時發生。")>
    Public Event SelectedItemChanged(ByVal item As MediaItem)

    ' ===== VB6 Aqua.MediaList 相容事件(以索引帶出項目,順序同 VB6) =====
    ' 選取流程:BeforeSelectedChanged → (選取) → ItemClick → ItemSelected → ItemKeyDown(鍵盤時)
    '           → SelectedChanged → AfterSelectedChanged。點擊/鍵盤/設定 SelectedIndex 都走同一流程,
    '           所以 ItemSelected 一定早於 SelectedChanged(iPhoto 在 ItemSelected 準備目前相片,
    '           SelectedChanged 才顯示它)。
    ''' <summary>選取即將改變(可在此儲存目前項目的編輯)。</summary>
    <Category("MediaList (VB6)")>
    Public Event BeforeSelectedChanged(ByVal sender As Object, ByVal e As EventArgs)
    ''' <summary>選取已改變。點擊時只在換了項目才發生;設定 SelectedIndex 時一律發生(同 VB6)。</summary>
    <Category("MediaList (VB6)")>
    Public Event SelectedChanged(ByVal sender As Object, ByVal e As EventArgs)
    ''' <summary>選取流程結束。</summary>
    <Category("MediaList (VB6)")>
    Public Event AfterSelectedChanged(ByVal sender As Object, ByVal e As EventArgs)
    ''' <summary>某個項目被選取(點擊、鍵盤或設定 SelectedIndex)。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemSelected(ByVal index As Integer)
    ''' <summary>某個項目的 Marked 改變(含程式設定)。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemMarkChanged(ByVal index As Integer)
    ''' <summary>在某個項目的媒體上按下滑鼠;座標相對於該媒體。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemMouseDown(ByVal index As Integer, ByVal e As MouseEventArgs)
    ''' <summary>清單有焦點時按鍵;index 為按鍵當下選取的項目。方向鍵/Home/End/PageUp/PageDown 會先移動選取。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemKeyDown(ByVal index As Integer, ByVal e As KeyEventArgs)
    ''' <summary>把某個項目拖出後結束,帶出拖放結果。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemCompleteDrag(ByVal index As Integer, ByVal effect As DragDropEffects)
    ''' <summary>雙擊某個項目(之後也會引發清單本身的 DoubleClick,即 VB6 的 DblClick)。</summary>
    <Category("MediaList (VB6)")>
    Public Event ItemDblClick(ByVal index As Integer)

    Private _limit As Integer = 0                      ' >0:每列(交叉軸)固定 Limit 個,項目大小隨可視區計算
    Private _aspectW As Integer = 4, _aspectH As Integer = 3
    Private _itemBorderSize As Integer = MediaItem.SelectionGap
    Private _markImage As Image = Nothing
    Private _markAlignment As ContentAlignment = ContentAlignment.BottomLeft
    Private _markPosition As MediaItemMarkPosition = MediaItemMarkPosition.SnapToPhoto
    Private _dragItem As Boolean = True
    Private _dropItem As Boolean = True
    Private _themedBorder As Boolean = False
    Private _borderColor As Color = ColorUtil.OleToColor(12434877)
    Private _borderFocusColor As Color = GridConst.BorderFocusColor
    Private _focused As Boolean = False

    Private _scrollDirection As MediaScrollDirection = MediaScrollDirection.Vertical
    Private _itemSize As New Size(200, 180)
    Private _itemMargin As Integer = 6
    Private _showCheckBox As Boolean = True
    Private _showRating As Boolean = True
    Private _lastPerLine As Integer = 1   ' 上次計算的每列數量,縮放時比對是否需重排
    ' 分段標題(只在垂直捲動時):第一個項目的索引 → 標題。每段從新的一列開始,上面留標題列。
    Private ReadOnly _sections As New SortedList(Of Integer, String)()
    Private _sectionHeaderHeight As Integer = 34
    Private _sectionHeaderWidth As Integer = 68     ' 水平捲動時:每段前面的標題欄寬
    Private _sectionFont As Font = Nothing
    Private _sectionForeColor As Color = Color.FromArgb(40, 40, 40)
    Private _sectionSubColor As Color = Color.FromArgb(120, 120, 120)
    Private _selectedItem As MediaItem = Nothing   ' 目前選取項目(單選;多選時是焦點項目)
    ' 多選(MultiSelect):所有選取的項目(含 _selectedItem);單選時只有 _selectedItem 一項或空
    Private ReadOnly _multi As New HashSet(Of MediaItem)()
    Private _anchor As MediaItem = Nothing          ' Shift 範圍選取的起點
    Private _multiSelect As Boolean = False

    ''' <summary>多選改變(Ctrl／Shift 點選、Ctrl+A、單選時也會引發)。</summary>
    Public Event SelectionChanged(ByVal sender As Object, ByVal e As EventArgs)

    ''' <summary>允許 Ctrl+點選(加入／移出)、Shift+點選(範圍)、Ctrl+A(全選)多選;預設 False(單選)。
    ''' SelectedIndex / SelectedItem 仍是焦點項目,SelectedIndices 是全部選取的項目。</summary>
    <Category("Behavior"), DefaultValue(False)>
    Public Property MultiSelect As Boolean
        Get
            Return _multiSelect
        End Get
        Set(value As Boolean)
            _multiSelect = value
            If Not value AndAlso _multi.Count > 1 Then SetOnly(_selectedItem)
        End Set
    End Property

    ''' <summary>所有選取項目的索引(由小到大);沒有選取時為空。</summary>
    <Browsable(False)>
    Public ReadOnly Property SelectedIndices As List(Of Integer)
        Get
            Return _multi.Select(Function(it) _items.IndexOf(it)).Where(Function(i) i >= 0).OrderBy(Function(i) i).ToList()
        End Get
    End Property

    ''' <summary>索引 <paramref name="index"/> 的項目是否在選取中。</summary>
    Public Function IsSelected(ByVal index As Integer) As Boolean
        Return index >= 0 AndAlso index < _items.Count AndAlso _multi.Contains(_items(index))
    End Function

    ''' <summary>全選(MultiSelect 時);焦點項目不變(沒有時為第一項)。</summary>
    Public Sub SelectAll()
        If Not _multiSelect OrElse _items.Count = 0 Then Return
        If _selectedItem Is Nothing Then SelectCore(0, SelectSource.Code, Nothing, -1)
        For Each it As MediaItem In _items
            If _multi.Add(it) Then it.Selected = True
        Next
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>全不選:只留焦點項目(清單總要有一個目前項目,iPhoto 靠它顯示照片)。</summary>
    Public Sub SelectNone()
        If _multi.Count <= 1 Then Return
        SetOnly(_selectedItem)
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>反向選取(MultiSelect 時):選取中的取消、沒選的選取;焦點移到選取中的第一項
    ''' (全部都取消時留在原來那一項)。</summary>
    Public Sub InvertSelection()
        If Not _multiSelect OrElse _items.Count = 0 Then Return
        Dim wanted As New List(Of MediaItem)
        For Each it As MediaItem In _items
            If Not _multi.Contains(it) Then wanted.Add(it)
        Next
        If wanted.Count = 0 Then
            SelectNone()
            Return
        End If
        Dim focus As MediaItem = If(wanted.Contains(_selectedItem), _selectedItem, wanted(0))
        SelectCore(_items.IndexOf(focus), SelectSource.Code, Nothing, -1)   ' focus alone first (events as usual)
        For Each it In wanted
            If _multi.Add(it) Then it.Selected = True
        Next
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>只留 <paramref name="keep"/> 在選取中(Nothing:全部取消)。</summary>
    Private Sub SetOnly(ByVal keep As MediaItem)
        For Each it In _multi.ToList()
            If it IsNot keep Then it.Selected = False
        Next
        _multi.Clear()
        If keep IsNot Nothing Then _multi.Add(keep)
    End Sub

    Public Sub New()
        Me.SetStyle(ControlStyles.ContainerControl, True)

        _viewport.Dock = DockStyle.Fill
        _viewport.AutoScroll = False    ' 改手動捲動,原生捲軸關閉
        Me.Controls.Add(_viewport)

        _bar.Orientation = If(_scrollDirection = MediaScrollDirection.Vertical, System.Windows.Forms.Orientation.Vertical, System.Windows.Forms.Orientation.Horizontal)
        _bar.ThumbColor = Color.FromArgb(120, 170, 230)   ' 配合選取藍
        _viewport.Controls.Add(_bar)
        _bar.BringToFront()
        AddHandler _bar.ValueChanged, AddressOf OnBarValueChanged

        ' 縮放時 viewport ClientSize 改變 → 每列數量可能改變 → 需重排/重算捲軸。
        AddHandler _viewport.ClientSizeChanged, AddressOf OnViewportResized
        AddHandler _viewport.Paint, AddressOf PaintSections
        AddHandler _viewport.MouseDown, AddressOf ViewportMouseDown
    End Sub

    ' ===== 滑鼠滾輪(IMessageFilter,不被子控制項吃掉) =====

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If Not _filterAdded Then
            Application.AddMessageFilter(Me)
            _filterAdded = True
        End If
    End Sub

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        If _filterAdded Then
            Application.RemoveMessageFilter(Me)
            _filterAdded = False
        End If
        MyBase.OnHandleDestroyed(e)
    End Sub

    ' 捲出畫面的項目不在任何 Controls 集合裡,不會跟著清單一起釋放:自己釋放全部項目
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            For Each it As MediaItem In _items
                UnhookItem(it)
                it.Dispose()
            Next
            _items.Clear()
            _bounds.Clear()
            _pending.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Const WM_MOUSEWHEEL As Integer = &H20A
        If m.Msg = WM_MOUSEWHEEL AndAlso Me.Visible AndAlso _viewport.IsHandleCreated Then
            Dim lp As Integer = m.LParam.ToInt32()
            Dim pt As New Point(SignedLoWord(lp), SignedLoWord(lp >> 16))
            If _viewport.ClientRectangle.Contains(_viewport.PointToClient(pt)) Then
                Dim delta As Integer = SignedLoWord(CInt((m.WParam.ToInt64() >> 16) And &HFFFF))
                OnWheelDelta(delta)
                Return True
            End If
        End If
        Return False
    End Function

    ' 取 16 位有號值(避免 CShort 對 >32767 的低字溢位)
    Private Shared Function SignedLoWord(ByVal v As Integer) As Integer
        Dim raw As Integer = v And &HFFFF
        If raw > &H7FFF Then raw -= &H10000
        Return raw
    End Function

    Private Sub OnWheelDelta(ByVal delta As Integer)
        Dim stepPx As Integer = 60
        ApplyScroll(_scrollOffset - Math.Sign(delta) * stepPx)
    End Sub

    ' ===== 捲動核心 =====

    Private Function MainContentLen() As Integer
        Return If(_scrollDirection = MediaScrollDirection.Vertical, _contentSize.Height, _contentSize.Width)
    End Function

    Private Function ViewLen() As Integer
        Return If(_scrollDirection = MediaScrollDirection.Vertical, _viewport.ClientSize.Height, _viewport.ClientSize.Width)
    End Function

    ' 套用捲動位移(夾在合法範圍,同步捲軸,重新決定哪些項目有視窗並擺到畫面位置)
    Private Sub ApplyScroll(ByVal v As Integer)
        Dim range As Integer = Math.Max(0, MainContentLen() - ViewLen())
        If v < 0 Then v = 0
        If v > range Then v = range
        _scrollOffset = v

        _syncingBar = True
        _bar.Value = v
        _syncingBar = False

        UpdateRealized()
    End Sub

    Private Sub OnBarValueChanged(ByVal newValue As Integer)
        If _syncingBar Then Return
        ApplyScroll(newValue)
    End Sub

    ' 依 content / viewport 更新捲軸度量與位置
    Private Sub UpdateScrollMetrics()
        PositionBar()
        _bar.Orientation = If(_scrollDirection = MediaScrollDirection.Vertical, System.Windows.Forms.Orientation.Vertical, System.Windows.Forms.Orientation.Horizontal)
        _bar.SetRange(MainContentLen(), ViewLen())
        ApplyScroll(_scrollOffset)   ' 夾範圍並同步
    End Sub

    ' 浮層捲軸位置:垂直靠右、水平靠下,不佔版面
    Private Sub PositionBar()
        Dim t As Integer = _bar.BarThickness
        If _scrollDirection = MediaScrollDirection.Vertical Then
            _bar.Bounds = New Rectangle(_viewport.ClientSize.Width - t, 0, t, _viewport.ClientSize.Height)
        Else
            _bar.Bounds = New Rectangle(0, _viewport.ClientSize.Height - t, _viewport.ClientSize.Width, t)
        End If
        _bar.BringToFront()
    End Sub

    ' ===== 設定屬性 =====

    ''' <summary>捲軸方向。</summary>
    <Category("MediaList"), Description("捲軸方向(垂直/水平)。"), DefaultValue(GetType(MediaScrollDirection), "Vertical")>
    Public Property ScrollDirection As MediaScrollDirection
        Get
            Return _scrollDirection
        End Get
        Set(value As MediaScrollDirection)
            If _scrollDirection <> value Then
                _scrollDirection = value
                _bar.Orientation = If(value = MediaScrollDirection.Vertical, System.Windows.Forms.Orientation.Vertical, System.Windows.Forms.Orientation.Horizontal)
                _scrollOffset = 0
                RelayoutItems()
            End If
        End Set
    End Property

    ''' <summary>每個 MediaItem 的顯示大小。</summary>
    <Category("MediaList"), Description("每個 MediaItem 的顯示大小。")>
    Public Property ItemSize As Size
        Get
            Return _itemSize
        End Get
        Set(value As Size)
            If value.Width < 1 Then value.Width = 1
            If value.Height < 1 Then value.Height = 1
            If _itemSize <> value Then
                _itemSize = value
                RelayoutItems()
            End If
        End Set
    End Property

    ''' <summary>項目之間與邊界的間距(像素)。</summary>
    <Category("MediaList"), Description("項目間距(像素)。"), DefaultValue(6)>
    Public Property ItemMargin As Integer
        Get
            Return _itemMargin
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _itemMargin <> value Then
                _itemMargin = value
                RelayoutItems()
            End If
        End Set
    End Property

    ''' <summary>是否顯示所有項目的核取方塊(設定時套用至現有項目,並作為新項目預設)。</summary>
    <Category("MediaList"), Description("是否顯示所有項目的核取方塊。"), DefaultValue(True)>
    Public Property ShowCheckBox As Boolean
        Get
            Return _showCheckBox
        End Get
        Set(value As Boolean)
            _showCheckBox = value
            For Each it As MediaItem In _items
                it.ShowCheckBox = value
            Next
            If _limit > 0 Then RelayoutItems()   ' 底部面板有無會改變 Limit 模式的項目高度
        End Set
    End Property

    ''' <summary>是否顯示所有項目的評分(設定時套用至現有項目,並作為新項目預設)。</summary>
    <Category("MediaList"), Description("是否顯示所有項目的評分。"), DefaultValue(True)>
    Public Property ShowRating As Boolean
        Get
            Return _showRating
        End Get
        Set(value As Boolean)
            _showRating = value
            For Each it As MediaItem In _items
                it.ShowRating = value
            Next
            If _limit > 0 Then RelayoutItems()
        End Set
    End Property

    ' ===== 資料存取 =====

    ''' <summary>項目數。</summary>
    <Browsable(False)>
    Public ReadOnly Property Count As Integer
        Get
            Return _items.Count
        End Get
    End Property

    ''' <summary>依索引取得 MediaItem。</summary>
    <Browsable(False)>
    Default Public ReadOnly Property Item(ByVal Index As Integer) As MediaItem
        Get
            Return _items(Index)
        End Get
    End Property

    ''' <summary>
    ''' 新增一個 MediaItem 並回傳。FileName 採延遲載入:只有出現在顯示區域時才實際載入媒體。
    ''' </summary>
    ''' <param name="FileName">媒體檔(圖片或影片)。</param>
    ''' <param name="Rating">評分值(0~5)。</param>
    ''' <param name="ShowCheckBox">此項目是否顯示核取方塊。</param>
    ''' <param name="IsChecked">核取方塊是否勾選。</param>
    ''' <param name="CheckText">核取方塊文字。</param>
    Public Function AddItem(ByVal FileName As String,
                            ByVal Rating As Integer,
                            ByVal ShowCheckBox As Boolean,
                            ByVal IsChecked As Boolean,
                            ByVal CheckText As String) As MediaItem
        Dim item As New MediaItem()
        item.UseBackgroundLoading()   ' 縮圖在背景執行緒產生,捲動不卡
        item.ShowRating = _showRating
        item.ShowCheckBox = ShowCheckBox
        item.Rating = Rating
        item.Checked = IsChecked
        item.CheckText = CheckText
        ApplyListSettings(item)

        Dim index As Integer = _items.Count
        Dim perLine As Integer = ComputePerLine()
        _lastPerLine = perLine
        Dim rc As Rectangle = CellBounds(index, perLine)

        ' 只記下位置:要到捲進顯示區域(UpdateRealized)才放進 viewport、建立視窗
        _items.Add(item)
        _bounds.Add(rc)
        item.Size = rc.Size
        HookItem(item)

        ' 延遲載入:先記錄 FileName,可視時才指派 item.FileName 觸發載入
        ' (項目先記住檔名,未載入前 item.FileName 也讀得到)
        If Not String.IsNullOrEmpty(FileName) Then
            _pending(item) = FileName
            item.SetDeferredFileName(FileName)
        End If

        _contentSize = New Size(Math.Max(_contentSize.Width, rc.Right + _itemMargin), Math.Max(_contentSize.Height, rc.Bottom + _itemMargin))

        If _updating = 0 Then UpdateScrollMetrics()   ' 捲動位置不變(不會自動捲到新項目);新項目在顯示區域內才建立視窗
        Return item
    End Function

    Private _updating As Integer = 0

    ''' <summary>一次加入大量項目前呼叫:AddItem 只記下位置,不更新捲軸、不建立視窗,
    ''' 到 EndUpdate 才一次做完(數千張時快很多)。可以巢狀呼叫。</summary>
    Public Sub BeginUpdate()
        _updating += 1
    End Sub

    ''' <summary>結束 BeginUpdate:更新捲軸並顯示可見的項目。</summary>
    Public Sub EndUpdate()
        If _updating = 0 Then Return
        _updating -= 1
        If _updating = 0 Then
            UpdateScrollMetrics()
            _viewport.Invalidate()
        End If
    End Sub

    ''' <summary>
    ''' 新增項目(VB6 AddItem 簽章):評分 0、核取方塊依清單的 ShowCheckBox、未勾選。
    ''' toolTipText 非空白時,滑鼠停在媒體上會顯示提示,標題為檔名(同 VB6)。
    ''' </summary>
    Public Function AddItem(ByVal FileName As String, Optional ByVal toolTipText As String = "") As MediaItem
        Dim item As MediaItem = AddItem(FileName, 0, _showCheckBox, False, "")
        If Not String.IsNullOrEmpty(toolTipText) AndAlso toolTipText.Trim().Length > 0 Then
            item.ToolTipTitle = System.IO.Path.GetFileName(If(FileName, ""))
            item.ToolTipText = toolTipText
        End If
        Return item
    End Function

    ''' <summary>移除指定索引的項目(VB6 RemoveItem)。移除的是選取項目時,改選同位置的下一項
    ''' (沒有下一項則選最後一項),並引發完整的選取事件。</summary>
    Public Function RemoveItem(ByVal index As Integer) As Boolean
        Dim wasSelected As Boolean = (index >= 0 AndAlso index < _items.Count AndAlso _items(index) Is _selectedItem)
        If Not RemoveAt(index) Then Return False
        If wasSelected AndAlso _items.Count > 0 Then SelectedIndex = Math.Min(index, _items.Count - 1)
        Return True
    End Function

    ' 清單層設定套用到項目(新增時與設定改變時)
    Private Sub ApplyListSettings(ByVal item As MediaItem)
        item.BorderSize = _itemBorderSize
        item.MarkImage = _markImage
        item.MarkTransparencyKey = _markTransparencyKey
        item.MarkAlignment = _markAlignment
        item.MarkPosition = _markPosition
        item.DragItem = _dragItem
        item.DropItem = _dropItem
    End Sub

    Private Sub ApplyListSettingsToAll()
        For Each it As MediaItem In _items
            ApplyListSettings(it)
        Next
    End Sub

    ''' <summary>清空所有項目。</summary>
    Public Sub Clear()
        ReleaseAll()
        _pending.Clear()
        For Each it As MediaItem In _items
            UnhookItem(it)
            it.Dispose()
        Next
        _items.Clear()
        _bounds.Clear()
        _sections.Clear()
        _contentSize = Size.Empty
        _scrollOffset = 0
        UpdateScrollMetrics()
        _viewport.Invalidate()
        _multi.Clear()
        _anchor = Nothing
        If _selectedItem IsNot Nothing Then
            _selectedItem = Nothing
            RaiseEvent SelectedItemChanged(Nothing)
        End If
    End Sub

    ''' <summary>移除指定項目。</summary>
    ''' <returns>成功移除傳回 True。</returns>
    Public Function RemoveItem(ByVal item As MediaItem) As Boolean
        Dim idx As Integer = _items.IndexOf(item)
        If idx < 0 Then Return False
        Return RemoveAt(idx)
    End Function

    ''' <summary>移除指定索引的項目。</summary>
    ''' <returns>成功移除傳回 True。</returns>
    Public Function RemoveAt(ByVal index As Integer) As Boolean
        If index < 0 OrElse index >= _items.Count Then Return False
        Dim item As MediaItem = _items(index)
        Dim wasSelected As Boolean = (_selectedItem Is item)
        _multi.Remove(item)
        If _anchor Is item Then _anchor = Nothing
        ReleaseAll()   ' 後面的索引都會位移:先全部釋放,RelayoutItems 再依新位置建立
        UnhookItem(item)
        _pending.Remove(item)
        _items.RemoveAt(index)
        _bounds.RemoveAt(index)
        ShiftSectionsAfterRemove(index)
        item.Dispose()
        If wasSelected Then
            _selectedItem = Nothing
            RaiseEvent SelectedItemChanged(Nothing)
        End If
        RelayoutItems()   ' 後續項目索引位移,重新配置
        Return True
    End Function

    ''' <summary>取得項目索引;不存在傳回 -1。</summary>
    Public Function IndexOf(ByVal item As MediaItem) As Integer
        Return _items.IndexOf(item)
    End Function

    ''' <summary>是否包含指定項目。</summary>
    Public Function Contains(ByVal item As MediaItem) As Boolean
        Return _items.Contains(item)
    End Function

    ''' <summary>全部勾選或全部取消勾選。</summary>
    Public Sub CheckAll(ByVal isChecked As Boolean)
        For Each it As MediaItem In _items
            it.Checked = isChecked
        Next
    End Sub

    ''' <summary>捲動使指定索引的項目進入顯示區域(並觸發其載入)。</summary>
    Public Sub EnsureVisible(ByVal index As Integer)
        If index < 0 OrElse index >= _items.Count Then Return
        Dim rc As Rectangle = _bounds(index)
        Dim target As Integer = If(_scrollDirection = MediaScrollDirection.Vertical, rc.Y, rc.X) - HeaderAbove(index)
        ApplyScroll(target)
    End Sub

    ''' <summary>所有已勾選的項目。</summary>
    <Browsable(False)>
    Public ReadOnly Property SelectedItems As MediaItem()
        Get
            Dim list As New List(Of MediaItem)()
            For Each it As MediaItem In _items
                If it.Checked Then list.Add(it)
            Next
            Return list.ToArray()
        End Get
    End Property

    ''' <summary>已勾選項目的數量。</summary>
    <Browsable(False)>
    Public ReadOnly Property CheckedCount As Integer
        Get
            Dim n As Integer = 0
            For Each it As MediaItem In _items
                If it.Checked Then n += 1
            Next
            Return n
        End Get
    End Property

    ''' <summary>目前選取的項目(單選);設定 Nothing 可清除選取。選取項目以淡藍色邊框顯示。</summary>
    <Browsable(False)>
    Public Property SelectedItem As MediaItem
        Get
            Return _selectedItem
        End Get
        Set(value As MediaItem)
            If value IsNot Nothing AndAlso Not _items.Contains(value) Then Return
            If _selectedItem Is value Then Return
            SelectCore(If(value Is Nothing, -1, _items.IndexOf(value)), SelectSource.Code, Nothing, -1)
        End Set
    End Property

    ''' <summary>目前選取項目的索引;未選取為 -1。設定有效索引時一律引發選取事件
    ''' (即使與目前相同,同 VB6:可用來重新整理目前項目)並捲動使它可見。</summary>
    <Browsable(False)>
    Public Property SelectedIndex As Integer
        Get
            If _selectedItem Is Nothing Then Return -1
            Return _items.IndexOf(_selectedItem)
        End Get
        Set(value As Integer)
            If value < 0 OrElse value >= _items.Count Then
                If _selectedItem IsNot Nothing Then SelectCore(-1, SelectSource.Code, Nothing, -1)
            Else
                SelectCore(value, SelectSource.Code, Nothing, -1)
            End If
        End Set
    End Property

    Private Enum SelectSource
        Code
        Click
        Key
    End Enum

    ''' <summary>唯一的選取流程(順序見上方 VB6 相容事件的說明)。index = -1 表示清除選取。</summary>
    Private Sub SelectCore(ByVal index As Integer, ByVal source As SelectSource,
                           ByVal keyArgs As KeyEventArgs, ByVal keyIndex As Integer,
                           Optional ByVal keepOthers As Boolean = False)
        Dim newItem As MediaItem = If(index >= 0 AndAlso index < _items.Count, _items(index), Nothing)
        RaiseEvent BeforeSelectedChanged(Me, EventArgs.Empty)
        Dim changed As Boolean = newItem IsNot _selectedItem
        Dim hadOthers As Boolean = _multi.Count > 1 OrElse (_multi.Count = 1 AndAlso Not _multi.Contains(newItem))
        If keepOthers Then
            If newItem IsNot Nothing AndAlso _multi.Add(newItem) Then newItem.Selected = True
        Else
            SetOnly(newItem)
            If newItem IsNot Nothing Then newItem.Selected = True
            _anchor = newItem
        End If
        If changed Then _selectedItem = newItem
        If newItem IsNot Nothing Then
            If source <> SelectSource.Click Then ScrollIntoView(index)
            If source = SelectSource.Click Then RaiseEvent ItemClick(newItem)
            RaiseEvent ItemSelected(index)
        End If
        If keyArgs IsNot Nothing Then RaiseEvent ItemKeyDown(keyIndex, keyArgs)
        If changed Then RaiseEvent SelectedItemChanged(_selectedItem)
        If newItem IsNot Nothing AndAlso (changed OrElse source = SelectSource.Code) Then
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End If
        RaiseEvent AfterSelectedChanged(Me, EventArgs.Empty)
        If changed OrElse hadOthers OrElse keepOthers Then RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    ' 由項目點擊觸發的選取(供事件轉發器呼叫);清單取得焦點,之後方向鍵可移動選取
    ''' <param name="toggle">True:當作按著 Ctrl 點選(滑鼠中鍵)。</param>
    Friend Sub SelectByClick(ByVal item As MediaItem, Optional ByVal toggle As Boolean = False)
        Dim idx As Integer = _items.IndexOf(item)
        If idx < 0 Then Return
        If Not Me.ContainsFocus Then Me.Focus()
        Dim mods As Keys = Control.ModifierKeys
        If _multiSelect AndAlso (toggle OrElse (mods And Keys.Control) = Keys.Control) Then
            ' Ctrl+點選:加入或移出選取
            If _multi.Contains(item) AndAlso _multi.Count > 1 Then
                _multi.Remove(item)
                item.Selected = False
                If _selectedItem Is item Then
                    ' 焦點移到仍選取中、最靠近的一項
                    Dim nearest As MediaItem = _multi.OrderBy(Function(it) Math.Abs(_items.IndexOf(it) - idx)).First()
                    SelectCore(_items.IndexOf(nearest), SelectSource.Click, Nothing, -1, keepOthers:=True)
                Else
                    RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                End If
            Else
                SelectCore(idx, SelectSource.Click, Nothing, -1, keepOthers:=True)
            End If
            _anchor = item
        ElseIf _multiSelect AndAlso (mods And Keys.Shift) = Keys.Shift AndAlso _anchor IsNot Nothing AndAlso _items.Contains(_anchor) Then
            ' Shift+點選:起點到這一項的範圍
            Dim a As Integer = _items.IndexOf(_anchor)
            Dim keepAnchor As MediaItem = _anchor
            SetOnly(Nothing)
            For i As Integer = Math.Min(a, idx) To Math.Max(a, idx)
                _multi.Add(_items(i))
                _items(i).Selected = True
            Next
            SelectCore(idx, SelectSource.Click, Nothing, -1, keepOthers:=True)
            _anchor = keepAnchor
        Else
            SelectCore(idx, SelectSource.Click, Nothing, -1)
        End If
    End Sub

    ''' <summary>必要時才捲動(只捲到剛好完整可見),不像 EnsureVisible 會把項目捲到最前面。</summary>
    Private Sub ScrollIntoView(ByVal index As Integer)
        If index < 0 OrElse index >= _bounds.Count Then Return
        Dim rc As Rectangle = _bounds(index)
        Dim startPos As Integer = If(_scrollDirection = MediaScrollDirection.Vertical, rc.Top, rc.Left) - HeaderAbove(index)
        Dim endPos As Integer = If(_scrollDirection = MediaScrollDirection.Vertical, rc.Bottom, rc.Right)
        If startPos - _itemMargin < _scrollOffset Then
            ApplyScroll(startPos - _itemMargin)
        ElseIf endPos + _itemMargin > _scrollOffset + ViewLen() Then
            ApplyScroll(endPos + _itemMargin - ViewLen())
        End If
    End Sub

    ' ===== VB6 Aqua.MediaList 相容屬性/方法 =====

    ''' <summary>每列(垂直捲動)或每欄(水平捲動)固定放幾個項目,項目大小隨可視區與 AspectRatio 計算
    ''' (VB6: Limit)。0 = 不限制,改用 ItemSize。</summary>
    <Category("MediaList (VB6)"), Description("每列(或每欄)固定放幾個項目;0 = 改用 ItemSize。"), DefaultValue(0)>
    Public Property Limit As Integer
        Get
            Return _limit
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _limit = value Then Return
            _limit = value
            RelayoutItems()
        End Set
    End Property

    ''' <summary>Limit 模式下媒體區的寬:高,例如 "4:3"(VB6: AspectRatio,預設 4:3)。</summary>
    <Category("MediaList (VB6)"), Description("Limit 模式下媒體區的寬:高,例如 4:3。"), DefaultValue("4:3")>
    Public Property AspectRatio As String
        Get
            Return _aspectW & ":" & _aspectH
        End Get
        Set(value As String)
            Dim w As Integer = 4, h As Integer = 3
            Dim parts As String() = If(value, "").Replace(" ", "").Split(":"c)
            If parts.Length = 2 Then
                Dim pw As Integer, ph As Integer
                If Integer.TryParse(parts(0), pw) AndAlso Integer.TryParse(parts(1), ph) AndAlso pw > 0 AndAlso ph > 0 Then
                    w = pw : h = ph
                End If
            End If
            If w = _aspectW AndAlso h = _aspectH Then Return
            _aspectW = w : _aspectH = h
            If _limit > 0 Then RelayoutItems()
        End Set
    End Property

    ''' <summary>同 ScrollDirection,以 VB6 的 OrientationMode 表示(VB6: Orientation)。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property Orientation As OrientationMode
        Get
            Return CType(CInt(_scrollDirection), OrientationMode)
        End Get
        Set(value As OrientationMode)
            ScrollDirection = CType(CInt(value), MediaScrollDirection)
        End Set
    End Property

    ''' <summary>捲動位置(像素)。設為 ScrollMax 捲到最後、ScrollMin 捲到最前(VB6: ScrollValue)。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property ScrollValue As Integer
        Get
            Return _scrollOffset
        End Get
        Set(value As Integer)
            ApplyScroll(value)
        End Set
    End Property

    <Browsable(False)>
    Public ReadOnly Property ScrollMin As Integer
        Get
            Return 0
        End Get
    End Property

    <Browsable(False)>
    Public ReadOnly Property ScrollMax As Integer
        Get
            Return Math.Max(0, MainContentLen() - ViewLen())
        End Get
    End Property

    ''' <summary>各項目選取框與媒體之間的留白(像素,VB6: BorderSize)。</summary>
    <Category("MediaList (VB6)"), Description("各項目選取框與媒體之間的留白(像素)。"), DefaultValue(MediaItem.SelectionGap)>
    Public Property BorderSize As Integer
        Get
            Return _itemBorderSize
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If _itemBorderSize = value Then Return
            _itemBorderSize = value
            ApplyListSettingsToAll()
            If _limit > 0 Then RelayoutItems()
        End Set
    End Property

    ''' <summary>各項目的標記圖(VB6: MarkImage);未指定時畫預設標記。</summary>
    <Category("MediaList (VB6)"), Description("各項目的標記圖;未指定時畫預設標記。")>
    Public Property MarkImage As Image
        Get
            Return _markImage
        End Get
        Set(value As Image)
            _markImage = value
            ApplyListSettingsToAll()
        End Set
    End Property

    Private _markTransparencyKey As Color = Color.White

    ''' <summary>標記圖中當作透明的顏色(VB6: MarkTransparencyKey,預設白色)。</summary>
    <Category("MediaList (VB6)"), Description("標記圖中當作透明的顏色(預設白色)。"), DefaultValue(GetType(Color), "White")>
    Public Property MarkTransparencyKey As Color
        Get
            Return _markTransparencyKey
        End Get
        Set(value As Color)
            _markTransparencyKey = value
            ApplyListSettingsToAll()
        End Set
    End Property

    <Category("MediaList (VB6)"), Description("標記圖對齊媒體的位置。"), DefaultValue(GetType(ContentAlignment), "BottomLeft")>
    Public Property MarkAlignment As ContentAlignment
        Get
            Return _markAlignment
        End Get
        Set(value As ContentAlignment)
            _markAlignment = value
            ApplyListSettingsToAll()
        End Set
    End Property

    <Category("MediaList (VB6)"), Description("標記圖貼齊媒體內側,或以媒體邊緣為中心。"), DefaultValue(GetType(MediaItemMarkPosition), "SnapToPhoto")>
    Public Property MarkPosition As MediaItemMarkPosition
        Get
            Return _markPosition
        End Get
        Set(value As MediaItemMarkPosition)
            _markPosition = value
            ApplyListSettingsToAll()
        End Set
    End Property

    ''' <summary>可否把項目拖出(VB6: DragItem)。注意:VB6 預設 False,這裡為了相容既有用法預設 True。</summary>
    <Category("MediaList (VB6)"), Description("可否把項目的媒體拖出。"), DefaultValue(True)>
    Public Property DragItem As Boolean
        Get
            Return _dragItem
        End Get
        Set(value As Boolean)
            _dragItem = value
            ApplyListSettingsToAll()
        End Set
    End Property

    ''' <summary>可否把媒體拖入項目(VB6: DropItem)。注意:VB6 預設 False,這裡為了相容既有用法預設 True。</summary>
    <Category("MediaList (VB6)"), Description("可否把媒體拖入項目。"), DefaultValue(True)>
    Public Property DropItem As Boolean
        Get
            Return _dropItem
        End Get
        Set(value As Boolean)
            _dropItem = value
            ApplyListSettingsToAll()
        End Set
    End Property

    ''' <summary>同 ShowRating,以 VB6 的 RankingMode 表示(VB6: Ranking)。</summary>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property Ranking As RankingMode
        Get
            Return If(_showRating, RankingMode.Shown, RankingMode.Hidden)
        End Get
        Set(value As RankingMode)
            ShowRating = (value = RankingMode.Shown)
        End Set
    End Property

    ''' <summary>是否畫 Aqua 主題外框(BorderColor / 取得焦點時 BorderFocusColor)。預設 False 以相容既有版面。</summary>
    <Category("MediaList (VB6)"), Description("是否畫 Aqua 主題外框。"), DefaultValue(False)>
    Public Property ThemedBorder As Boolean
        Get
            Return _themedBorder
        End Get
        Set(value As Boolean)
            If _themedBorder = value Then Return
            _themedBorder = value
            Me.Padding = If(value, New Padding(4), Padding.Empty)
            Invalidate()
        End Set
    End Property

    <Category("MediaList (VB6)"), Description("外框顏色(ThemedBorder)。")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            _borderColor = value
            If _themedBorder Then Invalidate()
        End Set
    End Property

    <Category("MediaList (VB6)"), Description("取得焦點時的外框顏色(ThemedBorder)。")>
    Public Property BorderFocusColor As Color
        Get
            Return _borderFocusColor
        End Get
        Set(value As Color)
            _borderFocusColor = value
            If _themedBorder Then Invalidate()
        End Set
    End Property

    ''' <summary>在項目中央彈出 AquaMenu;index 無效時在滑鼠位置彈出(VB6: PopupMenu)。</summary>
    Public Sub PopupMenu(ByVal index As Integer, ByVal menu As AquaMenu)
        If menu Is Nothing Then Return
        If index >= 0 AndAlso index < _items.Count Then
            ScrollIntoView(index)   ' 捲出畫面的項目沒有視窗,無法當選單的定位
            Dim it As MediaItem = _items(index)
            menu.ShowMenu(it, New Point(it.Width \ 2, it.Height \ 2))
        Else
            menu.ShowMenu()
        End If
    End Sub

    ''' <summary>同上,改用 WinForms 的 ContextMenuStrip。</summary>
    Public Sub PopupMenu(ByVal index As Integer, ByVal menu As ContextMenuStrip)
        If menu Is Nothing Then Return
        If index >= 0 AndAlso index < _items.Count Then
            ScrollIntoView(index)
            Dim it As MediaItem = _items(index)
            menu.Show(it, New Point(it.Width \ 2, it.Height \ 2))
        Else
            menu.Show(Cursor.Position)
        End If
    End Sub

    ' ===== 鍵盤(VB6 miItem_ItemKeyDown) =====
    ' 焦點通常在某個項目的子控制項上,KeyDown 不會往上冒泡;ProcessCmdKey 會沿父鏈呼叫,所以在這裡攔。

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Const WM_KEYDOWN As Integer = &H100
        If msg.Msg = WM_KEYDOWN AndAlso _items.Count > 0 AndAlso _multiSelect AndAlso keyData = (Keys.Control Or Keys.A) Then
            SelectAll()
            Return True
        End If
        If msg.Msg = WM_KEYDOWN AndAlso _items.Count > 0 Then
            Dim e As New KeyEventArgs(keyData)
            Dim cur As Integer = SelectedIndex
            Dim target As Integer = NavigateTarget(cur, e.KeyCode)
            If target >= 0 AndAlso target <> cur Then
                SelectCore(target, SelectSource.Key, e, cur)
                Return True
            ElseIf cur >= 0 Then
                RaiseEvent ItemKeyDown(cur, e)
                If e.Handled OrElse target >= 0 Then Return True
            End If
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    ''' <summary>方向鍵等要移到的索引;不是導覽鍵傳回 -1。垂直捲動時左右 ±1、上下 ±每列數;
    ''' 水平捲動時上下 ±1、左右 ±每欄數;Home/PageUp 到第一項、End/PageDown 到最後一項。</summary>
    Private Function NavigateTarget(ByVal cur As Integer, ByVal key As Keys) As Integer
        Dim last As Integer = _items.Count - 1
        Dim per As Integer = Math.Max(1, _lastPerLine)
        Dim vertical As Boolean = (_scrollDirection = MediaScrollDirection.Vertical)
        Dim stepVal As Integer
        Select Case key
            Case Keys.Home, Keys.PageUp : Return 0
            Case Keys.End, Keys.PageDown : Return last
            Case Keys.Left : stepVal = If(vertical, -1, -per)
            Case Keys.Right : stepVal = If(vertical, 1, per)
            Case Keys.Up : stepVal = If(vertical, -per, -1)
            Case Keys.Down : stepVal = If(vertical, per, 1)
            Case Else : Return -1
        End Select
        If cur < 0 Then Return 0
        If HasSections() AndAlso vertical AndAlso (key = Keys.Up OrElse key = Keys.Down) Then Return RowNeighbour(cur, key = Keys.Down)
        If HasSections() AndAlso Not vertical AndAlso (key = Keys.Left OrElse key = Keys.Right) Then Return RowNeighbour(cur, key = Keys.Right)
        Return Math.Max(0, Math.Min(last, cur + stepVal))
    End Function

    ' ===== 主題外框 =====

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _themedBorder Then
            BorderPainter.DrawThemedBorder(e.Graphics, Width, Height, _borderColor, _borderFocusColor, _focused, parhelia:=True)
        End If
    End Sub

    Protected Overrides Sub OnEnter(e As EventArgs)
        _focused = True
        If _themedBorder Then Invalidate()
        MyBase.OnEnter(e)
    End Sub

    Protected Overrides Sub OnLeave(e As EventArgs)
        _focused = False
        If _themedBorder Then Invalidate()
        MyBase.OnLeave(e)
    End Sub

    ' ===== 版面 =====

    ' 依 ItemSize 與可視區大小,計算交叉軸(每列/每欄)可容納的項目數
    Private Function ComputePerLine() As Integer
        If _limit > 0 Then Return _limit
        Dim m As Integer = _itemMargin
        Dim avail As Integer
        If _scrollDirection = MediaScrollDirection.Vertical Then
            avail = _viewport.ClientSize.Width - m
            Return Math.Max(1, avail \ (_itemSize.Width + m))
        Else
            avail = _viewport.ClientSize.Height - m
            Return Math.Max(1, avail \ (_itemSize.Height + m))
        End If
    End Function

    ''' <summary>實際使用的項目大小:未設 Limit 時為 ItemSize;設了 Limit 時,讓交叉軸剛好排下
    ''' Limit 個,媒體區依 AspectRatio,再加上選取框/留白與底部面板(同 VB6 InitialMediaItemProperty)。</summary>
    Private Function EffectiveItemSize() As Size
        If _limit <= 0 Then Return _itemSize
        Dim m As Integer = _itemMargin
        Dim inset As Integer = MediaItem.SelectionBorderWidth + _itemBorderSize
        Dim bottom As Integer = If(_showCheckBox OrElse _showRating, MediaItem.BottomPanelHeight, 0)
        If _scrollDirection = MediaScrollDirection.Vertical Then
            Dim w As Integer = Math.Max(1, (_viewport.ClientSize.Width - m * (_limit + 1)) \ _limit)
            Dim photoW As Integer = Math.Max(1, w - inset * 2)
            Return New Size(w, photoW * _aspectH \ _aspectW + inset * 2 + bottom)
        Else
            Dim h As Integer = Math.Max(1, (_viewport.ClientSize.Height - m * (_limit + 1)) \ _limit)
            Dim photoH As Integer = Math.Max(1, h - inset * 2 - bottom)
            Return New Size(photoH * _aspectW \ _aspectH + inset * 2, h)
        End If
    End Function

    ' 計算第 index 個項目在 content 內的邏輯座標(perLine=交叉軸數量)
    Private Function ComputeCellBounds(ByVal index As Integer, ByVal perLine As Integer) As Rectangle
        Dim sz As Size = EffectiveItemSize()
        Dim w As Integer = sz.Width
        Dim h As Integer = sz.Height
        Dim m As Integer = _itemMargin
        Dim per As Integer = Math.Max(1, perLine)

        Dim cross As Integer = index Mod per   ' 交叉軸上的位置
        Dim main As Integer = index \ per      ' 主(捲動)軸上的位置

        Dim x As Integer, y As Integer
        If _scrollDirection = MediaScrollDirection.Vertical Then
            x = m + cross * (w + m)   ' 交叉軸=水平(欄)
            y = m + main * (h + m)    ' 主軸=垂直(列)
        Else
            x = m + main * (w + m)    ' 主軸=水平(欄)
            y = m + cross * (h + m)   ' 交叉軸=垂直(列)
        End If
        Return New Rectangle(x, y, w, h)
    End Function

    ' 重新配置所有項目(設定改變 / 縮放 / 移除後):只算邏輯座標;有視窗的那一段由
    ' UpdateScrollMetrics → ApplyScroll → UpdateRealized 擺到新位置
    Private Sub RelayoutItems()
        Dim perLine As Integer = ComputePerLine()
        _lastPerLine = perLine
        Dim maxR As Integer = 0, maxB As Integer = 0
        For i As Integer = 0 To _items.Count - 1
            Dim rc As Rectangle = CellBounds(i, perLine)   ' 依序算:有分段時接著前一項排
            _bounds(i) = rc
            maxR = Math.Max(maxR, rc.Right + _itemMargin)
            maxB = Math.Max(maxB, rc.Bottom + _itemMargin)
        Next
        _contentSize = If(_items.Count = 0, Size.Empty, New Size(maxR, maxB))
        _layoutVersion += 1   ' 有視窗的項目全部要重新擺
        UpdateScrollMetrics()
    End Sub

    ' ===== 分段標題 =====
    ' AddSection("標題") 之後加入的項目屬於這一段:從新的一列開始,上面畫一列標題(清單背景上畫,
    ' 不是控制項)。標題可用 Tab 分成兩部分:前面粗體、後面灰色,例如 "3月15日 星期五" & vbTab & "臺中市 · 12 張"。
    ' 水平捲動時每段從新的一欄開始,前面一欄(SectionHeaderWidth)畫標題,文字自動換行。Clear 會清掉所有分段。

    ''' <summary>按下某段的標題列(sectionIndex 從 0 起)。</summary>
    <Category("MediaList"), Description("按下分段標題時發生。")>
    Public Event SectionClick(ByVal sectionIndex As Integer, ByVal e As MouseEventArgs)

    ''' <summary>標題列高度(像素)。</summary>
    <Category("MediaList"), DefaultValue(34)>
    Public Property SectionHeaderHeight As Integer
        Get
            Return _sectionHeaderHeight
        End Get
        Set(value As Integer)
            value = Math.Max(12, value)
            If _sectionHeaderHeight = value Then Return
            _sectionHeaderHeight = value
            If _sections.Count > 0 Then RelayoutItems()
        End Set
    End Property

    ''' <summary>水平捲動時,每段前面標題欄的寬(像素);標題在欄裡自動換行。</summary>
    <Category("MediaList"), DefaultValue(68)>
    Public Property SectionHeaderWidth As Integer
        Get
            Return _sectionHeaderWidth
        End Get
        Set(value As Integer)
            value = Math.Max(24, value)
            If _sectionHeaderWidth = value Then Return
            _sectionHeaderWidth = value
            If _sections.Count > 0 Then RelayoutItems()
        End Set
    End Property

    ''' <summary>標題字型(Nothing = 清單字型加粗、放大一級)。</summary>
    <Category("MediaList"), DefaultValue(GetType(Font), Nothing)>
    Public Property SectionFont As Font
        Get
            Return _sectionFont
        End Get
        Set(value As Font)
            _sectionFont = value
            _viewport.Invalidate()
        End Set
    End Property

    <Category("MediaList")>
    Public Property SectionForeColor As Color
        Get
            Return _sectionForeColor
        End Get
        Set(value As Color)
            _sectionForeColor = value
            _viewport.Invalidate()
        End Set
    End Property

    ''' <summary>標題 Tab 後面那部分的顏色。</summary>
    <Category("MediaList")>
    Public Property SectionSubColor As Color
        Get
            Return _sectionSubColor
        End Get
        Set(value As Color)
            _sectionSubColor = value
            _viewport.Invalidate()
        End Set
    End Property

    ''' <summary>下一個加入的項目開始新的一段。連續呼叫兩次(中間沒有項目)時用後面的標題。</summary>
    Public Sub AddSection(ByVal title As String)
        _sections(_items.Count) = If(title, "")
    End Sub

    <Browsable(False)>
    Public ReadOnly Property SectionCount As Integer
        Get
            Return _sections.Count
        End Get
    End Property

    Public Function SectionTitle(ByVal sectionIndex As Integer) As String
        Return _sections.Values(sectionIndex)
    End Function

    ''' <summary>第 sectionIndex 段第一個項目的索引。</summary>
    Public Function SectionFirstIndex(ByVal sectionIndex As Integer) As Integer
        Return _sections.Keys(sectionIndex)
    End Function

    ''' <summary>第 sectionIndex 段最後一個項目的索引。</summary>
    Public Function SectionLastIndex(ByVal sectionIndex As Integer) As Integer
        Return If(sectionIndex + 1 < _sections.Count, _sections.Keys(sectionIndex + 1), _items.Count) - 1
    End Function

    ''' <summary>項目所在的段(-1 = 在第一段之前,或沒有分段)。</summary>
    Public Function SectionOfItem(ByVal index As Integer) As Integer
        Dim keys As IList(Of Integer) = _sections.Keys
        Dim lo As Integer = 0, hi As Integer = keys.Count - 1, found As Integer = -1
        While lo <= hi
            Dim mid As Integer = (lo + hi) \ 2
            If keys(mid) <= index Then
                found = mid
                lo = mid + 1
            Else
                hi = mid - 1
            End If
        End While
        Return found
    End Function

    ''' <summary>把第 sectionIndex 段的標題捲到最上面。</summary>
    Public Sub ScrollToSection(ByVal sectionIndex As Integer)
        If sectionIndex < 0 OrElse sectionIndex >= _sections.Count Then Return
        Dim first As Integer = _sections.Keys(sectionIndex)
        If first < _items.Count Then EnsureVisible(first)
    End Sub

    Private Function HasSections() As Boolean
        Return _sections.Count > 0
    End Function

    Private ReadOnly Property IsVertical As Boolean
        Get
            Return _scrollDirection = MediaScrollDirection.Vertical
        End Get
    End Property

    ''' <summary>標題佔主軸(捲動方向)的長度:垂直時是標題列的高,水平時是標題欄的寬。</summary>
    Private ReadOnly Property HeaderLen As Integer
        Get
            Return If(IsVertical, _sectionHeaderHeight, _sectionHeaderWidth)
        End Get
    End Property

    Private Function MainStart(ByVal rc As Rectangle) As Integer
        Return If(IsVertical, rc.Y, rc.X)
    End Function

    Private Function MainEnd(ByVal rc As Rectangle) As Integer
        Return If(IsVertical, rc.Bottom, rc.Right)
    End Function

    Private Function CrossStart(ByVal rc As Rectangle) As Integer
        Return If(IsVertical, rc.X, rc.Y)
    End Function

    ''' <summary>項目在它那一段的第一列(水平:第一欄)時,前面標題的長度(捲到它時要連標題一起露出來);否則 0。</summary>
    Private Function HeaderAbove(ByVal index As Integer) As Integer
        If Not HasSections() Then Return 0
        Dim s As Integer = SectionOfItem(index)
        If s < 0 Then Return 0
        Dim first As Integer = _sections.Keys(s)
        Return If(first < _bounds.Count AndAlso MainStart(_bounds(first)) = MainStart(_bounds(index)), HeaderLen, 0)
    End Function

    ''' <summary>第 index 項的邏輯座標。有分段時要依序算(接著 _bounds(index - 1) 排):每段從新的一列
    ''' (水平:新的一欄)開始,前面留標題的位置。</summary>
    Private Function CellBounds(ByVal index As Integer, ByVal perLine As Integer) As Rectangle
        If Not HasSections() Then Return ComputeCellBounds(index, perLine)
        Dim sz As Size = EffectiveItemSize()
        Dim m As Integer = _itemMargin
        Dim s As Integer = SectionOfItem(index)
        Dim first As Integer = If(s < 0, 0, _sections.Keys(s))
        Dim cross As Integer = (index - first) Mod Math.Max(1, perLine)
        Dim main As Integer
        If index = 0 Then
            main = If(first = 0 AndAlso s >= 0, HeaderLen, m)
        ElseIf index = first AndAlso s >= 0 Then
            main = MainEnd(_bounds(index - 1)) + m + HeaderLen
        ElseIf cross = 0 Then
            main = MainEnd(_bounds(index - 1)) + m
        Else
            main = MainStart(_bounds(index - 1))
        End If
        If IsVertical Then Return New Rectangle(m + cross * (sz.Width + m), main, sz.Width, sz.Height)
        Return New Rectangle(main, m + cross * (sz.Height + m), sz.Width, sz.Height)
    End Function

    ''' <summary>有分段時各列(欄)長短不一:用二分搜尋找顯示區域(加前後一列)涵蓋的項目。</summary>
    Private Sub VisibleRangeBySearch(ByRef first As Integer, ByRef last As Integer)
        Dim sz As Size = EffectiveItemSize()
        Dim lineLen As Integer = If(IsVertical, sz.Height, sz.Width) + _itemMargin
        Dim lowEdge As Integer = _scrollOffset - lineLen
        Dim highEdge As Integer = _scrollOffset + ViewLen() + lineLen
        Dim n As Integer = _bounds.Count
        ' 第一個結束 >= lowEdge
        Dim lo As Integer = 0, hi As Integer = n
        While lo < hi
            Dim mid As Integer = (lo + hi) \ 2
            If MainEnd(_bounds(mid)) >= lowEdge Then hi = mid Else lo = mid + 1
        End While
        first = lo
        ' 最後一個開始 <= highEdge
        lo = 0 : hi = n
        While lo < hi
            Dim mid As Integer = (lo + hi) \ 2
            If MainStart(_bounds(mid)) <= highEdge Then lo = mid + 1 Else hi = mid
        End While
        last = lo - 1
        If first >= n OrElse last < first Then first = -1 : last = -1
    End Sub

    ''' <summary>前／後一列(水平:一欄)裡交叉軸位置最接近的項目(有分段時列的長短不一)。</summary>
    Private Function RowNeighbour(ByVal cur As Integer, ByVal forward As Boolean) As Integer
        Dim rcMain As Integer = MainStart(_bounds(cur)), rcCross As Integer = CrossStart(_bounds(cur))
        Dim last As Integer = _items.Count - 1
        If forward Then
            Dim j As Integer = cur + 1
            While j <= last AndAlso MainStart(_bounds(j)) = rcMain : j += 1 : End While
            If j > last Then Return cur
            Dim lineAt As Integer = MainStart(_bounds(j)), best As Integer = j
            While j <= last AndAlso MainStart(_bounds(j)) = lineAt
                If CrossStart(_bounds(j)) <= rcCross Then best = j
                j += 1
            End While
            Return best
        Else
            Dim j As Integer = cur - 1
            While j >= 0 AndAlso MainStart(_bounds(j)) = rcMain : j -= 1 : End While
            If j < 0 Then Return cur
            Dim lineAt As Integer = MainStart(_bounds(j)), best As Integer = j
            While j >= 0 AndAlso MainStart(_bounds(j)) = lineAt
                If CrossStart(_bounds(j)) <= rcCross Then
                    best = j
                    Exit While
                End If
                j -= 1
            End While
            Return best
        End If
    End Function

    ''' <summary>移除第 index 項後:之後的分段往前移一格;變成空的分段拿掉。</summary>
    Private Sub ShiftSectionsAfterRemove(ByVal index As Integer)
        If _sections.Count = 0 Then Return
        Dim moved As New List(Of KeyValuePair(Of Integer, String))
        For Each kv In _sections
            moved.Add(If(kv.Key > index, New KeyValuePair(Of Integer, String)(kv.Key - 1, kv.Value), kv))
        Next
        _sections.Clear()
        For Each kv In moved
            _sections(kv.Key) = kv.Value   ' 同一個起點(前一段空了):後面那段留下
        Next
        ' 最後一段沒有項目了
        While _sections.Count > 0 AndAlso _sections.Keys(_sections.Count - 1) >= _items.Count AndAlso _items.Count > 0
            _sections.RemoveAt(_sections.Count - 1)
        End While
        _viewport.Invalidate()
    End Sub

    ''' <summary>標題在畫面上的位置(viewport 座標):垂直時是一列,水平時是一欄;該段沒有項目時 Rectangle.Empty。</summary>
    Private Function SectionHeaderRect(ByVal sectionIndex As Integer) As Rectangle
        Dim first As Integer = _sections.Keys(sectionIndex)
        If first >= _bounds.Count Then Return Rectangle.Empty
        Dim at As Integer = MainStart(_bounds(first)) - HeaderLen - _scrollOffset
        If IsVertical Then Return New Rectangle(0, at, _viewport.ClientSize.Width, _sectionHeaderHeight)
        Return New Rectangle(at, 0, _sectionHeaderWidth, _viewport.ClientSize.Height)
    End Function

    Private Sub PaintSections(sender As Object, e As PaintEventArgs)
        If Not HasSections() Then Return
        Dim bold As Font = If(_sectionFont, New Font(Me.Font.FontFamily, Me.Font.Size + 1.5F, FontStyle.Bold))
        Try
            Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix Or TextFormatFlags.EndEllipsis
            ' a column (horizontal): the title wraps, main part then the grey part, from the top
            Dim colFlags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix
            For s As Integer = 0 To _sections.Count - 1
                Dim r As Rectangle = SectionHeaderRect(s)
                If r.IsEmpty Then Continue For
                If IsVertical Then
                    If r.Bottom < e.ClipRectangle.Top Then Continue For
                    If r.Top > e.ClipRectangle.Bottom Then Exit For
                Else
                    If r.Right < e.ClipRectangle.Left Then Continue For
                    If r.Left > e.ClipRectangle.Right Then Exit For
                End If
                Dim title As String = _sections.Values(s)
                Dim tab As Integer = title.IndexOf(ControlChars.Tab)
                Dim main As String = If(tab < 0, title, title.Substring(0, tab))
                If IsVertical Then
                    Dim text As New Rectangle(_itemMargin + 4, r.Y + 4, r.Width - _itemMargin * 2 - 8, r.Height - 4)
                    TextRenderer.DrawText(e.Graphics, main, bold, text, _sectionForeColor, flags)
                    If tab >= 0 Then
                        Dim w As Integer = TextRenderer.MeasureText(e.Graphics, main, bold, text.Size, flags).Width
                        Dim subRect As New Rectangle(text.X + w + 10, text.Y, Math.Max(0, text.Width - w - 10), text.Height)
                        TextRenderer.DrawText(e.Graphics, title.Substring(tab + 1), Me.Font, subRect, _sectionSubColor, flags)
                    End If
                Else
                    ' a narrow column: one line per part -- after 年 and at each space ("2024年 / 3月16日 / 星期六")
                    main = main.Replace("年", "年" & vbLf).Replace(" ", vbLf).Replace(vbLf & vbLf, vbLf).Trim(ControlChars.Lf)
                    Dim inner As New Rectangle(r.X + 3, r.Y + _itemMargin + 2, r.Width - 6, r.Height - _itemMargin * 2 - 4)
                    Dim h As Integer = TextRenderer.MeasureText(e.Graphics, main, Me.Font, New Size(inner.Width, Integer.MaxValue), colFlags).Height
                    TextRenderer.DrawText(e.Graphics, main, Me.Font, New Rectangle(inner.X, inner.Y, inner.Width, Math.Min(h, inner.Height)), _sectionForeColor, colFlags)
                    If tab >= 0 AndAlso h + 4 < inner.Height Then
                        TextRenderer.DrawText(e.Graphics, title.Substring(tab + 1), Me.Font, New Rectangle(inner.X, inner.Y + h + 4, inner.Width, inner.Height - h - 4), _sectionSubColor, colFlags)
                    End If
                    Using p As New Pen(Color.FromArgb(70, _sectionSubColor))
                        e.Graphics.DrawLine(p, r.X, r.Y + _itemMargin, r.X, r.Bottom - _itemMargin)   ' where the day starts
                    End Using
                End If
            Next
        Finally
            If _sectionFont Is Nothing Then bold.Dispose()
        End Try
    End Sub
    Private Sub ViewportMouseDown(sender As Object, e As MouseEventArgs)
        If Not HasSections() Then Return
        For s As Integer = 0 To _sections.Count - 1
            Dim r As Rectangle = SectionHeaderRect(s)
            If Not r.IsEmpty AndAlso r.Contains(e.Location) Then
                RaiseEvent SectionClick(s, e)
                Return
            End If
        Next
    End Sub

    ' ===== 延遲載入 =====

    ' 縮放:每列數量可能改變 → 重排;否則重算捲軸並重查可視載入
    Private Sub OnViewportResized(sender As Object, e As EventArgs)
        If _limit > 0 OrElse ComputePerLine() <> _lastPerLine Then   ' Limit 模式下項目大小隨可視區改變
            RelayoutItems()
        Else
            UpdateScrollMetrics()
        End If
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        UpdateScrollMetrics()
    End Sub

    ' ===== 虛擬化:哪些項目有視窗 =====

    ''' <summary>顯示區域加前後各一列(預載,捲動時較不會看到空白)涵蓋的項目索引;
    ''' 由捲動位置直接算出,不必逐一檢查全部項目。沒有時兩者都是 -1。</summary>
    Private Sub VisibleRange(ByRef first As Integer, ByRef last As Integer)
        first = -1 : last = -1
        If _items.Count = 0 Then Return
        If HasSections() Then
            VisibleRangeBySearch(first, last)
            Return
        End If
        Dim sz As Size = EffectiveItemSize()
        Dim m As Integer = _itemMargin
        Dim per As Integer = Math.Max(1, _lastPerLine)
        Dim lineLen As Integer = Math.Max(1, If(_scrollDirection = MediaScrollDirection.Vertical, sz.Height, sz.Width) + m)
        Dim firstLine As Integer = Math.Max(0, (_scrollOffset - lineLen - m) \ lineLen)
        Dim lastLine As Integer = Math.Max(0, (_scrollOffset + ViewLen() + lineLen - m) \ lineLen)
        first = firstLine * per
        last = Math.Min(_items.Count - 1, (lastLine + 1) * per - 1)
        If first > last Then first = -1 : last = -1
    End Sub

    ''' <summary>讓顯示區域附近的項目有視窗並擺到畫面位置(邏輯座標減捲動位移)、載入它們的媒體,
    ''' 離開的項目釋放視窗。捲動、版面改變、新增項目後呼叫。</summary>
    Private Sub UpdateRealized()
        If Not _viewport.IsHandleCreated Then Return
        Dim nf As Integer, nl As Integer
        VisibleRange(nf, nl)

        Dim oldFirst As Integer = _realFirst, oldLast As Integer = _realLast
        If oldFirst >= 0 Then
            For i As Integer = oldFirst To Math.Min(oldLast, _items.Count - 1)
                If i < nf OrElse i > nl Then Release(_items(i))
            Next
        End If
        _realFirst = nf : _realLast = nl
        If nf < 0 Then Return

        ' Nothing moved (same scroll position and layout, e.g. while photos are being added below):
        ' only the items that just entered the range need placing; the rest are already in place.
        Dim moved As Boolean = (_placedOffset <> _scrollOffset OrElse _placedLayout <> _layoutVersion OrElse oldFirst < 0)
        Dim from As Integer = nf, [to] As Integer = nl
        If Not moved Then
            If nf >= oldFirst AndAlso nl <= oldLast Then Return   ' nothing new either
            If nf >= oldFirst AndAlso nf <= oldLast Then from = oldLast + 1   ' the usual case: new ones at the end
        End If
        _placedOffset = _scrollOffset
        _placedLayout = _layoutVersion

        Dim dx As Integer = If(_scrollDirection = MediaScrollDirection.Vertical, 0, -_scrollOffset)
        Dim dy As Integer = If(_scrollDirection = MediaScrollDirection.Vertical, -_scrollOffset, 0)
        ' 一次移動數十個子視窗:先停止重繪,全部就位後整塊重畫一次(避免移動中的撕裂/閃爍)。
        ' 只是加上新進範圍的項目時不必:它們出現時自己會畫。
        If moved Then SendMessage(_viewport.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero)
        Try
            For i As Integer = from To [to]
                Dim it As MediaItem = _items(i)
                Dim rc As Rectangle = _bounds(i)
                it.SetBounds(rc.X + dx, rc.Y + dy, rc.Width, rc.Height)   ' 先定大小,載入的縮圖才會是這個大小
                Dim entering As Boolean = it.Parent IsNot _viewport
                If entering Then _viewport.Controls.Add(it)
                Dim fn As String = Nothing
                If _pending.TryGetValue(it, fn) Then
                    _pending.Remove(it)
                    it.FileName = fn   ' 第一次:開始(背景)載入
                ElseIf entering Then
                    it.ReloadMedia()   ' 捲出去時釋放過縮圖:取回(最近的由快取直接給)
                End If
            Next
        Finally
            If moved Then
                SendMessage(_viewport.Handle, WM_SETREDRAW, New IntPtr(1), IntPtr.Zero)
                _viewport.Invalidate(True)
            End If
        End Try
    End Sub

    ''' <summary>項目離開顯示區域:移出 viewport 並釋放它的視窗(資料、縮圖保留)。</summary>
    Private Sub Release(ByVal it As MediaItem)
        If it.Parent IsNot _viewport Then Return
        If it.ContainsFocus Then _viewport.Focus()   ' 焦點留在清單內,方向鍵導覽才不會斷
        _viewport.Controls.Remove(it)
        it.ReleaseWindow()
        it.UnloadMedia()   ' 縮圖也釋放:捲過的相片不再全部留在記憶體(ThumbnailLoader 快取最近用過的)
    End Sub

    ''' <summary>釋放目前所有有視窗的項目(移除 / 清空前:之後索引就不對了)。</summary>
    Private Sub ReleaseAll()
        If _realFirst >= 0 Then
            For i As Integer = _realFirst To Math.Min(_realLast, _items.Count - 1)
                Release(_items(i))
            Next
        End If
        _realFirst = -1 : _realLast = -1
        _placedOffset = -1
    End Sub

    Private Const WM_SETREDRAW As Integer = &HB
    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SendMessage(ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
    End Function

    ''' <summary>可以取得焦點的 Panel:項目捲出畫面、視窗被釋放時,焦點移到這裡(仍在清單內)。</summary>
    Private Class FocusPanel
        Inherits System.Windows.Forms.Panel
        Public Sub New()
            SetStyle(ControlStyles.Selectable, True)
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True)   ' 分段標題捲動時不閃
            TabStop = False
        End Sub
    End Class

    ' ===== 項目事件轉發 =====

    Private Sub HookItem(ByVal item As MediaItem)
        Dim relay As New ItemEventRelay(Me, item)
        _relays(item) = relay
        AddHandler item.MediaClicked, AddressOf relay.OnClick
        AddHandler item.CheckedChanged, AddressOf relay.OnChecked
        AddHandler item.RatingChanged, AddressOf relay.OnRating
        AddHandler item.DropMedia, AddressOf relay.OnDrop
        AddHandler item.MarkedChanged, AddressOf relay.OnMarked
        AddHandler item.MediaMouseDown, AddressOf relay.OnMouseDown
        AddHandler item.MediaDoubleClick, AddressOf relay.OnDoubleClick
        AddHandler item.DragCompleted, AddressOf relay.OnDragCompleted
    End Sub

    Private Sub UnhookItem(ByVal item As MediaItem)
        Dim relay As ItemEventRelay = Nothing
        If _relays.TryGetValue(item, relay) Then
            RemoveHandler item.MediaClicked, AddressOf relay.OnClick
            RemoveHandler item.CheckedChanged, AddressOf relay.OnChecked
            RemoveHandler item.RatingChanged, AddressOf relay.OnRating
            RemoveHandler item.DropMedia, AddressOf relay.OnDrop
            RemoveHandler item.MarkedChanged, AddressOf relay.OnMarked
            RemoveHandler item.MediaMouseDown, AddressOf relay.OnMouseDown
            RemoveHandler item.MediaDoubleClick, AddressOf relay.OnDoubleClick
            RemoveHandler item.DragCompleted, AddressOf relay.OnDragCompleted
            _relays.Remove(item)
        End If
    End Sub

    Friend Sub RaiseItemMarkChanged(ByVal item As MediaItem)
        Dim idx As Integer = _items.IndexOf(item)
        If idx >= 0 Then RaiseEvent ItemMarkChanged(idx)
    End Sub

    Friend Sub RaiseItemMouseDown(ByVal item As MediaItem, ByVal e As MouseEventArgs)
        Dim idx As Integer = _items.IndexOf(item)
        If idx < 0 Then Return
        ' 滑鼠中鍵 = Ctrl+左鍵:加入或移出選取(MultiSelect 時)
        If e.Button = MouseButtons.Middle AndAlso _multiSelect Then SelectByClick(item, toggle:=True)
        RaiseEvent ItemMouseDown(idx, e)
    End Sub

    ' VB6 miItem_ItemDblClick:選取該項(不引發選取事件)→ ItemDblClick → 清單的 DblClick
    Friend Sub RaiseItemDblClick(ByVal item As MediaItem)
        Dim idx As Integer = _items.IndexOf(item)
        If idx < 0 Then Return
        If _selectedItem IsNot item Then
            ' inside a multi-selection it stays; otherwise the double-clicked item alone
            If Not _multi.Contains(item) Then SetOnly(item)
            _multi.Add(item)
            _selectedItem = item
            item.Selected = True
            _anchor = item
            RaiseEvent SelectedItemChanged(item)
        End If
        RaiseEvent ItemDblClick(idx)
        MyBase.OnDoubleClick(EventArgs.Empty)
    End Sub

    Friend Sub RaiseItemCompleteDrag(ByVal item As MediaItem, ByVal effect As DragDropEffects)
        Dim idx As Integer = _items.IndexOf(item)
        If idx >= 0 Then RaiseEvent ItemCompleteDrag(idx, effect)
    End Sub

    Friend Sub RaiseItemCheckedChanged(ByVal item As MediaItem)
        RaiseEvent ItemCheckedChanged(item)
    End Sub

    Friend Sub RaiseItemRatingChanged(ByVal item As MediaItem, ByVal newRating As Integer)
        RaiseEvent ItemRatingChanged(item, newRating)
    End Sub

    Friend Sub RaiseItemDropMedia(ByVal item As MediaItem, ByVal fileName As String)
        RaiseEvent ItemDropMedia(item, fileName)
    End Sub

    ' 每個項目一個轉發器,持有 owner 與 item 參考,將無 sender 的事件補回來源項目
    Private Class ItemEventRelay
        Private ReadOnly _owner As MediaList
        Private ReadOnly _item As MediaItem
        Public Sub New(ByVal owner As MediaList, ByVal item As MediaItem)
            _owner = owner
            _item = item
        End Sub
        Public Sub OnClick(ByVal sender As Object, ByVal e As MediaClickedEventArgs)
            _owner.SelectByClick(_item)   ' 點擊即單選(選取流程內會引發 ItemClick)
        End Sub
        Public Sub OnMarked(ByVal sender As Object, ByVal e As EventArgs)
            _owner.RaiseItemMarkChanged(_item)
        End Sub
        Public Sub OnMouseDown(ByVal sender As Object, ByVal e As MouseEventArgs)
            _owner.RaiseItemMouseDown(_item, e)
        End Sub
        Public Sub OnDoubleClick(ByVal sender As Object, ByVal e As EventArgs)
            _owner.RaiseItemDblClick(_item)
        End Sub
        Public Sub OnDragCompleted(ByVal effect As DragDropEffects)
            _owner.RaiseItemCompleteDrag(_item, effect)
        End Sub
        Public Sub OnChecked(ByVal sender As Object, ByVal e As EventArgs)
            _owner.RaiseItemCheckedChanged(_item)
        End Sub
        Public Sub OnRating(ByVal newRating As Integer)
            _owner.RaiseItemRatingChanged(_item, newRating)
        End Sub
        Public Sub OnDrop(ByVal fileName As String)
            _owner.RaiseItemDropMedia(_item, fileName)
        End Sub
    End Class

    ' 現代浮層式細捲軸:圓角浮動滑塊(Region 讓非滑塊區可穿透點選),閒置自動隱藏
    Private Class ThinScrollBar
        Inherits Control

        Public Event ValueChanged(ByVal newValue As Integer)

        Private Const Thickness As Integer = 10
        Private Const ThumbPad As Integer = 2      ' 滑塊與邊之間留白
        Private Const MinThumb As Integer = 28
        Private Const HoldTicks As Integer = 30    ' 閒置約 1 秒後隱藏

        Private _orientation As System.Windows.Forms.Orientation = System.Windows.Forms.Orientation.Vertical
        Private _thumbColor As Color = Color.FromArgb(120, 170, 230)
        Private _content As Integer = 0
        Private _view As Integer = 0
        Private _value As Integer = 0

        Private ReadOnly _timer As New Timer()
        Private _idle As Integer = 0
        Private _hoverThumb As Boolean = False
        Private _dragging As Boolean = False
        Private _dragOffset As Integer = 0

        Public Sub New()
            Me.SetStyle(ControlStyles.UserPaint Or
                        ControlStyles.AllPaintingInWmPaint Or
                        ControlStyles.OptimizedDoubleBuffer, True)
            Me.Visible = False
            _timer.Interval = 33
            AddHandler _timer.Tick, AddressOf OnTick
        End Sub

        Public Property Orientation As System.Windows.Forms.Orientation
            Get
                Return _orientation
            End Get
            Set(value As System.Windows.Forms.Orientation)
                _orientation = value
                UpdateThumbRegion()
                Invalidate()
            End Set
        End Property

        Public Property ThumbColor As Color
            Get
                Return _thumbColor
            End Get
            Set(value As Color)
                _thumbColor = value
                Invalidate()
            End Set
        End Property

        Public ReadOnly Property BarThickness As Integer
            Get
                Return Thickness
            End Get
        End Property

        Public Property Value As Integer
            Get
                Return _value
            End Get
            Set(v As Integer)
                v = ClampValue(v)
                If _value <> v Then
                    _value = v
                    UpdateThumbRegion()
                    ShowNow()
                    RaiseEvent ValueChanged(_value)
                End If
            End Set
        End Property

        ' 設定內容與可視長度;不可捲動時隱藏
        Public Sub SetRange(ByVal contentLen As Integer, ByVal viewLen As Integer)
            _content = Math.Max(0, contentLen)
            _view = Math.Max(0, viewLen)
            _value = ClampValue(_value)
            Dim scrollable As Boolean = (_content > _view AndAlso _view > 0)
            If scrollable Then
                If Not Me.Visible Then Me.Visible = True
                UpdateThumbRegion()
                ShowNow()
            Else
                Me.Visible = False
                _timer.Stop()
            End If
            Invalidate()
        End Sub

        Private Function RangePx() As Integer
            Return Math.Max(0, _content - _view)
        End Function

        Private Function ClampValue(ByVal v As Integer) As Integer
            Dim r As Integer = RangePx()
            If v < 0 Then Return 0
            If v > r Then Return r
            Return v
        End Function

        Private Function TrackLen() As Integer
            Return If(_orientation = System.Windows.Forms.Orientation.Vertical, Me.Height, Me.Width)
        End Function

        Private Function ThumbLen() As Integer
            Dim tl As Integer = TrackLen()
            If _content <= 0 OrElse _view <= 0 OrElse _content <= _view Then Return tl
            Return Math.Max(MinThumb, CInt(CLng(tl) * _view \ _content))
        End Function

        Private Function ThumbPos() As Integer
            Dim travel As Integer = TrackLen() - ThumbLen()
            Dim r As Integer = RangePx()
            If r <= 0 Then Return 0
            Return CInt(CLng(travel) * _value \ r)
        End Function

        Private Function ThumbRect() As Rectangle
            Dim pos As Integer = ThumbPos()
            Dim len As Integer = ThumbLen()
            If _orientation = System.Windows.Forms.Orientation.Vertical Then
                Return New Rectangle(ThumbPad, pos + ThumbPad, Thickness - ThumbPad * 2, len - ThumbPad * 2)
            Else
                Return New Rectangle(pos + ThumbPad, ThumbPad, len - ThumbPad * 2, Thickness - ThumbPad * 2)
            End If
        End Function

        Private Sub UpdateThumbRegion()
            If Not Me.IsHandleCreated Then Return
            Dim rc As Rectangle = ThumbRect()
            If rc.Width <= 0 OrElse rc.Height <= 0 Then
                Me.Region = New Region(New Rectangle(0, 0, 0, 0))
                Return
            End If
            Using path As Drawing2D.GraphicsPath = RoundedRect(rc, Math.Min(rc.Width, rc.Height) \ 2)
                Me.Region = New Region(path)
            End Using
        End Sub

        Private Shared Function RoundedRect(ByVal r As Rectangle, ByVal radius As Integer) As Drawing2D.GraphicsPath
            Dim d As Integer = Math.Max(1, radius * 2)
            Dim gp As New Drawing2D.GraphicsPath()
            gp.AddArc(r.X, r.Y, d, d, 180, 90)
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            gp.CloseFigure()
            Return gp
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim rc As Rectangle = ThumbRect()
            If rc.Width <= 0 OrElse rc.Height <= 0 Then Return
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Dim c As Color = If(_dragging OrElse _hoverThumb,
                                ControlPaint.Dark(_thumbColor, 0.05F), _thumbColor)
            Using b As New SolidBrush(c)
                Using path As Drawing2D.GraphicsPath = RoundedRect(rc, Math.Min(rc.Width, rc.Height) \ 2)
                    e.Graphics.FillPath(b, path)
                End Using
            End Using
        End Sub

        ' 顯示並重置閒置計時
        Private Sub ShowNow()
            _idle = 0
            If Not Me.Visible AndAlso (_content > _view AndAlso _view > 0) Then Me.Visible = True
            If Not _timer.Enabled Then _timer.Start()
            Invalidate()
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            If _dragging OrElse _hoverThumb Then
                _idle = 0
                Return
            End If
            _idle += 1
            If _idle >= HoldTicks Then
                Me.Visible = False     ' 閒置後隱藏(內容完全露出)
                _timer.Stop()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                _dragging = True
                _dragOffset = If(_orientation = System.Windows.Forms.Orientation.Vertical, e.Y - ThumbPos(), e.X - ThumbPos())
                ShowNow()
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            _hoverThumb = True
            If _dragging Then
                Dim travel As Integer = TrackLen() - ThumbLen()
                If travel > 0 Then
                    Dim pos As Integer = If(_orientation = System.Windows.Forms.Orientation.Vertical, e.Y, e.X) - _dragOffset
                    pos = Math.Max(0, Math.Min(travel, pos))
                    Me.Value = CInt(CLng(pos) * RangePx() \ travel)
                End If
            End If
            ShowNow()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverThumb = False
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            UpdateThumbRegion()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            UpdateThumbRegion()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then _timer.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Class

End Namespace
