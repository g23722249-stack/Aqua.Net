Option Strict Off
Imports System.ComponentModel
Imports System.IO
Imports System.Drawing
Imports System.Windows.Forms

'用來顯示 WADO 下載的圖片，進階版為了搭配圖譜使用 WEB CAM 的錄影，增加可以顯示影片功能
Namespace Global.Aqua

<DefaultEvent("MediaClicked")>
Public Class MediaViewerControl
    Inherits UserControl
    Implements System.Windows.Forms.IMessageFilter

    Private _fileName As String
    Private _realSize As Size
    Private _originalSize As Size

    ' 目前載入的是否為影片檔
    Private _isVideo As Boolean

    ' 影片播放器；採延遲建立，只有使用者點擊縮圖要求播放時才建立。
    ' net8 走 LibVLCSharp，.NET Framework 走 Windows Media Player COM。
#If NETFRAMEWORK Then
    Private wmpPlayer As AxWMPLib.AxWindowsMediaPlayer
#Else
    Private _mediaPlayer As LibVLCSharp.Shared.MediaPlayer
    Private _videoView As LibVLCSharp.WinForms.VideoView
    ' 所有檢視器共用一個 LibVLC(建立時要載入全部外掛,第一次約 3 秒、之後每次也要 0.3 秒),
    ' 在背景預先建立(PreloadVideoEngine),點擊播放時就不必在 UI 執行緒上等。程式結束才釋放。
    Private Shared ReadOnly _vlcLock As New Object
    Private Shared _vlcTask As System.Threading.Tasks.Task(Of LibVLCSharp.Shared.LibVLC)
#End If

    ' 影片副檔名
    Private Shared ReadOnly ImageExts As String() = {".jpg", ".jpeg", ".png", ".bmp", ".gif"}
    Private Shared ReadOnly VideoExts As String() = {".mp4", ".avi", ".mov", ".mpg", ".mpeg", ".wmv",
                                                     ".dat", ".rm", ".m2p", ".divx", ".3gp", ".m2ts", ".asf", ".mkv"}

    ''' <summary>點擊媒體時觸發，參數帶出目前檔名。</summary>
    Public Event MediaClicked As EventHandler(Of MediaClickedEventArgs)

    ''' <summary>有媒體被拖放到本控制項時觸發，帶出來源檔名。</summary>
    Public Event DropMedia(ByVal FileName As String)

    ' 拖放:把目前媒體拖出時,以縮圖(同尺寸、80% 透明)跟隨鼠標,像整張圖被拖曳
    Private Const MediaDragFormat As String = "MediaViewerFile"
    Private _dragStart As Point
    Private _grabOffset As Size
    Private _dragImageForm As DragImageForm
    Private _thumb As Image    ' 保留一份縮圖(播放中背景圖已釋放時,拖曳仍可用)
    Private _mfPressed As Boolean   ' 訊息過濾器:是否在本控制項上按住左鍵(用於影片畫面拖曳)

    ' 觸發 MediaClicked 事件
    Private Sub RaiseMediaClicked()
        RaiseEvent MediaClicked(Me, New MediaClickedEventArgs(_fileName))
    End Sub

    Public Sub New()
        InitializeComponent()

        _originalSize = Me.Size

        ' 縮圖階段點擊 → 開始播放影片
        ' 用 MouseClick 只接左鍵:WinForms 的 Click 連右鍵放開也會觸發,那時已彈出的右鍵選單
        ' (AquaMenu 是獨立的作用中視窗)會因清單被點選、搶回焦點而立刻關閉
        AddHandler Me.MouseClick, AddressOf OnControlMouseClick

        ' 拖放:來源(拖出)與目標(拖入)
        Me.AllowDrop = True
        AddHandler Me.MouseDown, AddressOf OnMediaMouseDown
        AddHandler Me.MouseMove, AddressOf OnMediaMouseMove
        AddHandler Me.GiveFeedback, AddressOf OnMediaGiveFeedback
        AddHandler Me.DragEnter, AddressOf OnMediaDragEnter
        AddHandler Me.DragDrop, AddressOf OnMediaDragDrop

        ' 影片畫面(VLC 原生視窗)不會冒 WinForms 滑鼠事件,改用執行緒訊息過濾器偵測拖曳;
        ' 只在有視窗時註冊(見 OnHandleCreated):MediaList 只替看得到的項目建立視窗,
        ' 否則上千個項目就是上千個過濾器,每則訊息都要逐一經過
        AddHandler Me.Disposed, Sub(s, ev) _regrowTimer.Dispose()
    End Sub

    Private _filterAdded As Boolean

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

    ''' <summary>停止播放中的影片並換回縮圖(MediaList 釋放捲出畫面的項目視窗前呼叫:
    ''' 播放器綁在原生視窗上)。沒有在播放時不做事。</summary>
    Friend Sub StopPlayback()
#If NETFRAMEWORK Then
        If wmpPlayer Is Nothing Then Return
#Else
        If _mediaPlayer Is Nothing AndAlso _videoView Is Nothing Then Return
