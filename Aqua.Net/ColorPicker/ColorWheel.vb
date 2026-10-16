Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Namespace Global.Aqua

    ''' <summary>
    ''' 單獨的小色環（外圈色相、內接方塊飽和度 × 明度），給快速面板這類放不下整個選色器的地方用。
    ''' 配色跟著 Aqua.Theme。Color 屬性設定時不觸發 ColorChanged。
    ''' </summary>
    <DefaultEvent("ColorChanged"), DesignerCategory("Code")>
    Public Class ColorWheel
        Inherits Control
        Implements IThemeHost

        Private ReadOnly _state As New ColorState()
        Private ReadOnly _page As HueRingPage
        Private _silent As Boolean

        ''' <summary>使用者拖曳改了顏色（連續觸發）。</summary>
        Public Event ColorChanged As EventHandler

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            _page = New HueRingPage(_state) With {.Dock = DockStyle.Fill}
            Controls.Add(_page)
            AddHandler _state.Changed, Sub(s, e)
                                           _page.Invalidate()
                                           If Not _silent Then RaiseEvent ColorChanged(Me, EventArgs.Empty)
                                       End Sub
            AddHandler Global.Aqua.Theme.Changed, AddressOf OnThemeChanged
            Size = New Size(130, 130)
        End Sub

        Private Sub OnThemeChanged(ByVal sender As Object, ByVal e As EventArgs)
            If Not _customBack Then MyBase.BackColor = Scheme.Back
            Invalidate(True)
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing Then RemoveHandler Global.Aqua.Theme.Changed, AddressOf OnThemeChanged
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>配色跟著 Aqua.Theme；設了 BackColor 時色環後面用這個底色（和所在的面板同色）。</summary>
        Private ReadOnly Property Scheme As PickerTheme Implements IThemeHost.Scheme
            Get
                Dim s As PickerTheme = PickerTheme.For(ColorPickerTheme.Auto)
                Return If(_customBack, s.WithBack(BackColor), s)
            End Get
        End Property

        Private _customBack As Boolean

        Public Overrides Property BackColor As Color
            Get
                Return MyBase.BackColor
            End Get
            Set(ByVal value As Color)
                MyBase.BackColor = value
                _customBack = True
                Invalidate(True)
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Color As Color
            Get
                Return _state.Color
            End Get
            Set(ByVal value As Color)
                _silent = True
                Try
                    _state.Color = Color.FromArgb(255, value)
                Finally
                    _silent = False
                End Try
            End Set
        End Property

        Protected Overrides Sub OnCreateControl()
            MyBase.OnCreateControl()
            If Not _customBack Then MyBase.BackColor = Scheme.Back
        End Sub
    End Class

End Namespace
