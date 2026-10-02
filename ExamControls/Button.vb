Option Strict Off


Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Design
Imports System.Windows.Forms

Namespace Global.Aqua

    Public Class Button
        Inherits Control

        ' 四種狀態圖片
        Public Property ImageNormal As Image
        Public Property ImageHover As Image
        Public Property ImagePressed As Image
        Public Property ImageDisabled As Image

        ' === 色彩調整(純設計時工具)===
        ' 原始底圖:建構時從 frmResource 載入的未染色圖,供編輯器「非破壞性」重複調整之用。
        ' 不序列化(執行時直接用序列化後、已烘焙的 ImageNormal/Hover/Pressed)。
        Private _baseNormal As Image
        Private _baseHover As Image
        Private _basePressed As Image

        ' 上次調整的 HSB 參數;-1 代表尚未調整過(編輯器會改用底圖平均色作起點)。
        Private _tintHue As Integer = -1
        Private _tintSaturation As Integer = -1
        Private _tintBrightness As Integer = -1

        Private isHovered As Boolean = False
        Private isPressed As Boolean = False

        Public Sub New()
            ' 以 WS_EX_TRANSPARENT(見 CreateParams)搭配「不畫背景」,讓視窗真正透明:
            ' 不畫的區域會透出背後(父容器背景圖/兄弟控制項),只畫 PNG 本身。
            ' 注意:不要開 OptimizedDoubleBuffer,雙緩衝會蓋掉透明。
            Me.SetStyle(ControlStyles.SupportsTransparentBackColor Or
                    ControlStyles.UserPaint Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.ResizeRedraw, True)
            Me.SetStyle(ControlStyles.Opaque, False)
            Me.BackColor = Color.Transparent

            ' 預設圖整個程式只從 frmResource 載入一次,所有實例共用(見 ExamDefaultImages)
            ExamDefaultImages.EnsureLoaded()
            ImageNormal = ExamDefaultImages.ButtonNormal
            ImageHover = ExamDefaultImages.ButtonHover
            ImagePressed = ExamDefaultImages.ButtonPressed
            ImageDisabled = ExamDefaultImages.ButtonDisabled

            ' 記住未染色的原始底圖
            _baseNormal = ImageNormal
            _baseHover = ImageHover
            _basePressed = ImagePressed
        End Sub

        ' === 色彩調整用的公開介面(供 ImageButtonColorEditor 存取)===

        ''' <summary>屬性視窗入口:按右側「…」開啟色彩調整視窗,以 ImageNormal 為預覽,調整色相/彩度/亮度後烘焙進三張狀態圖。</summary>
        <Category("外觀")>
        <Description("開啟色彩調整視窗:以 ImageNormal 為預覽,個別調整色相/彩度/亮度,套用到 Normal/Hover/Pressed 三張圖(不影響 Disabled)。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        <Editor(GetType(ImageButtonColorEditor), GetType(UITypeEditor))>
        Public Property 色彩調整 As String
            Get
                Return "(按 … 調整)"
            End Get
            Set(value As String)
                ' 唯讀性質:值由編輯器操作,忽略直接指派
            End Set
        End Property

        ' 未染色的原始底圖(唯讀,不序列化)——編輯器據此非破壞性重算
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property BaseImageNormal As Image
            Get
                Return _baseNormal
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property BaseImageHover As Image
            Get
                Return _baseHover
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property BaseImagePressed As Image
            Get
                Return _basePressed
            End Get
        End Property

        ' 已套用的 HSB 參數:序列化保存,供下次開啟編輯器回填滑桿(-1 = 尚未調整)
        <Browsable(False), DefaultValue(-1)>
        Public Property TintHue As Integer
            Get
                Return _tintHue
            End Get
            Set(value As Integer)
                _tintHue = value
            End Set
        End Property

        <Browsable(False), DefaultValue(-1)>
        Public Property TintSaturation As Integer
            Get
                Return _tintSaturation
            End Get
            Set(value As Integer)
                _tintSaturation = value
            End Set
        End Property

        <Browsable(False), DefaultValue(-1)>
        Public Property TintBrightness As Integer
            Get
                Return _tintBrightness
            End Get
            Set(value As Integer)
                _tintBrightness = value
            End Set
        End Property

        ' === 狀態事件 ===
        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            isHovered = True
            RefreshTransparent()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            isHovered = False
            isPressed = False
            RefreshTransparent()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                isPressed = True
                RefreshTransparent()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If e.Button = MouseButtons.Left Then
                isPressed = False
                RefreshTransparent()
                ' 正確觸發 Click 事件
                MyBase.OnClick(EventArgs.Empty)
            End If
        End Sub

        ' === 繪製 ===
        ' WS_EX_TRANSPARENT:視窗透明,不畫的區域顯示背後內容(含父背景圖/兄弟控制項)
        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Const WS_EX_TRANSPARENT As Integer = &H20
                Dim cp As CreateParams = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or WS_EX_TRANSPARENT
                Return cp
            End Get
        End Property

        ' 不畫背景 → 保留視窗透明
        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        End Sub

        ' 狀態改變重繪時,連同背後區域一起刷新,避免殘影
        Private Sub RefreshTransparent()
            If Me.Parent IsNot Nothing Then
                Me.Parent.Invalidate(Me.Bounds, True)
            End If
            Me.Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)

            Dim g As Graphics = e.Graphics
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

            ' 選擇當前狀態圖片
            Dim currentImage As Image
            If Not Me.Enabled AndAlso ImageDisabled IsNot Nothing Then
                currentImage = ImageDisabled
            ElseIf isPressed AndAlso ImagePressed IsNot Nothing Then
                currentImage = ImagePressed
            ElseIf isHovered AndAlso ImageHover IsNot Nothing Then
                currentImage = ImageHover
            Else
                currentImage = ImageNormal
            End If

            If currentImage Is Nothing Then Return

            ' 原始圖片分割 (解析度 516x172)
            Dim leftRectSrc As New Rectangle(0, 0, 172, 172)
            Dim centerRectSrc As New Rectangle(172, 0, 172, 172)
            Dim rightRectSrc As New Rectangle(344, 0, 172, 172)

            ' 計算縮放比例 (保持圓弧比例)
            Dim scaleY As Single = CSng(Me.Height) / 172.0F
            Dim arcScaledWidth As Integer = CInt(172 * scaleY)

            ' 確保中間區域寬度合法
            Dim centerWidth As Integer = Math.Max(1, Me.Width - arcScaledWidth * 2)

            ' 目標區域
            Dim leftRectDest As New Rectangle(0, 0, arcScaledWidth, Me.Height)
            Dim rightRectDest As New Rectangle(Me.Width - arcScaledWidth, 0, arcScaledWidth, Me.Height)
            Dim centerRectDest As New Rectangle(arcScaledWidth, 0, centerWidth, Me.Height)

            ' 繪製三段
            g.DrawImage(currentImage, leftRectDest, leftRectSrc, GraphicsUnit.Pixel)
            g.DrawImage(currentImage, centerRectDest, centerRectSrc, GraphicsUnit.Pixel)
            g.DrawImage(currentImage, rightRectDest, rightRectSrc, GraphicsUnit.Pixel)

            ' 繪製文字 (置中)
            Dim textColor As Brush = If(Me.Enabled, Brushes.White, Brushes.DarkGray)
            Using sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.DrawString(Me.Text, Me.Font, textColor, Me.ClientRectangle, sf)
            End Using
        End Sub
    End Class

End Namespace