#End If
        DisposePlayer()
        LoadMedia()
    End Sub

        ' 屬性：檔案名稱
        Public Property FileName As String
            Get
                Return _fileName
            End Get
            Set(value As String)
                _fileName = value
                LoadMedia()
            End Set
        End Property

        ' 屬性：檔案的實際解析度
        Public ReadOnly Property RealSize As Size
            Get
                Return _realSize
            End Get
        End Property

        ' 載入圖片或影片
        Private Sub LoadMedia()
            ' 先釋放前一次的資源，避免 GDI / 播放器洩漏
            DisposePlayer()
            DisposeBackgroundImage()
            SetThumb(Nothing)
            _isVideo = False
            _loadGen += 1        ' 還在排隊的上一個背景載入作廢
            _unloaded = False

            If String.IsNullOrEmpty(_fileName) OrElse Not File.Exists(_fileName) Then
                _realSize = Size.Empty
                Return
            End If

            Dim ext As String = Path.GetExtension(_fileName).ToLower()
            If Array.IndexOf(ImageExts, ext) >= 0 Then
                ' 圖片顯示:只保留「符合控制項大小」的縮小版(原始尺寸記在 RealSize)。
                ' 以前整張原圖(外加一份 _thumb 複本)留在記憶體,一個相簿幾十張大照片就會把
                ' 32 位元程式的記憶體耗盡,後面的縮圖載入失敗而空白。控制項放大時再重新產生(見 OnSizeChanged)。
                If _loadAsync Then
                    RequestThumbnail(video:=False)
                    Return
                End If
                Dim img As Image = LoadDisplayImage(_fileName, DisplayBox(), _realSize)
                If img Is Nothing Then Return
                Me.BackgroundImage = img
                Me.BackgroundImageLayout = ImageLayout.Zoom
            ElseIf Array.IndexOf(VideoExts, ext) >= 0 Then
                ' 影片：先以 Shell 縮圖顯示 (與檔案總管相同)，點擊後才播放
                _isVideo = True
                PreloadVideoEngine()      ' 顯示縮圖時就在背景備好播放引擎,點擊後立即播放
                If _loadAsync Then
                    RequestThumbnail(video:=True)
                    Return
                End If
                Dim thumb As Bitmap = ShellThumbnail.GetThumbnail(_fileName, Me.Width, Me.Height)
                If thumb IsNot Nothing Then
                    _realSize = thumb.Size
                    Me.BackgroundImage = thumb
                    Me.BackgroundImageLayout = ImageLayout.Zoom
                Else
                    _realSize = Me.Size
                End If
            End If
        End Sub

        ' ===== 背景載入(MediaList 的項目用;見 ThumbnailLoader) =====

        Private _loadAsync As Boolean
        Private _loadGen As Integer          ' 每次換檔 / 卸載 / 重新產生加一:舊的背景請求就作廢
        Private _unloaded As Boolean         ' UnloadImage 釋放了縮圖,再顯示時要重新取得

        ''' <summary>縮圖在背景執行緒產生(先空白,好了再顯示),不卡住畫面執行緒。MediaList 的項目用;
        ''' 單獨使用(看片視窗)時維持同步載入。</summary>
        Friend Property LoadAsynchronously As Boolean
            Get
                Return _loadAsync
            End Get
            Set(value As Boolean)
                _loadAsync = value
            End Set
        End Property

        Private Sub RequestThumbnail(ByVal video As Boolean)
            Dim box As Size = If(video, New Size(Math.Max(1, Me.Width), Math.Max(1, Me.Height)), DisplayBox())
            Dim real As Size
            Dim cached As Bitmap = ThumbnailLoader.TryGetCached(_fileName, box, real)
            If cached IsNot Nothing Then
                ApplyThumbnail(cached, real)
            Else
                ThumbnailLoader.Enqueue(Me, _fileName, box, video, _loadGen)
            End If
        End Sub

        ''' <summary>背景執行緒詢問:這個請求還算數嗎(沒換檔、沒捲走、沒釋放)。</summary>
        Friend Function IsCurrentLoad(ByVal generation As Integer) As Boolean
            Return Not IsDisposed AndAlso Threading.Thread.VolatileRead(_loadGen) = generation
        End Function

        ''' <summary>背景產生的縮圖送達(畫面執行緒)。</summary>
        Friend Sub DeliverThumbnail(ByVal generation As Integer, ByVal bmp As Bitmap, ByVal realSize As Size)
            If IsDisposed OrElse generation <> _loadGen Then
                bmp.Dispose()
                Return
            End If
            ApplyThumbnail(bmp, realSize)
        End Sub

        Private Sub ApplyThumbnail(ByVal bmp As Bitmap, ByVal realSize As Size)
            DisposeBackgroundImage()
            _realSize = realSize
            Me.BackgroundImage = bmp
            Me.BackgroundImageLayout = ImageLayout.Zoom
        End Sub

        ''' <summary>釋放縮圖(項目捲出畫面時;檔名、RealSize 保留)。ReloadIfUnloaded 再取回:
        ''' 最近用過的由 ThumbnailLoader 的快取直接給,不必重新解碼。</summary>
        Friend Sub UnloadImage()
            If String.IsNullOrEmpty(_fileName) OrElse IsPlayerCreated() Then Return
            _loadGen += 1
            DisposeBackgroundImage()
            SetThumb(Nothing)
            _unloaded = True
        End Sub

        Friend Sub ReloadIfUnloaded()
            If Not _unloaded Then Return
            Dim real As Size = _realSize
            LoadMedia()
            If _realSize.IsEmpty Then _realSize = real   ' 背景載入完成前,RealSize 維持原值
        End Sub

        ' 縮小版影像的目標框:控制項大小(至少 160x120,避免很小時又一再重建)
        Private Function DisplayBox() As Size
            Return New Size(Math.Max(160, Me.ClientSize.Width), Math.Max(120, Me.ClientSize.Height))
        End Function

        ''' <summary>讀出影像並縮小到剛好放進 <paramref name="box"/>(保持比例、不放大);
        ''' <paramref name="realSize"/> 傳回原始尺寸。讀不到(檔案損壞、記憶體不足)時傳回 Nothing。</summary>
        Friend Shared Function LoadDisplayImage(ByVal path As String, ByVal box As Size, ByRef realSize As Size) As Image
            Try
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using original As Image = Image.FromStream(fs, False, False)
                        realSize = original.Size
                        Dim scale As Double = Math.Min(1.0, Math.Min(box.Width / CDbl(original.Width), box.Height / CDbl(original.Height)))
                        Dim w As Integer = Math.Max(1, CInt(original.Width * scale)), h As Integer = Math.Max(1, CInt(original.Height * scale))
                        Dim bmp As New Bitmap(w, h)
                        Using g As Graphics = Graphics.FromImage(bmp)
                            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                            g.DrawImage(original, 0, 0, w, h)
                        End Using
                        Return bmp
                    End Using
                End Using
            Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is OutOfMemoryException OrElse
                                   TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                realSize = Size.Empty
                Return Nothing
            End Try
        End Function

        ' 控制項放大(例如縮圖大小滑桿)後,縮小版不夠清楚 → 停止調整 300ms 後從檔案重新產生
        Private WithEvents _regrowTimer As New Timer With {.Interval = 300}

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            Dim img As Image = Me.BackgroundImage
            If _isVideo OrElse img Is Nothing OrElse String.IsNullOrEmpty(_fileName) Then Return
            Dim box As Size = DisplayBox()
            Dim tooSmall As Boolean = (img.Width < box.Width AndAlso img.Width < _realSize.Width) AndAlso
                                  (img.Height < box.Height AndAlso img.Height < _realSize.Height)
            If tooSmall Then
                _regrowTimer.Stop()
                _regrowTimer.Start()
            End If
        End Sub

        Private Sub RegrowTimer_Tick(sender As Object, e As EventArgs) Handles _regrowTimer.Tick
            _regrowTimer.Stop()
            If _isVideo OrElse Me.BackgroundImage Is Nothing OrElse String.IsNullOrEmpty(_fileName) OrElse Not File.Exists(_fileName) Then Return
            If _loadAsync Then
                ' the smaller one stays up until the bigger one arrives
                _loadGen += 1
                RequestThumbnail(video:=False)
                Return
            End If
            Dim realSize As Size
            Dim img As Image = LoadDisplayImage(_fileName, DisplayBox(), realSize)
            If img Is Nothing Then Return
            DisposeBackgroundImage()
            Me.BackgroundImage = img
            Me.BackgroundImageLayout = ImageLayout.Zoom
        End Sub

        ' 以檔案複本載入影像，讀取後即釋放檔案控制代碼，不會鎖住原始檔
        Private Shared Function LoadImageWithoutLock(ByVal path As String) As Image
        Using fs As New FileStream(path, FileMode.Open, FileAccess.Read)
            Using original As Image = Image.FromStream(fs)
                Return New Bitmap(original)
            End Using
        End Using
    End Function

    ' 點擊控制項 (縮圖階段)：影片 → 開始播放;只有左鍵(右鍵是選單)
    Private Sub OnControlMouseClick(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        RaiseMediaClicked()
        If _isVideo AndAlso Not IsPlayerCreated() Then
            StartPlayback()
        End If
    End Sub

    ' 播放器是否已建立 (兩種實作共用的判斷)
    Private Function IsPlayerCreated() As Boolean
#If NETFRAMEWORK Then
        
Return wmpPlayer IsNot Nothing
#Else
        Return _mediaPlayer IsNot Nothing
#End If
    End Function

    ''' <summary>在背景預先載入影片播放引擎(LibVLC),讓第一次點擊播放不必等外掛載入。
    ''' 可重複呼叫;顯示影片縮圖時會自動呼叫,程式也可在啟動後先呼叫。.NET Framework 版不做事。</summary>
    Public Shared Sub PreloadVideoEngine()
#If Not NETFRAMEWORK Then
        SyncLock _vlcLock
            If _vlcTask IsNot Nothing AndAlso Not _vlcTask.IsFaulted Then Return
            _vlcTask = System.Threading.Tasks.Task.Run(
                Function()
                    LibVLCSharp.Shared.Core.Initialize()
                    Return New LibVLCSharp.Shared.LibVLC()
                End Function)
        End SyncLock
#End If
    End Sub

#If Not NETFRAMEWORK Then
    ' 共用的 LibVLC;背景還在載入時等它完成(載入失敗則重試一次並把例外丟出,與以前相同)
    Private Shared Function SharedLibVlc() As LibVLCSharp.Shared.LibVLC
        PreloadVideoEngine()
        Dim t As System.Threading.Tasks.Task(Of LibVLCSharp.Shared.LibVLC)
        SyncLock _vlcLock
            t = _vlcTask
        End SyncLock
        Return t.GetAwaiter().GetResult()
    End Function
#End If

    ' 建立播放器並開始播放
    Private Sub StartPlayback()
        If String.IsNullOrEmpty(_fileName) OrElse Not File.Exists(_fileName) Then Return

#If NETFRAMEWORK Then
        wmpPlayer = New AxWMPLib.AxWindowsMediaPlayer()
        wmpPlayer.Dock = DockStyle.Fill
        Me.Controls.Add(wmpPlayer)                 ' Add 之後 ActiveX 控制代碼才建立
        wmpPlayer.uiMode = "none"                  ' 隱藏內建控制列，改由點擊操作
        wmpPlayer.stretchToFit = True
        wmpPlayer.enableContextMenu = False
        wmpPlayer.settings.volume = _volume

        ' 播放器上點擊 → 切換 播放 / 暫停
        AddHandler wmpPlayer.ClickEvent, AddressOf OnPlayerClick
        AddHandler wmpPlayer.PlayStateChange, AddressOf OnWmpPlayStateChange
        AddHandler wmpPlayer.OpenStateChange, AddressOf OnWmpOpenStateChange

        ' 播放縮圖已無用，釋放以省 GDI(先留一份給拖曳用:播放中/暫停時仍可把媒體拖出)
        SetThumb(Me.BackgroundImage)
        DisposeBackgroundImage()

        wmpPlayer.URL = _fileName                  ' 預設 autoStart，設定 URL 即開始播放
#Else
        _videoView = New LibVLCSharp.WinForms.VideoView() With {.Dock = DockStyle.Fill}
        Me.Controls.Add(_videoView)                ' Add 之後才建立 handle
        Dim libVlc As LibVLCSharp.Shared.LibVLC = SharedLibVlc()   ' 已預先載入時立即取得
        _mediaPlayer = New LibVLCSharp.Shared.MediaPlayer(libVlc)
        ' 關閉 libvlc 自身的滑鼠/鍵盤處理,讓 VideoView 收得到滑鼠事件(才能點擊/拖曳)
        _mediaPlayer.EnableMouseInput = False
        _mediaPlayer.EnableKeyInput = False
        _mediaPlayer.Volume = _volume
        _videoView.MediaPlayer = _mediaPlayer

        ' 播放狀態 / 長度(libvlc 在自己的執行緒觸發,轉回 UI 執行緒)
        AddHandler _mediaPlayer.Playing, AddressOf OnVlcStateEvent
        AddHandler _mediaPlayer.Paused, AddressOf OnVlcStateEvent
        AddHandler _mediaPlayer.Stopped, AddressOf OnVlcStateEvent
        AddHandler _mediaPlayer.EndReached, AddressOf OnVlcEndReached
        AddHandler _mediaPlayer.LengthChanged, AddressOf OnVlcLengthChanged

        ' 播放器上點擊 → 切換 播放 / 暫停(拖曳改由訊息過濾器處理)
        AddHandler _videoView.MouseClick, AddressOf OnPlayerMouseClick

        ' 播放縮圖已無用，釋放以省 GDI(先留一份給拖曳用:播放中/暫停時仍可把媒體拖出)
        SetThumb(Me.BackgroundImage)
        DisposeBackgroundImage()

        Using media As New LibVLCSharp.Shared.Media(libVlc, _fileName, LibVLCSharp.Shared.FromType.FromPath)
            _mediaPlayer.Play(media)
        End Using

#End If
    End Sub

    ' 播放器上點擊 → 切換 播放 / 暫停
#If NETFRAMEWORK Then
   Private Sub OnPlayerClick(sender As Object, e As AxWMPLib._WMPOCXEvents_ClickEvent)
        RaiseMediaClicked()
        If wmpPlayer Is Nothing Then Return
        If wmpPlayer.playState = WMPLib.WMPPlayState.wmppsPlaying Then
            wmpPlayer.Ctlcontrols.pause()
        Else
            wmpPlayer.Ctlcontrols.play()
        End If
    End Sub
#Else
 Private Sub OnPlayerMouseClick(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return   ' 右鍵是選單,不當點擊(也不切換播放)
        RaiseMediaClicked()
        If _mediaPlayer Is Nothing Then Return
        If _mediaPlayer.IsPlaying Then
            _mediaPlayer.Pause()
        Else
            _mediaPlayer.Play()
        End If
    End Sub

    
#End If

    ' 設定/釋放保留用縮圖(獨立於背景圖,供拖曳影像使用)
    Private Sub SetThumb(ByVal img As Image)
        If _thumb IsNot Nothing Then
            _thumb.Dispose()
            _thumb = Nothing
        End If
        If img IsNot Nothing Then _thumb = New Bitmap(img)
    End Sub

    ' 釋放目前的背景影像
    Private Sub DisposeBackgroundImage()
        Dim old As Image = Me.BackgroundImage
        Me.BackgroundImage = Nothing
        If old IsNot Nothing Then
            old.Dispose()
        End If
    End Sub

    ' 釋放播放器
    Private Sub DisposePlayer()
#If NETFRAMEWORK Then
        
  If wmpPlayer IsNot Nothing Then
            RemoveHandler wmpPlayer.ClickEvent, AddressOf OnPlayerClick
            RemoveHandler wmpPlayer.PlayStateChange, AddressOf OnWmpPlayStateChange
            RemoveHandler wmpPlayer.OpenStateChange, AddressOf OnWmpOpenStateChange
            Try
                wmpPlayer.Ctlcontrols.stop()
                wmpPlayer.close()
            Catch
                ' 忽略關閉時的例外
            End Try
            Me.Controls.Remove(wmpPlayer)
            wmpPlayer.Dispose()
            wmpPlayer = Nothing
        End If
#Else
      If _videoView IsNot Nothing Then
            RemoveHandler _videoView.MouseClick, AddressOf OnPlayerMouseClick
        End If
        If _mediaPlayer IsNot Nothing Then
            RemoveHandler _mediaPlayer.Playing, AddressOf OnVlcStateEvent
            RemoveHandler _mediaPlayer.Paused, AddressOf OnVlcStateEvent
            RemoveHandler _mediaPlayer.Stopped, AddressOf OnVlcStateEvent
            RemoveHandler _mediaPlayer.EndReached, AddressOf OnVlcEndReached
            RemoveHandler _mediaPlayer.LengthChanged, AddressOf OnVlcLengthChanged
            Try
                _mediaPlayer.Stop()
            Catch
                ' 忽略關閉時的例外
            End Try
        End If
        If _videoView IsNot Nothing Then
            _videoView.MediaPlayer = Nothing        ' 先解除關聯再釋放，避免原生層當掉
            Me.Controls.Remove(_videoView)
            _videoView.Dispose()
            _videoView = Nothing
        End If
        If _mediaPlayer IsNot Nothing Then
            _mediaPlayer.Dispose()
            _mediaPlayer = Nothing
        End If
#End If
    End Sub

    ' ===== 影片播放控制(VB6 MediaPlayer:Play / Pause / Stop / PlayState / Duration /
    '       CurrentPosition / Volume / AutoRewind / ImageSourceWidth、Height) =====
    ' 設定 FileName 只會顯示縮圖;Play 才建立播放器開始播放(與點擊縮圖相同)。

    Private _autoRewind As Boolean = True
    Private _volume As Integer = 100

    ''' <summary>播放狀態改變(VB6 PlayStateChange)。</summary>
    Public Event PlayStateChanged(sender As Object, e As EventArgs)
    ''' <summary>影片已開啟、長度可取得(VB6 OpenStateChange)。</summary>
    Public Event MediaOpened(sender As Object, e As EventArgs)

    ''' <summary>目前載入的是否為影片。</summary>
    <Browsable(False)>
    Public ReadOnly Property IsVideo As Boolean
        Get
            Return _isVideo
        End Get
    End Property

    ''' <summary>播完是否回到開頭(VB6 AutoRewind,預設 True)。</summary>
    <Category("Behavior"), Description("播完是否回到開頭。"), DefaultValue(True)>
    Public Property AutoRewind As Boolean
        Get
            Return _autoRewind
        End Get
        Set(value As Boolean)
            _autoRewind = value
        End Set
    End Property

    ''' <summary>音量 0~100。</summary>
    <Category("Behavior"), Description("音量 0~100。"), DefaultValue(100)>
    Public Property Volume As Integer
        Get
            Return _volume
        End Get
        Set(value As Integer)
            _volume = Math.Max(0, Math.Min(100, value))
#If NETFRAMEWORK Then
            If wmpPlayer IsNot Nothing Then wmpPlayer.settings.volume = _volume
#Else
            If _mediaPlayer IsNot Nothing Then _mediaPlayer.Volume = _volume
#End If
        End Set
    End Property

    <Browsable(False)>
    Public ReadOnly Property PlayState As MediaPlayState
        Get
#If NETFRAMEWORK Then
            If wmpPlayer Is Nothing Then Return MediaPlayState.Stopped
            Try
                Select Case wmpPlayer.playState
                    Case WMPLib.WMPPlayState.wmppsPlaying : Return MediaPlayState.Playing
                    Case WMPLib.WMPPlayState.wmppsPaused : Return MediaPlayState.Paused
                    Case Else : Return MediaPlayState.Stopped
                End Select
            Catch
                Return MediaPlayState.Stopped
            End Try
#Else
            If _mediaPlayer Is Nothing Then Return MediaPlayState.Stopped
            If _mediaPlayer.IsPlaying Then Return MediaPlayState.Playing
            If _mediaPlayer.State = LibVLCSharp.Shared.VLCState.Paused Then Return MediaPlayState.Paused
            Return MediaPlayState.Stopped
#End If
        End Get
    End Property

    ''' <summary>影片長度(秒);尚未開啟為 0。</summary>
    <Browsable(False)>
    Public ReadOnly Property Duration As Double
        Get
#If NETFRAMEWORK Then
            If wmpPlayer Is Nothing OrElse wmpPlayer.currentMedia Is Nothing Then Return 0
            Return wmpPlayer.currentMedia.duration
#Else
            If _mediaPlayer Is Nothing OrElse _mediaPlayer.Length <= 0 Then Return 0
            Return _mediaPlayer.Length / 1000.0
#End If
        End Get
    End Property

    ''' <summary>目前播放位置(秒)。</summary>
    <Browsable(False)>
    Public Property CurrentPosition As Double
        Get
#If NETFRAMEWORK Then
            If wmpPlayer Is Nothing Then Return 0
            Return wmpPlayer.Ctlcontrols.currentPosition
#Else
            If _mediaPlayer Is Nothing OrElse _mediaPlayer.Time < 0 Then Return 0
            Return _mediaPlayer.Time / 1000.0
#End If
        End Get
        Set(value As Double)
#If NETFRAMEWORK Then
            If wmpPlayer IsNot Nothing Then wmpPlayer.Ctlcontrols.currentPosition = Math.Max(0, value)
#Else
            If _mediaPlayer IsNot Nothing AndAlso _mediaPlayer.IsSeekable Then _mediaPlayer.Time = CLng(Math.Max(0, value) * 1000)
#End If
        End Set
    End Property

    ''' <summary>影片原始解析度(VB6 ImageSourceWidth / Height);尚未開啟為 Size.Empty。</summary>
    <Browsable(False)>
    Public ReadOnly Property VideoSize As Size
        Get
#If NETFRAMEWORK Then
            If wmpPlayer Is Nothing OrElse wmpPlayer.currentMedia Is Nothing Then Return Size.Empty
            Return New Size(wmpPlayer.currentMedia.imageSourceWidth, wmpPlayer.currentMedia.imageSourceHeight)
#Else
            If _mediaPlayer Is Nothing Then Return Size.Empty
            Dim w As UInteger, h As UInteger
            If Not _mediaPlayer.Size(0UI, w, h) Then Return Size.Empty
            Return New Size(CInt(w), CInt(h))
#End If
        End Get
    End Property

    ''' <summary>播放(第一次呼叫時建立播放器);暫停中則繼續,播完則從頭。</summary>
    Public Sub Play()
        If Not _isVideo Then Return
        If Not IsPlayerCreated() Then
            StartPlayback()
            Return
        End If
#If NETFRAMEWORK Then
        wmpPlayer.Ctlcontrols.play()
#Else
        If _mediaPlayer.State = LibVLCSharp.Shared.VLCState.Ended Then _mediaPlayer.Stop()
        _mediaPlayer.Play()
#End If
    End Sub

    Public Sub Pause()
#If NETFRAMEWORK Then
        If wmpPlayer IsNot Nothing Then wmpPlayer.Ctlcontrols.pause()
#Else
        If _mediaPlayer IsNot Nothing AndAlso _mediaPlayer.IsPlaying Then _mediaPlayer.SetPause(True)
#End If
    End Sub

    ''' <summary>停止並回到開頭(播放器保留,可再 Play)。</summary>
    Public Sub [Stop]()
#If NETFRAMEWORK Then
        If wmpPlayer IsNot Nothing Then wmpPlayer.Ctlcontrols.stop()
#Else
        If _mediaPlayer IsNot Nothing Then
            _mediaPlayer.Stop()
            RaiseEvent PlayStateChanged(Me, EventArgs.Empty)
        End If
#End If
    End Sub

    ' 在 UI 執行緒執行(控制項已釋放則略過)
    Private Sub RunOnUi(ByVal action As MethodInvoker)
        If IsDisposed OrElse Disposing Then Return
        If Not IsHandleCreated Then Return
        Try
            BeginInvoke(action)
        Catch ex As InvalidOperationException
            ' 關閉中
        End Try
    End Sub

#If NETFRAMEWORK Then
    Private Sub OnWmpPlayStateChange(sender As Object, e As AxWMPLib._WMPOCXEvents_PlayStateChangeEvent)
        Const wmppsMediaEnded As Integer = 8
        If e.newState = wmppsMediaEnded AndAlso Not _autoRewind Then
            ' WMP 播完會自己回到開頭;不回頭時停在最後
            RunOnUi(Sub()
                        Try
                            wmpPlayer.Ctlcontrols.currentPosition = wmpPlayer.currentMedia.duration
                        Catch
                        End Try
                    End Sub)
        End If
        RaiseEvent PlayStateChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub OnWmpOpenStateChange(sender As Object, e As AxWMPLib._WMPOCXEvents_OpenStateChangeEvent)
        Const wmposMediaOpen As Integer = 13
        If e.newState = wmposMediaOpen Then RaiseEvent MediaOpened(Me, EventArgs.Empty)
    End Sub
#Else
    Private Sub OnVlcStateEvent(sender As Object, e As EventArgs)
        RunOnUi(Sub() RaiseEvent PlayStateChanged(Me, EventArgs.Empty))
    End Sub

    Private Sub OnVlcEndReached(sender As Object, e As EventArgs)
        ' libvlc 事件執行緒內不能呼叫 Stop,轉回 UI 執行緒
        RunOnUi(Sub()
                    If _mediaPlayer Is Nothing Then Return
                    If _autoRewind Then _mediaPlayer.Stop()
                    RaiseEvent PlayStateChanged(Me, EventArgs.Empty)
                End Sub)
    End Sub

    Private Sub OnVlcLengthChanged(sender As Object, e As LibVLCSharp.Shared.MediaPlayerLengthChangedEventArgs)
        RunOnUi(Sub() RaiseEvent MediaOpened(Me, EventArgs.Empty))
    End Sub
#End If

    ' ===== 拖放(Drag & Drop) =====

    Private _allowDragOut As Boolean = True

    ''' <summary>是否可把目前媒體拖出(VB6 MediaItem.DragItem)。拖入由 AllowDrop 控制。</summary>
    <Category("Behavior"), Description("是否可把目前媒體拖出。"), DefaultValue(True)>
    Public Property AllowDragOut As Boolean
        Get
            Return _allowDragOut
        End Get
        Set(value As Boolean)
            _allowDragOut = value
        End Set
    End Property

    ''' <summary>拖出結束時觸發(不論是否放下),帶出拖放結果(VB6 ItemCompleteDrag)。</summary>
    Public Event DragCompleted(ByVal effect As DragDropEffects)

    ''' <summary>媒體實際顯示的範圍(Zoom 後去掉空白邊);無圖時為整個工作區。</summary>
    Friend Function DisplayedImageRect() As Rectangle
        Dim cs As Size = Me.ClientSize
        Dim img As Image = Me.BackgroundImage
        If img Is Nothing OrElse img.Width <= 0 OrElse img.Height <= 0 Then Return New Rectangle(Point.Empty, cs)
        Dim scale As Double = Math.Min(cs.Width / CDbl(img.Width), cs.Height / CDbl(img.Height))
        Dim dw As Integer = CInt(img.Width * scale)
        Dim dh As Integer = CInt(img.Height * scale)
        Return New Rectangle((cs.Width - dw) \ 2, (cs.Height - dh) \ 2, dw, dh)
    End Function

    Private Sub OnMediaMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then _dragStart = e.Location
    End Sub

    Private Sub OnMediaMouseMove(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        If Not _allowDragOut OrElse String.IsNullOrEmpty(_fileName) Then Return
        ' 影片播放中不可拖曳(需先點一下暫停)
        If IsPlaying() Then Return
        ' 超過系統拖曳門檻才開始,避免點擊誤觸
        If Math.Abs(e.X - _dragStart.X) < SystemInformation.DragSize.Width \ 2 AndAlso
           Math.Abs(e.Y - _dragStart.Y) < SystemInformation.DragSize.Height \ 2 Then Return
        ' 影片暫停中:擷取目前秒數的影格作為縮圖
        If IsPlayerCreated() Then CaptureCurrentFrameToThumb()
        If DragImage() Is Nothing Then Return
        StartMediaDrag()
    End Sub

    ''' <summary>拖曳時跟著鼠標的影像:影片播放後擷取的影格(_thumb),否則就是顯示中的縮圖。
    ''' (以前每張縮圖都另外複製一份 _thumb 備用,縮圖記憶體因此加倍。)</summary>
    Private Function DragImage() As Image
        Return If(_thumb, Me.BackgroundImage)
    End Function

    ' 以目前縮圖(同尺寸、80% 透明)建立跟隨鼠標的浮動影像,開始拖曳
    Private Sub StartMediaDrag()
        Dim src As Image = DragImage()
        If src Is Nothing Then Return

        ' 只取「圖片本身」的顯示範圍(去掉 Zoom 造成的空白邊),浮動縮圖不含超過圖片的區域
        Dim cs As Size = Me.ClientSize
        Dim iw As Integer = Math.Max(1, src.Width)
        Dim ih As Integer = Math.Max(1, src.Height)
        Dim scale As Double = Math.Min(cs.Width / CDbl(iw), cs.Height / CDbl(ih))
        If scale <= 0 OrElse Double.IsInfinity(scale) OrElse Double.IsNaN(scale) Then scale = 1
        Dim dw As Integer = Math.Max(1, CInt(iw * scale))
        Dim dh As Integer = Math.Max(1, CInt(ih * scale))
        Dim ox As Integer = (cs.Width - dw) \ 2
        Dim oy As Integer = (cs.Height - dh) \ 2

        ' 抓取點相對於顯示的圖片(夾在圖片範圍內)→ 拖曳時影像維持在鼠標同一相對位置
        Dim gx As Integer = Math.Min(Math.Max(_dragStart.X - ox, 0), dw)
        Dim gy As Integer = Math.Min(Math.Max(_dragStart.Y - oy, 0), dh)
        _grabOffset = New Size(gx, gy)

        _dragImageForm = New DragImageForm(New Bitmap(src), New Size(dw, dh))
        PositionDragImage()
        _dragImageForm.Show()
        PositionDragImage()

        Dim result As DragDropEffects = DragDropEffects.None
        Try
            Dim data As New DataObject()
            data.SetData(MediaDragFormat, _fileName)                       ' 內部格式:MediaViewerControl 之間
            Dim files As New System.Collections.Specialized.StringCollection()
            files.Add(_fileName)
            data.SetFileDropList(files)                                    ' 檔案格式:可拖到檔案總管/其他程式(跨視窗跨 App)
            result = Me.DoDragDrop(data, DragDropEffects.Copy)
        Finally
            If _dragImageForm IsNot Nothing Then
                Dim bmp As Image = _dragImageForm.BackgroundImage
                _dragImageForm.BackgroundImage = Nothing
                _dragImageForm.Dispose()
                If bmp IsNot Nothing Then bmp.Dispose()
                _dragImageForm = Nothing
            End If
        End Try

        RaiseEvent DragCompleted(result)
        ' 拖曳完成(有放下)→ 觸發 DropMedia
        If result <> DragDropEffects.None Then RaiseEvent DropMedia(_fileName)
    End Sub

    ' 把浮動影像放到鼠標處(維持抓取點的相對位置 → 像整張圖被拖曳)
    Private Sub PositionDragImage()
        If _dragImageForm Is Nothing Then Return
        Dim p As Point = Cursor.Position
        _dragImageForm.Location = New Point(p.X - _grabOffset.Width, p.Y - _grabOffset.Height)
    End Sub

    Private Sub OnMediaGiveFeedback(sender As Object, e As GiveFeedbackEventArgs)
        PositionDragImage()   ' 拖曳過程持續更新浮動影像位置
    End Sub

    ' 影片是否正在播放
    Private Function IsPlaying() As Boolean
#If NETFRAMEWORK Then
        If wmpPlayer Is Nothing Then Return False
        Try
            Return wmpPlayer.playState = WMPLib.WMPPlayState.wmppsPlaying
        Catch
            Return False
        End Try

#Else
        
Return _mediaPlayer IsNot Nothing AndAlso _mediaPlayer.IsPlaying

#End If
    End Function

    ' 擷取目前影格作為拖曳縮圖(暫停中)。VLC 以 TakeSnapshot;WMP 無簡易方式則沿用既有縮圖。
    Private Sub CaptureCurrentFrameToThumb()
#If NETFRAMEWORK Then

#else
        If _mediaPlayer Is Nothing Then Return
        Dim tmp As String = Path.Combine(Path.GetTempPath(), "mvc_" & Guid.NewGuid().ToString("N") & ".png")
        Try
            Dim w As UInteger = CUInt(Math.Max(1, Me.Width))
            Dim h As UInteger = CUInt(Math.Max(1, Me.Height))
            If Not _mediaPlayer.TakeSnapshot(0UI, tmp, w, h) Then Return
            ' TakeSnapshot 可能略為非同步,等待檔案寫出(最多 ~600ms)
            Dim waited As Integer = 0
            While waited < 600 AndAlso (Not File.Exists(tmp) OrElse New FileInfo(tmp).Length = 0)
                System.Threading.Thread.Sleep(20)
                waited += 20
            End While
            If File.Exists(tmp) AndAlso New FileInfo(tmp).Length > 0 Then
                Using img As Image = LoadImageWithoutLock(tmp)
                    SetThumb(img)
                End Using
            End If
        Catch
            ' 擷取失敗 → 沿用既有縮圖
        Finally
            Try
                If File.Exists(tmp) Then File.Delete(tmp)
            Catch
            End Try
        End Try
#End If
    End Sub

    Private Sub OnMediaDragEnter(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(MediaDragFormat) OrElse e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        Else
            e.Effect = DragDropEffects.None
        End If
    End Sub

    Private Sub OnMediaDragDrop(sender As Object, e As DragEventArgs)
        Dim fn As String = Nothing
        If e.Data.GetDataPresent(MediaDragFormat) Then
            fn = TryCast(e.Data.GetData(MediaDragFormat), String)
        ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files IsNot Nothing AndAlso files.Length > 0 Then fn = files(0)
        End If
        If Not String.IsNullOrEmpty(fn) Then RaiseEvent DropMedia(fn)
    End Sub

    ' 影片畫面(VLC 原生視窗)拖曳:用執行緒訊息過濾器攔截滑鼠,不受原生視窗吃掉事件影響
    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Const WM_LBUTTONDOWN As Integer = &H201
        Const WM_MOUSEMOVE As Integer = &H200
        Const WM_LBUTTONUP As Integer = &H202
        ' 僅在「影片播放器已建立」時處理(圖片/縮圖階段用一般 MouseDown/Move)
        If Not IsPlayerCreated() Then Return False

        Select Case m.Msg
            Case WM_LBUTTONDOWN
                If _allowDragOut AndAlso IsCursorOverControl() Then
                    _dragStart = Me.PointToClient(Cursor.Position)
                    _mfPressed = True
                End If
            Case WM_MOUSEMOVE
                If _mfPressed AndAlso (Control.MouseButtons And MouseButtons.Left) = MouseButtons.Left Then
                    If IsPlaying() Then Return False      ' 播放中不可拖
                    Dim p As Point = Me.PointToClient(Cursor.Position)
                    If Math.Abs(p.X - _dragStart.X) >= SystemInformation.DragSize.Width \ 2 OrElse
                       Math.Abs(p.Y - _dragStart.Y) >= SystemInformation.DragSize.Height \ 2 Then
                        _mfPressed = False
                        CaptureCurrentFrameToThumb()     ' 暫停中:以目前影格當縮圖
                        If DragImage() IsNot Nothing Then StartMediaDrag()
                    End If
                End If
            Case WM_LBUTTONUP
                _mfPressed = False
        End Select
        Return False
    End Function

    Private Function IsCursorOverControl() As Boolean
        If Not Me.IsHandleCreated OrElse Not Me.Visible Then Return False
        Return Me.RectangleToScreen(Me.ClientRectangle).Contains(Cursor.Position)
    End Function

    ' 清空顯示
    Public Sub Clear()
        _fileName = Nothing
        _realSize = Size.Empty
        _isVideo = False
        DisposePlayer()
        DisposeBackgroundImage()
        SetThumb(Nothing)
    End Sub

    Private Sub MediaViewerControl_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class

''' <summary>
''' 拖曳時跟隨鼠標的浮動縮圖視窗:同尺寸、80% 不透明、滑鼠可穿透,
''' 讓拖放看起來像整張圖被拖曳,沒有視覺落差。
''' </summary>
Friend Class DragImageForm
    Inherits Form

    Public Sub New(ByVal img As Image, ByVal sz As Size)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.Manual
        Me.TopMost = True
        Me.Opacity = 0.8                       ' 透明度 80%
        Me.BackgroundImage = img
        Me.BackgroundImageLayout = ImageLayout.Stretch   ' 尺寸已等於圖片顯示範圍,直接填滿(無空白邊)
        Me.ClientSize = sz
    End Sub

    ' 加上 Layered + Transparent + NoActivate:半透明且滑鼠可穿透(不擋放置目標),也不搶焦點
    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Const WS_EX_LAYERED As Integer = &H80000
            Const WS_EX_TRANSPARENT As Integer = &H20
            Const WS_EX_NOACTIVATE As Integer = &H8000000
            Const WS_EX_TOOLWINDOW As Integer = &H80
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_LAYERED Or WS_EX_TRANSPARENT Or WS_EX_NOACTIVATE Or WS_EX_TOOLWINDOW
            Return cp
        End Get
    End Property

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property
End Class

''' <summary>影片播放狀態(VB6 MediaPlayer mpStopped / mpPaused / mpPlaying)。</summary>
Public Enum MediaPlayState
    Stopped = 0
    Paused = 1
    Playing = 2
End Enum

''' <summary>MediaClicked 事件參數，帶出被點擊媒體的檔名。</summary>
Public Class MediaClickedEventArgs
    Inherits EventArgs

    Private ReadOnly _fileName As String

    Public Sub New(fileName As String)
        _fileName = fileName
    End Sub

    ''' <summary>被點擊媒體的檔名 (可能為 Nothing)。</summary>
    Public ReadOnly Property FileName As String
        Get
            Return _fileName
        End Get
    End Property
End Class

End Namespace
