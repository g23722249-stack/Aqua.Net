Option Strict Off
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Design
Imports System.Windows.Forms

''' <summary>
''' 以圖片呈現的 RadioButton(參考 ImageCheckBox)。預設圖片取自 frmResource 的 picRadio 組。
''' 分群:GroupName 為空 → 同一父容器內的 ImageRadioButton 自動互斥;GroupName 非空 → 同表單內同名者互斥(不受容器限制)。
''' </summary>
Namespace Global.Aqua

    Public Class RadioButton
        Inherits UserControl

        Private _checked As Boolean = False
        Private _imageUnChecked As Image
        Private _imageChecked As Image
        Private _imageUnCheckDisabled As Image
        Private _imageCheckDisabled As Image
        Private _text As String = String.Empty
        Private _groupName As String = String.Empty

        ' === 色彩調整(純設計時工具)===
        ' 原始底圖:建構時載入的未染色啟用態圖(UnChecked/Checked),供編輯器非破壞性重複調整。
        ' 不序列化;兩張 Disabled 圖維持灰階不染色。
        Private _baseUnChecked As Image
        Private _baseChecked As Image

        ' 上次調整的 HSB 參數;-1 代表尚未調整過(編輯器改用底圖平均色作起點)。
        Private _tintHue As Integer = -1
        Private _tintSaturation As Integer = -1
        Private _tintBrightness As Integer = -1

        Public Event CheckedChanged As EventHandler

        Public Property ImageUnChecked As Image
            Get
                Return _imageUnChecked
            End Get
            Set(value As Image)
                _imageUnChecked = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Public Property ImageChecked As Image
            Get
                Return _imageChecked
            End Get
            Set(value As Image)
                _imageChecked = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Public Property ImageUnCheckDisabled As Image
            Get
                Return _imageUnCheckDisabled
            End Get
            Set(value As Image)
                _imageUnCheckDisabled = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Public Property ImageCheckDisabled As Image
            Get
                Return _imageCheckDisabled
            End Get
            Set(value As Image)
                _imageCheckDisabled = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        ''' <summary>分群名稱。空白時以「同一父容器」為群組;非空白時以「同表單內同名」為群組。</summary>
        <Category("行為")>
        <DefaultValue("")>
        <Description("分群名稱。空白 → 同一父容器內的 ImageRadioButton 互斥;非空白 → 同表單內同名者互斥。")>
        Public Property GroupName As String
            Get
                Return _groupName
            End Get
            Set(value As String)
                _groupName = If(value, String.Empty)
            End Set
        End Property

        Public Property Checked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If _checked <> value Then
                    _checked = value
                    ' 被選取時,取消同群其他顆(RadioButton 互斥)
                    If _checked Then UncheckPeers()
                    RefreshTransparent()
                    RaiseEvent CheckedChanged(Me, EventArgs.Empty)
                End If
            End Set
        End Property

        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Private _textGap As Integer = 4

        ''' <summary>圖片與文字之間的距離(像素)。</summary>
        <Category("外觀"), Description("圖片與文字之間的距離(像素)。"), DefaultValue(4)>
        Public Property TextGap As Integer
            Get
                Return _textGap
            End Get
            Set(value As Integer)
                value = Math.Max(0, value)
                If _textGap = value Then Return
                _textGap = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Public Property TextValue As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = value
                Me.Invalidate()
                AutoSizeControl()
            End Set
        End Property

        Public Sub New()
            ' 以 WS_EX_TRANSPARENT(見 CreateParams)搭配「不畫背景」,讓視窗真正透明,
            ' 圖片透明像素會透出背後內容(父背景圖/兄弟控制項)。不要開 OptimizedDoubleBuffer。
            Me.SetStyle(ControlStyles.SupportsTransparentBackColor Or
                    ControlStyles.UserPaint Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.ResizeRedraw, True)
            Me.SetStyle(ControlStyles.Opaque, False)
            Me.BackColor = Color.Transparent

            ' 預設圖整個程式只從 frmResource 載入一次,所有實例共用(見 ExamDefaultImages)
            ExamDefaultImages.EnsureLoaded()
            _imageUnChecked = ExamDefaultImages.RadioUnChecked
            _imageChecked = ExamDefaultImages.RadioChecked
            _imageUnCheckDisabled = ExamDefaultImages.RadioUnCheckDisabled
            _imageCheckDisabled = ExamDefaultImages.RadioDisabled

            ' 記住未染色的原始啟用態底圖
            _baseUnChecked = _imageUnChecked
            _baseChecked = _imageChecked
        End Sub

        ' === 分群互斥 ===

        ''' <summary>取消同群其他 ImageRadioButton 的選取。</summary>
        Private Sub UncheckPeers()
            For Each rb As RadioButton In GetPeers()
                If rb IsNot Me AndAlso rb.Checked Then
                    rb.Checked = False
                End If
            Next
        End Sub

        ''' <summary>依 GroupName 規則取得同群成員。</summary>
        Private Function GetPeers() As List(Of RadioButton)
            Dim result As New List(Of RadioButton)()
            Dim name As String = _groupName.Trim()

            If name.Length = 0 Then
                ' 群組 = 同一父容器
                If Me.Parent IsNot Nothing Then
                    For Each c As Control In Me.Parent.Controls
                        Dim rb As RadioButton = TryCast(c, RadioButton)
                        If rb IsNot Nothing AndAlso rb.GroupName.Trim().Length = 0 Then
                            result.Add(rb)
                        End If
                    Next
                End If
            Else
                ' 群組 = 同表單內同名(不限容器)
                Dim root As Control = Me.FindForm()
                If root Is Nothing Then root = GetTopParent()
                If root IsNot Nothing Then CollectByName(root, name, result)
            End If

            Return result
        End Function

        Private Function GetTopParent() As Control
            Dim c As Control = Me
            While c.Parent IsNot Nothing
                c = c.Parent
            End While
            Return c
        End Function

        Private Shared Sub CollectByName(container As Control, name As String, result As List(Of RadioButton))
            For Each c As Control In container.Controls
                Dim rb As RadioButton = TryCast(c, RadioButton)
                If rb IsNot Nothing AndAlso String.Equals(rb.GroupName.Trim(), name, StringComparison.Ordinal) Then
                    result.Add(rb)
                End If
                If c.HasChildren Then CollectByName(c, name, result)
            Next
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            MyBase.OnClick(e)
            ' RadioButton:點選即選取;點已選者維持選取(不會取消)
            If Me.Enabled AndAlso Not Me.Checked Then
                Me.Checked = True
            End If
        End Sub

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

            ' 選擇要顯示的圖片
            Dim img As Image = Nothing
            If Not Me.Enabled Then
                If _checked AndAlso _imageCheckDisabled IsNot Nothing Then
                    img = _imageCheckDisabled
                ElseIf _imageUnCheckDisabled IsNot Nothing Then
                    img = _imageUnCheckDisabled
                End If
            Else
                If _checked AndAlso _imageChecked IsNot Nothing Then
                    img = _imageChecked
                ElseIf _imageUnChecked IsNot Nothing Then
                    img = _imageUnChecked
                End If
            End If

            ' 繪製圖片 (垂直置中)
            Dim imgWidth As Integer = 0
            Dim imgHeight As Integer = Me.Font.Height
            Dim imgTop As Integer = 0
            If img IsNot Nothing Then
                imgWidth = img.Width
                imgHeight = img.Height
                imgTop = (Me.Height - imgHeight) \ 2
                g.DrawImage(img, 0, imgTop, img.Width, img.Height)
            End If

            ' 繪製文字 (圖片右側，垂直置中)
            ' 用 TextRenderer 量測與繪製,與 AutoSizeControl 的 TextRenderer.MeasureText 一致,
            ' 避免「量測用 GDI、繪製用 GDI+」造成執行階段被截字。
            Dim textSz As Size = TextRenderer.MeasureText(Me.Text, Me.Font)
            Dim textY As Integer = (Me.Height - textSz.Height) \ 2
            Dim textColor As Color = If(Me.Enabled, Me.ForeColor, Color.Gray)
            TextRenderer.DrawText(g, Me.Text, Me.Font, New Point(imgWidth + _textGap, textY), textColor)
        End Sub

        ' 字型變更時重新調整大小(避免 InitializeComponent 先設文字、後設字型導致量測偏小而截字)
        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            AutoSizeControl()
        End Sub

        Private _autoSizing As Boolean = False
        Private Sub AutoSizeControl()
            If _autoSizing Then Return
            _autoSizing = True
            Try
                ' 根據圖片+文字自動調整寬度
                Dim imgWidth As Integer = If(_imageUnChecked IsNot Nothing, _imageUnChecked.Width, 0)
                Dim imgHeight As Integer = If(_imageUnChecked IsNot Nothing, _imageUnChecked.Height, Me.Font.Height)

                ' 與 OnPaint 的 TextRenderer.DrawText 同引擎量測;+4 起點 + 4 尾端留白避免截字
                Dim textSize As Size = TextRenderer.MeasureText(Me.Text, Me.Font)
                Dim newWidth As Integer = imgWidth + _textGap + textSize.Width + 8
                Dim newHeight As Integer = Math.Max(imgHeight, textSize.Height)

                Dim changed As Boolean = (Me.Width <> newWidth) OrElse (Me.Height <> newHeight)
                If Me.Width <> newWidth Then Me.Width = newWidth
                If Me.Height <> newHeight Then Me.Height = newHeight

                ' 停靠(Dock=Left/Right)時,需請父容器重排,右側控制項才會依內容跟著縮放
                If changed AndAlso Me.Parent IsNot Nothing Then
                    Me.Parent.PerformLayout(Me, "Bounds")
                End If
            Finally
                _autoSizing = False
            End Try
        End Sub

        ' === 色彩調整用的公開介面(供 ImageRadioButtonColorEditor 存取)===

        ''' <summary>屬性視窗入口:按右側「…」開啟色彩調整視窗,以 ImageChecked 為預覽,調整色相/彩度/亮度後烘焙進 UnChecked/Checked 兩張啟用態圖。</summary>
        <Category("外觀")>
        <Description("開啟色彩調整視窗:以 ImageChecked 為預覽,個別調整色相/彩度/亮度,套用到 UnChecked/Checked 兩張啟用態圖(不影響 Disabled)。")>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        <Editor(GetType(ImageRadioButtonColorEditor), GetType(UITypeEditor))>
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
        Public ReadOnly Property BaseImageUnChecked As Image
            Get
                Return _baseUnChecked
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property BaseImageChecked As Image
            Get
                Return _baseChecked
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

        Private Sub InitializeComponent()
            Me.SuspendLayout()
            '
            'ImageRadioButton
            '
            Me.Name = "ImageRadioButton"
            Me.ResumeLayout(False)

        End Sub

        Private Sub ImageRadioButton_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        End Sub
    End Class

End Namespace
