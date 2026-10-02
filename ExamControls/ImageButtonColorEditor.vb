Option Strict Off
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Drawing.Design
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports System.Windows.Forms.Design

''' <summary>
''' ImageButton「色彩調整」屬性的設計時編輯器。
''' 開啟對話視窗,以 ImageNormal 為預覽,個別調整色相/彩度/亮度(整片染色),
''' 確定後從未染色底圖重算並烘焙進 Normal/Hover/Pressed 三張狀態圖(純設計時,結果序列化保存)。
''' </summary>
Namespace Global.Aqua

    Public Class ImageButtonColorEditor
        Inherits UITypeEditor

        Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
            Return UITypeEditorEditStyle.Modal
        End Function

        Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
            Dim btn As Button = If(context IsNot Nothing, TryCast(context.Instance, Button), Nothing)
            If btn Is Nothing OrElse provider Is Nothing Then Return value

            Dim edSvc As IWindowsFormsEditorService =
            TryCast(provider.GetService(GetType(IWindowsFormsEditorService)), IWindowsFormsEditorService)
            If edSvc Is Nothing Then Return value

            ' 起始 HSB:已調整過用保存值,否則取底圖平均色(選項 B)
            Dim h As Integer, s As Integer, b As Integer
            If btn.TintHue >= 0 Then
                h = btn.TintHue : s = btn.TintSaturation : b = btn.TintBrightness
            Else
                ImageTintHelper.AverageHsb(btn.BaseImageNormal, h, s, b)
            End If

            Using dlg As New frmImageTint(btn.BaseImageNormal, h, s, b)
                If edSvc.ShowDialog(dlg) = DialogResult.OK Then
                    Dim images As New Dictionary(Of String, Image)()
                    images("ImageNormal") = ImageTintHelper.Tint(btn.BaseImageNormal, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)
                    If btn.BaseImageHover IsNot Nothing Then _
                    images("ImageHover") = ImageTintHelper.Tint(btn.BaseImageHover, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)
                    If btn.BaseImagePressed IsNot Nothing Then _
                    images("ImagePressed") = ImageTintHelper.Tint(btn.BaseImagePressed, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)

                    TintApplier.Apply(btn, provider, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue, images)
                End If
            End Using

            Return value
        End Function
    End Class


    ''' <summary>
    ''' ImageCheckBox「色彩調整」屬性的設計時編輯器。
    ''' 以 ImageChecked 為預覽,染色套用到 UnChecked/Checked 兩張啟用態圖(不影響 Disabled)。
    ''' </summary>
    Public Class ImageCheckBoxColorEditor
        Inherits UITypeEditor

        Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
            Return UITypeEditorEditStyle.Modal
        End Function

        Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
            Dim chk As CheckBox = If(context IsNot Nothing, TryCast(context.Instance, CheckBox), Nothing)
            If chk Is Nothing OrElse provider Is Nothing Then Return value

            Dim edSvc As IWindowsFormsEditorService =
            TryCast(provider.GetService(GetType(IWindowsFormsEditorService)), IWindowsFormsEditorService)
            If edSvc Is Nothing Then Return value

            Dim previewBase As Image = chk.BaseImageChecked

            Dim h As Integer, s As Integer, b As Integer
            If chk.TintHue >= 0 Then
                h = chk.TintHue : s = chk.TintSaturation : b = chk.TintBrightness
            Else
                ImageTintHelper.AverageHsb(previewBase, h, s, b)
            End If

            Using dlg As New frmImageTint(previewBase, h, s, b)
                If edSvc.ShowDialog(dlg) = DialogResult.OK Then
                    Dim images As New Dictionary(Of String, Image)()
                    If chk.BaseImageUnChecked IsNot Nothing Then _
                    images("ImageUnChecked") = ImageTintHelper.Tint(chk.BaseImageUnChecked, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)
                    If chk.BaseImageChecked IsNot Nothing Then _
                    images("ImageChecked") = ImageTintHelper.Tint(chk.BaseImageChecked, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)

                    TintApplier.Apply(chk, provider, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue, images)
                End If
            End Using

            Return value
        End Function
    End Class


    ''' <summary>
    ''' ImageRadioButton「色彩調整」屬性的設計時編輯器。
    ''' 以 ImageChecked 為預覽,染色套用到 UnChecked/Checked 兩張啟用態圖(不影響 Disabled)。
    ''' </summary>
    Public Class ImageRadioButtonColorEditor
        Inherits UITypeEditor

        Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
            Return UITypeEditorEditStyle.Modal
        End Function

        Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
            Dim rb As RadioButton = If(context IsNot Nothing, TryCast(context.Instance, RadioButton), Nothing)
            If rb Is Nothing OrElse provider Is Nothing Then Return value

            Dim edSvc As IWindowsFormsEditorService =
            TryCast(provider.GetService(GetType(IWindowsFormsEditorService)), IWindowsFormsEditorService)
            If edSvc Is Nothing Then Return value

            Dim previewBase As Image = rb.BaseImageChecked

            Dim h As Integer, s As Integer, b As Integer
            If rb.TintHue >= 0 Then
                h = rb.TintHue : s = rb.TintSaturation : b = rb.TintBrightness
            Else
                ImageTintHelper.AverageHsb(previewBase, h, s, b)
            End If

            Using dlg As New frmImageTint(previewBase, h, s, b)
                If edSvc.ShowDialog(dlg) = DialogResult.OK Then
                    Dim images As New Dictionary(Of String, Image)()
                    If rb.BaseImageUnChecked IsNot Nothing Then _
                    images("ImageUnChecked") = ImageTintHelper.Tint(rb.BaseImageUnChecked, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)
                    If rb.BaseImageChecked IsNot Nothing Then _
                    images("ImageChecked") = ImageTintHelper.Tint(rb.BaseImageChecked, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue)

                    TintApplier.Apply(rb, provider, dlg.HueValue, dlg.SaturationValue, dlg.BrightnessValue, images)
                End If
            End Using

            Return value
        End Function
    End Class


    ''' <summary>把染色結果與 HSB 參數套回控制項,並透過設計工具的變更服務觸發序列化(供各編輯器共用)。</summary>
    Friend Module TintApplier

        ''' <summary>寫入 TintHue/Saturation/Brightness 三個參數與 imageProps 指定的各張圖片。</summary>
        Public Sub Apply(comp As Object, provider As IServiceProvider,
                     h As Integer, s As Integer, b As Integer,
                     imageProps As Dictionary(Of String, Image))
            Dim changeSvc As IComponentChangeService =
            TryCast(provider.GetService(GetType(IComponentChangeService)), IComponentChangeService)
            Dim props As PropertyDescriptorCollection = TypeDescriptor.GetProperties(comp)

            ' 先記參數(供下次回填滑桿),再烘焙圖片
            SetProp(changeSvc, comp, props, "TintHue", h)
            SetProp(changeSvc, comp, props, "TintSaturation", s)
            SetProp(changeSvc, comp, props, "TintBrightness", b)

            For Each kv As KeyValuePair(Of String, Image) In imageProps
                SetProp(changeSvc, comp, props, kv.Key, kv.Value)
            Next

            Dim ctrl As Control = TryCast(comp, Control)
            If ctrl IsNot Nothing Then ctrl.Invalidate()
        End Sub

        ''' <summary>以 PropertyDescriptor 設值,並包上變更通知,讓設計工具把新值序列化進宿主表單。</summary>
        Private Sub SetProp(changeSvc As IComponentChangeService, comp As Object,
                        props As PropertyDescriptorCollection, name As String, newValue As Object)
            Dim pd As PropertyDescriptor = props(name)
            If pd Is Nothing Then Return
            Dim oldValue As Object = pd.GetValue(comp)
            If changeSvc IsNot Nothing Then changeSvc.OnComponentChanging(comp, pd)
            pd.SetValue(comp, newValue)
            If changeSvc IsNot Nothing Then changeSvc.OnComponentChanged(comp, pd, oldValue, newValue)
        End Sub

    End Module


    ''' <summary>整片染色(tint)工具:保留原像素亮度與 Alpha,套用指定色相/彩度,並以亮度倍率縮放。</summary>
    Friend Module ImageTintHelper

        ''' <summary>
        ''' 逐像素整片染色。
        ''' hue:0–360;saturation:0–100(%);brightness:0–200(%,100=原亮度)。
        ''' 保留原圖 Alpha(維持透明圓角),不改變透明像素。
        ''' </summary>
        Public Function Tint(src As Image, hue As Integer, saturation As Integer, brightness As Integer) As Bitmap
            Dim bmp As New Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.DrawImage(src, New Rectangle(0, 0, bmp.Width, bmp.Height))
            End Using

            Dim h As Double = ((hue Mod 360) + 360) Mod 360
            Dim s As Double = Math.Max(0, Math.Min(100, saturation)) / 100.0
            Dim bScale As Double = Math.Max(0, brightness) / 100.0

            Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
            Dim data As BitmapData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim n As Integer = Math.Abs(data.Stride) * bmp.Height
                Dim buf(n - 1) As Byte
                Marshal.Copy(data.Scan0, buf, 0, n)

                Dim i As Integer = 0
                While i < n
                    ' 記憶體排列為 BGRA
                    Dim b0 As Integer = buf(i)
                    Dim g0 As Integer = buf(i + 1)
                    Dim r0 As Integer = buf(i + 2)
                    Dim a0 As Integer = buf(i + 3)

                    If a0 <> 0 Then
                        Dim lum As Double = (0.299 * r0 + 0.587 * g0 + 0.114 * b0) / 255.0
                        Dim l As Double = lum * bScale
                        If l > 1.0 Then l = 1.0
                        If l < 0.0 Then l = 0.0

                        Dim rr As Integer, gg As Integer, bb As Integer
                        HslToRgb(h, s, l, rr, gg, bb)
                        buf(i) = CByte(bb)
                        buf(i + 1) = CByte(gg)
                        buf(i + 2) = CByte(rr)
                        ' a0(buf(i+3))保留
                    End If
                    i += 4
                End While

                Marshal.Copy(buf, 0, data.Scan0, n)
            Finally
                bmp.UnlockBits(data)
            End Try

            Return bmp
        End Function

        ''' <summary>取底圖平均色相/彩度作為編輯器起始值(選項 B)。忽略透明像素。回傳亮度固定 100。</summary>
        Public Sub AverageHsb(src As Image, ByRef hue As Integer, ByRef saturation As Integer, ByRef brightness As Integer)
            hue = 0 : saturation = 0 : brightness = 100
            If src Is Nothing Then Return

            Using bmp As New Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.DrawImage(src, New Rectangle(0, 0, bmp.Width, bmp.Height))
                End Using

                Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
                Dim data As BitmapData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                Try
                    Dim n As Integer = Math.Abs(data.Stride) * bmp.Height
                    Dim buf(n - 1) As Byte
                    Marshal.Copy(data.Scan0, buf, 0, n)

                    ' 色相為角度,直接平均會在 0/360 交界出錯,故以向量(cos/sin)累加
                    Dim sumX As Double = 0, sumY As Double = 0, sumS As Double = 0
                    Dim count As Long = 0
                    Dim i As Integer = 0
                    While i < n
                        Dim a0 As Integer = buf(i + 3)
                        If a0 >= 16 Then ' 略過幾乎全透明像素
                            Dim hh As Double, ss As Double, ll As Double
                            RgbToHsl(buf(i + 2), buf(i + 1), buf(i), hh, ss, ll)
                            ' 近乎無彩(灰)像素不納入色相統計,避免拉偏
                            If ss > 0.05 Then
                                Dim rad As Double = hh * Math.PI / 180.0
                                sumX += Math.Cos(rad)
                                sumY += Math.Sin(rad)
                                sumS += ss
                                count += 1
                            End If
                        End If
                        i += 4
                    End While

                    If count > 0 Then
                        Dim ang As Double = Math.Atan2(sumY / count, sumX / count) * 180.0 / Math.PI
                        If ang < 0 Then ang += 360
                        hue = CInt(Math.Round(ang))
                        saturation = CInt(Math.Round((sumS / count) * 100.0))
                    End If
                Finally
                    bmp.UnlockBits(data)
                End Try
            End Using
        End Sub

        ' H:0-360, S/L:0-1 → RGB 0-255
        Private Sub HslToRgb(h As Double, s As Double, l As Double, ByRef r As Integer, ByRef g As Integer, ByRef b As Integer)
            If s <= 0.0 Then
                Dim v As Integer = Clamp255(l * 255.0)
                r = v : g = v : b = v
                Return
            End If

            Dim c As Double = (1.0 - Math.Abs(2.0 * l - 1.0)) * s
            Dim hp As Double = h / 60.0
            Dim x As Double = c * (1.0 - Math.Abs((hp Mod 2.0) - 1.0))
            Dim r1 As Double = 0, g1 As Double = 0, b1 As Double = 0

            Select Case CInt(Math.Floor(hp)) Mod 6
                Case 0 : r1 = c : g1 = x : b1 = 0
                Case 1 : r1 = x : g1 = c : b1 = 0
                Case 2 : r1 = 0 : g1 = c : b1 = x
                Case 3 : r1 = 0 : g1 = x : b1 = c
                Case 4 : r1 = x : g1 = 0 : b1 = c
                Case Else : r1 = c : g1 = 0 : b1 = x
            End Select

            Dim m As Double = l - c / 2.0
            r = Clamp255((r1 + m) * 255.0)
            g = Clamp255((g1 + m) * 255.0)
            b = Clamp255((b1 + m) * 255.0)
        End Sub

        ' RGB 0-255 → H:0-360, S/L:0-1
        Private Sub RgbToHsl(r As Integer, g As Integer, b As Integer, ByRef h As Double, ByRef s As Double, ByRef l As Double)
            Dim rd As Double = r / 255.0, gd As Double = g / 255.0, bd As Double = b / 255.0
            Dim max As Double = Math.Max(rd, Math.Max(gd, bd))
            Dim min As Double = Math.Min(rd, Math.Min(gd, bd))
            Dim d As Double = max - min
            l = (max + min) / 2.0

            If d <= 0.0 Then
                h = 0 : s = 0
                Return
            End If

            s = d / (1.0 - Math.Abs(2.0 * l - 1.0))
            If max = rd Then
                h = 60.0 * (((gd - bd) / d) Mod 6.0)
            ElseIf max = gd Then
                h = 60.0 * (((bd - rd) / d) + 2.0)
            Else
                h = 60.0 * (((rd - gd) / d) + 4.0)
            End If
            If h < 0 Then h += 360
        End Sub

        Private Function Clamp255(v As Double) As Integer
            If v < 0 Then Return 0
            If v > 255 Then Return 255
            Return CInt(v)
        End Function

    End Module


    ''' <summary>色彩調整對話視窗:左側預覽底圖,右側色相/彩度/亮度三滑桿。與控制項型別無關。</summary>
    Friend Class frmImageTint
        Inherits Form

        Private ReadOnly _base As Image
        Private ReadOnly _initHue As Integer
        Private ReadOnly _initSat As Integer
        Private ReadOnly _initBri As Integer

        Private previewBox As PictureBox
        Private trkHue As TrackBar
        Private trkSat As TrackBar
        Private trkBri As TrackBar
        Private lblHue As Label
        Private lblSat As Label
        Private lblBri As Label

        Public ReadOnly Property HueValue As Integer
            Get
                Return trkHue.Value
            End Get
        End Property
        Public ReadOnly Property SaturationValue As Integer
            Get
                Return trkSat.Value
            End Get
        End Property
        Public ReadOnly Property BrightnessValue As Integer
            Get
                Return trkBri.Value
            End Get
        End Property

        Public Sub New(previewBase As Image, initHue As Integer, initSat As Integer, initBri As Integer)
            _base = previewBase
            _initHue = initHue
            _initSat = initSat
            _initBri = initBri

            BuildUi()

            trkHue.Value = ClampTrack(trkHue, _initHue)
            trkSat.Value = ClampTrack(trkSat, _initSat)
            trkBri.Value = ClampTrack(trkBri, _initBri)
            UpdatePreview()
        End Sub

        Private Shared Function ClampTrack(trk As TrackBar, v As Integer) As Integer
            If v < trk.Minimum Then Return trk.Minimum
            If v > trk.Maximum Then Return trk.Maximum
            Return v
        End Function

        Private Sub BuildUi()
            Me.Text = "色彩調整"
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.StartPosition = FormStartPosition.CenterParent
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(560, 320)

            ' 預覽區(灰底以看清透明與染色效果)
            previewBox = New PictureBox() With {
            .Location = New Point(12, 12),
            .Size = New Size(300, 260),
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.FromArgb(120, 120, 120),
            .BorderStyle = BorderStyle.FixedSingle
        }
            Me.Controls.Add(previewBox)

            Dim panelX As Integer = 330
            Dim panelW As Integer = 218

            lblHue = MakeLabel(panelX, 20)
            trkHue = MakeTrack(panelX, 40, panelW, 0, 360)
            lblSat = MakeLabel(panelX, 100)
            trkSat = MakeTrack(panelX, 120, panelW, 0, 100)
            lblBri = MakeLabel(panelX, 180)
            trkBri = MakeTrack(panelX, 200, panelW, 0, 200)


            Dim btnOk As New System.Windows.Forms.Button() With {
            .Text = "確定", .DialogResult = DialogResult.OK,
            .Location = New Point(panelX + panelW - 170, 270), .Size = New Size(80, 28)
        }

            Dim btnCancel As New System.Windows.Forms.Button() With {
            .Text = "取消", .DialogResult = DialogResult.Cancel,
            .Location = New Point(panelX + panelW - 80, 270), .Size = New Size(80, 28)
        }
            Me.Controls.Add(btnOk)
            Me.Controls.Add(btnCancel)
            Me.AcceptButton = btnOk
            Me.CancelButton = btnCancel
        End Sub

        Private Function MakeLabel(x As Integer, y As Integer) As Label
            Dim l As New Label() With {.Location = New Point(x, y), .AutoSize = True}
            Me.Controls.Add(l)
            Return l
        End Function

        Private Function MakeTrack(x As Integer, y As Integer, w As Integer, min As Integer, max As Integer) As TrackBar
            Dim t As New TrackBar() With {
            .Location = New Point(x, y), .Width = w,
            .Minimum = min, .Maximum = max,
            .TickStyle = TickStyle.None, .SmallChange = 1, .LargeChange = 10
        }
            AddHandler t.ValueChanged, AddressOf OnTrackChanged
            Me.Controls.Add(t)
            Return t
        End Function

        Private Sub OnTrackChanged(sender As Object, e As EventArgs)
            UpdatePreview()
        End Sub

        Private Sub UpdatePreview()
            lblHue.Text = "色相 (Hue): " & trkHue.Value
            lblSat.Text = "彩度 (Saturation): " & trkSat.Value & "%"
            lblBri.Text = "亮度 (Brightness): " & trkBri.Value & "%"

            If _base Is Nothing Then Return
            Dim old As Image = previewBox.Image
            previewBox.Image = ImageTintHelper.Tint(_base, trkHue.Value, trkSat.Value, trkBri.Value)
            If old IsNot Nothing Then old.Dispose()
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            MyBase.OnFormClosed(e)
            If previewBox IsNot Nothing AndAlso previewBox.Image IsNot Nothing Then
                previewBox.Image.Dispose()
                previewBox.Image = Nothing
            End If
        End Sub

    End Class

End Namespace
