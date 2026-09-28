Option Strict Off
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''這是進階的 KEY IMAGE 功能，由原本的打勾，提升為使用星等來表示 KEY IMAGE 的重要性
Namespace Global.Aqua

<DefaultEvent("RatingChanged")>
Public Class RatingControl
    Inherits UserControl

    Private _rating As Integer = 0
    Public Property Rating As Integer
        Get
            Return _rating
        End Get
        Set(value As Integer)
            Dim newValue As Integer = Math.Max(0, Math.Min(5, value))
            If _rating <> newValue Then
                _rating = newValue
                Me.Invalidate()
                RaiseEvent RatingChanged(_rating)
            End If
        End Set
    End Property

    Public Event RatingChanged(newRating As Integer)

    Public Sub New()
        InitializeComponent()
        Me.Width = 200
        Me.Height = 40
        Me.DoubleBuffered = True
        ' only the stars are drawn: let whatever it sits on show around them
        Me.SetStyle(ControlStyles.SupportsTransparentBackColor, True)
        MyBase.BackColor = Color.Transparent
    End Sub

    <DefaultValue(GetType(Color), "Transparent")>
    Public Overrides Property BackColor As Color
        Get
            Return MyBase.BackColor
        End Get
        Set(value As Color)
            MyBase.BackColor = value
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        ' 星星大小依照控制項高度縮放
        Dim starSize As Integer = Me.Height - 10
        Dim spacing As Integer = starSize \ 6

        ' 計算總寬度
        Dim totalWidth As Integer = 5 * starSize + 4 * spacing
        Dim startX As Integer = (Me.Width - totalWidth) \ 2

        For i As Integer = 0 To 4
            Dim x As Integer = startX + i * (starSize + spacing)
            Dim rect As New Rectangle(x, (Me.Height - starSize) \ 2, starSize, starSize)

            ' 判斷顏色
            Dim centerColor As Color, surroundColor As Color
            If i < _rating Then
                centerColor = Color.FromArgb(255, 255, 200) ' 淺黃
                surroundColor = Color.FromArgb(255, 200, 0) ' 深黃
            Else
                centerColor = Color.FromArgb(220, 220, 220) ' 淺灰
                surroundColor = Color.FromArgb(128, 128, 128) ' 深灰
            End If

            Using path As GraphicsPath = CreateStar(rect)
                Using brush As New PathGradientBrush(path) With {
                    .CenterColor = centerColor,
                    .SurroundColors = New Color() {surroundColor}
                }
                    g.FillPath(brush, path)
                    g.DrawPath(Pens.Gray, path)
                End Using
            End Using
        Next
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)

        Dim starSize As Integer = Me.Height - 10
        Dim spacing As Integer = starSize \ 6
        Dim totalWidth As Integer = 5 * starSize + 4 * spacing
        Dim startX As Integer = (Me.Width - totalWidth) \ 2

        For i As Integer = 0 To 4
            Dim x As Integer = startX + i * (starSize + spacing)
            Dim rect As New Rectangle(x, (Me.Height - starSize) \ 2, starSize, starSize)
            If rect.Contains(e.Location) Then
                Rating = i + 1
                Exit For
            End If
        Next
    End Sub

    Private Function CreateStar(rect As Rectangle) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim cx As Single = rect.X + rect.Width / 2
        Dim cy As Single = rect.Y + rect.Height / 2
        Dim rOuter As Single = rect.Width / 2
        Dim rInner As Single = rOuter / 2.5

        Dim pts As New List(Of PointF)
        For i As Integer = 0 To 9
            Dim angle As Double = i * Math.PI / 5 - Math.PI / 2
            Dim r As Single = If(i Mod 2 = 0, rOuter, rInner)
            Dim px As Single = cx + r * Math.Cos(angle)
            Dim py As Single = cy + r * Math.Sin(angle)
            pts.Add(New PointF(px, py))
        Next

        path.AddPolygon(pts.ToArray())
        Return path
    End Function

    Private Sub RatingControl_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class

End Namespace
